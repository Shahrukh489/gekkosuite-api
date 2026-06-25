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

There is no org-level copy of this data, so the owner sees it by **federated read**: org → its stores → their rows. Every store has `organization_id`, and every store-owned row has `store_id`, so:

```sql
-- Stores in org
Store         WHERE organization_id = org_1   -- -> [StoreA, StoreB, StoreC]
-- All products
Store_Product WHERE store_id IN (StoreA, StoreB, StoreC)
-- All orders
Store_Order   WHERE store_id IN (...)
-- All returns
Store_Return  WHERE store_id IN (...)
```

Same pattern for every entity. If this gets slow later, add a read-optimized layer (materialized view / search index); the source of truth stays per-store.

### How does one user belong to multiple stores?

One user, one account. Belonging is expressed through `Membership` — **one row per place the user is granted a role**: `user_id` is the principal (who), `role_id` is the set of permissions (what they can do), and `resource_id` is the resource the grant is scoped to (the store or org). The role's `resource_type` says whether `resource_id` points at a STORE or the ORG.

```
Membership
 user_id  role_id    resource_id   (role.resource_type)
 -------  ---------  -----------   --------------------
 John     Cashier    StoreA         STORE
 John     Manager    StoreB         STORE
```

To put John in a third store, insert one more row. To remove him from StoreB, delete that one row — his account is untouched. So a user belongs to as many stores as they have store memberships, each with its own role.

### How do we know which store or org a user belongs to?

Read the user's `Membership` rows. Each row's `resource_id` is the store or org they belong to, and the joined `Role.resource_type` says which kind:

```sql
SELECT m.resource_id, r.resource_type
FROM Membership m JOIN Role r ON r.role_id = m.role_id
WHERE m.user_id = John
-- StoreA / STORE,  StoreB / STORE   -> John belongs to StoreA and StoreB
```

`resource_id` is **never null** — an org-level grant points at the org itself (org *is* the resource), so there's no ambiguous "null means no access" reading.

### How do we know if a user has organization level permissions?

Org-level access is just a membership whose role has `resource_type = ORG`. One condition, no scanning every store:

```sql
SELECT 1
FROM Membership m JOIN Role r ON r.role_id = m.role_id
WHERE m.user_id = Owner AND r.resource_type = 'ORG'
```

```
Membership
 user_id  role_id     resource_id   (role.resource_type)
 -------  ----------  -----------   --------------------
 Owner    RootAdmin   org_1          ORG     <- org-level: authority over the whole org
 John     Cashier     StoreA         STORE   <- store-level only
```

If the user has any `resource_type = ORG` membership, they operate at the organization level (e.g. RootAdmin over `org_1`); otherwise they're store staff.

### How do we allow a store admin or organization admin to create custom roles?

The `Role` table holds both the global roles we ship and custom roles companies create. They're told apart by `organization_id`: **null = global (we own it)**, otherwise the role belongs to that organization. Managed and custom roles are the same entity distinguished by one owner field — not separate tables.

```
Role
 role_id  name           organization_id   -- null = global
 -------  -------------  ----------------
 1        RootAdmin      null               <- global, shipped by us
 4        Cashier        null               <- global
 50       NightManager   org_1              <- custom, made by org_1's admin
```

Creating a custom role = inserting a `Role` row with `organization_id` set to that admin's organization, then attaching permissions via `Role_Permission`. Custom roles are invisible to other orgs (filtered by `organization_id`). Role names are unique per owner via `(name, organization_id)`; store and org roles never overlap, so no separate scope column is needed.

**Example — org_1 wants its own version of the global Root Admin.** It does *not* edit the global role. It creates its own role with `organization_id` populated and assigns its members to that instead:

```
Role
 role_id  name         organization_id   -- null = global
 -------  -----------  ----------------
 1        RootAdmin    null               <- global, untouched, still shared by everyone else
 60       RoleAdmin    org_1              <- org_1's custom role with its own permissions

Role_Permission
 role_id  permission_id
 -------  -------------
 1        ...           <- global Root Admin's permissions (unchanged)
 60       manage_stores <- org_1's chosen permissions for RoleAdmin
 60       view_reports
```

Now org_1's members point at role 60 (`RoleAdmin`). Their permissions are fully their own, and the global `RootAdmin` (role 1) is unaffected for every other organization.

### If a user has a Global role, what happens when we update the permissions of that role?

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

### How do we distinguish global tables (we own them) vs custom company tables?

Three tiers, decided by the table category (and for the one shared table, by `is_managed` / `organization_id`):

1. **Global-only tables** — only *we* ever insert; customers only reference them. No owner column: `Plan`, `Feature`, `Permission`.
2. **Shared table** — both we *and* customers insert: `Role`. Told apart by `is_managed` (true = we manage it, `organization_id` null) vs custom (`is_managed` false, `organization_id` set).
3. **Company-owned tables** — always belong to a tenant and always carry an org/store id: `Organization`, `Store`, and all `Store_*`.

```
Global-only  (no owner col):   Plan, Feature, Permission
Shared       (is_managed):     Role        -- managed = ours, else org's custom
Company-owned (org/store id):  Organization, Store, Store_Product, Store_Order, ...
```

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

**Adding features is easy and propagates automatically.** Because features are resolved through the plan at read time (not copied onto each org), adding a feature to a plan is a single `Plan_Feature` insert, and **every org on that plan instantly gains it** — no per-org updates. Removing a feature from a plan removes it for all those orgs the same way.

```
Add "AI Analytics" to the Pro plan:
   INSERT Plan_Feature(Pro, AI Analytics)
   => every org with plan_id = Pro now has "AI Analytics", automatically
```

### (Optional) Can we add products, users and orders to store at root org level or must be logged into store to do?
(**pending**)

### (Optional) How can one store search other store products in read-only?

Same federated-read mechanism as Q2, scoped to products and **gated by permission**. A store still owns its own rows; "search other stores" is just a read across sibling stores in the same org, allowed only if the user's role grants it.

```sql
-- Sibling stores in the same org
Store WHERE organization_id = (the user's store's org)   -- -> [StoreA, StoreB, StoreC]

-- Cross-store product search (read-only)
Store_Product WHERE store_id IN (those stores) AND name ILIKE '%coke%'
```

Read-only is enforced two ways: a **permission** such as `product:read_cross_store` on the role, and the query only ever `SELECT`s from other stores — never writes. Later, the same read-optimized layer from Q2 (materialized view / search index) speeds it up.
