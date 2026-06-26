Author: Salman Hoosein
Version : 1.0
Project:
Description: 



# Target Niche 
We serve small and mid-size businesses running a multi-store POS. A typical organization has up to ~100 stores. At this scale we keep the design simple with a monolith application and single database

# Constraints
- Organization must have minimum one store to sell products
    - Create one by default on onboarding

# FAQ

### How will each store keep its own product without conflicts in other stores?

Every product row carries a `store_id`. That column is the isolation — a product belongs to exactly one store, and every query filters by `store_id`.

```
Store_Product
 store_product_id  store_id  sku       name
 ----------------  --------  --------  ------
 101               StoreA    SKU-123   Coke
 102               StoreB    SKU-123   Coke
```

The same SKU in two stores is just two separate rows — no conflict. Store A only ever sees `store_id = StoreA`. They never collide because the unique key is `store_product_id` (per row), not the SKU.

SKU is unique **within** a store via a unique index on `(store_id, sku)`: a store can't list the same SKU twice, but other stores still can.

### How will an organization owner see products, users, roles, orders, returns across all stores?

There is no org-level copy of this data, so the owner sees it by **federated read**: we first find the org's stores, then read each thing (products, orders, returns, customers) for those stores and combine the results. 

1. Find the stores that belong to the organization.
2. Read the products / orders / returns / customers for those stores, and merge them into one list.

The same two steps work for every kind of record. At ~100 stores this is fast. If it ever slows down, we can keep a pre-built read copy (a search index or summary table) to speed it up — the real data still lives in each store.

### How do we know what access a user has?

**Short answer:** at login we look up everywhere the user belongs (their stores and their org) and what they can do at each, and return a single list of "place → permissions."

A user's access is always a **place** (a store or the org) plus **what they can do there**. The `User_Role` table records this: each row links a user to a role at one place. A role has permissions attached to it. Putting it all together we get a user with a role that has permissions for a place.

It looks like this:

```
John's access
 place     kind    name            can do
 -------   -----   -------------   -----------------------------------
 org_1     ORG     Acme Inc         organization:read
 StoreA    STORE   Acme Seattle     store:read, store:sell
 StoreB    STORE   Acme Portland    store:read, store:sell, store:refund
```


**A note on integrity.** A `User_Role` row points at *either* an organization or a store depending on its resource type. A database can't make one link rule cover two different tables, so it won't on its own stop a row from pointing at a place that was deleted (a "dangling reference"). We handle this the way large systems do  and guard it in the application: check the place exists when a row is created, remove a place's rows when the place is deleted, and run an occasional sweep to catch any strays. With only two resource types this is cheap and enough. If strict database-enforced integrity ever becomes a hard requirement, see **Alternate design: BusinessUnit** below.

The place is **always filled in** — an org-level grant points at the org itself — so there's no confusing "empty means nowhere" case. And we don't need to store the level separately: the permission name already implies it (`store:…` vs `organization:…`).

With this loaded, the app answers any "is this allowed?" question by checking an action **and** a place together:

- Can John refund at Store B? → yes (he's Manager there).
- Can John refund at Store A? → no (he's only a Cashier there).
- Can John read the organization? → yes (granted at the org).

Because the place is always part of the check, a permission earned at one store (or the org) can never leak somewhere it wasn't granted.

### How do we allow an organization admin to create custom roles?

Custom roles are created at the **organization level only**. The `Role` table holds both the global roles we ship and custom roles orgs create. Two columns tell them apart: **`is_managed`** (true = we own/manage it) and **`organization_id`** (null for managed/global roles, the owning org for custom ones). Managed and custom roles are the same entity distinguished by these owner fields — not separate tables.

```
Role
 role_id  name           is_managed  organization_id
 -------  -------------  ----------  ----------------
 1        RootAdmin      true        null               <- global, shipped by us
 4        Cashier        true        null               <- global
 50       NightManager   false       org_1              <- custom, made by org_1's admin
```

Creating a custom role = inserting a `Role` row with `is_managed = false` and `organization_id` set to that admin's organization, then attaching permissions via `Role_Permission`. Custom roles are invisible to other orgs (filtered by `organization_id`). Role names are unique per owner via `(name, organization_id)`;

**Example — org_1 wants its own version of the global Root Admin.** It does *not* edit the global role. It creates its own role (`is_managed = false`, `organization_id = org_1`) and assigns its members to that instead:

```
Role
 role_id  name         is_managed  organization_id
 -------  -----------  ----------  ----------------
 1        RootAdmin    true        null               <- global, untouched, still shared by everyone else
 60       RoleAdmin    false       org_1              <- org_1's custom role with its own permissions

Role_Permission
 role_id  permission         -- subject:action
 -------  ----------------
 1        ...                <- global Root Admin's permissions (unchanged)
 60       store:manage       <- org_1's chosen permissions for RoleAdmin
 60       organization:read
```

Now org_1's members point at role 60 (`RoleAdmin`). Their permissions are fully their own, and the global `RootAdmin` (role 1) is unaffected for every other organization.

### If a user has a Managed role, what happens when we update the permissions of that role?

Permissions link to the role through `Role_Permission` (not copied onto users), so updating a **managed** role (`is_managed = true`) instantly applies to every existing user who has that role, across all orgs — there's nothing to propagate. The next permission check sees the new rules.

```
Managed Role "RootAdmin" --< Role_Permission >-- Permissions
        ^                                            ^
   existing users               we edit here = everyone updates at once
   point at this role
```

Two product decisions follow from this:

1. **Warn at assignment time.** When an admin assigns a managed role, the UI tells them this role is managed by us and its permissions may change in the future (and those changes will apply to their users automatically).

2. **Offer "Clone".** Give a UI action to clone a managed role's permissions into a new custom role (`is_managed = false`, their `organization_id`). The org assigns the clone instead, so it's frozen from our updates and they can edit it freely. The clone shadows the managed role of the same name (resolved via `(name, organization_id)`), leaving the managed role untouched for every other org.

### How does an organization get features for their plan?

The chain is **Organization → Plan → Features**. The org points at a plan (`Organization.plan_id`), and the plan is linked to its features via `Plan_Feature`. Features aren't attached to the org directly — you always go through the plan.

```
Organization.plan_id ──> Plan ──< Plan_Feature >── Feature

org_1 (plan_id = Pro)
   Pro --< Plan_Feature >-- [multi_store, reports, returns]
   => org_1 has: multi_store, reports, returns
```

A feature check ("can org_1 use reports?") = does the org's plan include that feature in `Plan_Feature`.

**One plan per org.** `plan_id` lives on the `Organization` row, so each org has exactly one plan at a time. (If we ever need plan history or multiple plans, we'd reintroduce a separate `Organization_Plan` table.)

**Adding features is easy and propagates automatically.** Because features are read through the plan (not copied onto each org), adding a feature to a plan instantly gives it to **every org on that plan** — no per-org updates. For example, adding "AI Analytics" to the Pro plan means every organization on the Pro plan now has AI Analytics, automatically. Removing a feature from a plan removes it for all those orgs the same way.

### (Optional) Can we add products, users and orders to store at root org level or must be logged into store to do?
(**pending**)

### (Optional) How can one store search other store products in read-only?

Same federated-read approach as the org-wide owner view above, but limited to products and **gated by permission**. A store still owns its own products; "search other stores" just finds the sibling stores in the same org and reads their products — it never writes to them.

Two things keep it safe: the user must hold a cross-store read permission (such as `product:read_cross_store`), and the search only ever reads other stores, never changes them. If it ever gets slow, the same pre-built read copy from the federated-read answer can speed it up.

# Alternate design: BusinessUnit (strong foreign keys)

The main design keeps `Organization` and `Store` as two separate, cleanly-shaped tables, and `User_Role` points at one or the other with a **polymorphic** `(resource_type, resource_id)`. The tradeoff (noted above) is that this reference can't be a single database foreign key, so we guard against dangling references in the application.

If database-enforced integrity ever becomes a hard requirement, the alternative is a **single unified table** — call it `BusinessUnit` — where both organizations and stores live as rows:

```
BusinessUnit(business_unit_id, type, parent_id, name, plan_id, manager, country, region, city, state, zip_code, ...)
   type:      ORG | STORE
   parent_id: the org a store belongs to (null for an org)

User_Role(user_id, role_id, business_unit_id)   -- business_unit_id is a REAL foreign key to BusinessUnit
```

**What this buys you**

- **A real foreign key.** `User_Role.business_unit_id` references one table, so the database guarantees every grant points at a place that exists, and deleting a place automatically cleans up its grants. No dangling references, ever — no app-side validation or triggers needed.
- **One uniform reference everywhere** (audits, grants, queries), which is the most index- and shard-friendly shape if you grow to thousands of orgs.

**What it costs**

- **One table with many nulls.** An org row and a store row need different columns — `plan_id` only applies to orgs; `manager`, `region` only to stores. In one table those become nullable columns that are empty for half the rows (an org row has a null `manager`; a store row has a null `plan_id`). The table's shape no longer cleanly describes either thing.
- The database can't enforce "an ORG must have a plan_id and a STORE must have a manager" without conditional `CHECK` constraints, so some validation moves back into the app anyway.
- Every store-only or org-only query gains a `WHERE type = 'STORE'` / `'ORG'` filter.

A common refinement is to keep `BusinessUnit` for the **shared** fields only (`business_unit_id, type, parent_id, name`) and put the type-specific columns in detail tables (`Organization`, `Store`) keyed by `business_unit_id`. That removes the nulls and keeps the real foreign key — at the price of an extra join to fetch details.

**Why the main design doesn't use this:** we only ever have two unit types (org and store) and the sole downside of the polymorphic approach is dangling references, which a small write-time validation (and optional trigger) fully handles. The `BusinessUnit` table trades that cheap, contained cost for either a null-filled table or an extra join on every read — complexity we don't need at our scale. It's documented here as the path to take **if** strict DB-level integrity or a deeper unit hierarchy (e.g. regions) ever becomes a real requirement.
