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

### How do we know what access a user has?

**Short answer:** at login we take the `user_id`, read their `Membership` rows, join through to roles and permissions, and return a JSON object keyed by `resource_id` — each place (a store or the org) mapped to the permissions the user has there.

A user's access is always a **place** (the `resource_id` — a store or the org) plus **what they can do there** (the permissions). We resolve both in one pass: from each membership we follow its role to that role's permissions, keeping the `resource_id` attached so we never lose *where* a permission applies.

But `Membership` alone only gives us an opaque `resource_id` — a bare id with no name and no way to tell a store from the org. To label each place, we resolve the `resource_id` against the `Organization` and `Store` tables with a **UNION**: a membership matches one or the other, and whichever table it matches tells us the type and name.

```sql
-- store-level access (resource_id matches a Store)
SELECT s.store_id AS resource_id, 'STORE' AS type, s.name, p.name AS permission
FROM Membership m
JOIN Store s            ON s.store_id = m.resource_id
JOIN Role_Permission rp ON rp.role_id = m.role_id
JOIN Permission p       ON p.permission_id = rp.permission_id
WHERE m.user_id = 'John'

UNION ALL

-- org-level access (resource_id matches an Organization)
SELECT o.organization_id AS resource_id, 'ORG' AS type, o.name, p.name AS permission
FROM Membership m
JOIN Organization o     ON o.organization_id = m.resource_id
JOIN Role_Permission rp ON rp.role_id = m.role_id
JOIN Permission p       ON p.permission_id = rp.permission_id
WHERE m.user_id = 'John';
```

We group the rows by `resource_id` and return that to the UI at login:

```json
{
  "user_id": "John",
  "access": {
    "org_1":  { "type": "ORG",   "name": "Acme Inc",      "permissions": ["organization:read"] },
    "StoreA": { "type": "STORE", "name": "Acme Seattle",  "permissions": ["store:read", "store:sell"] },
    "StoreB": { "type": "STORE", "name": "Acme Portland", "permissions": ["store:read", "store:sell", "store:refund"] }
  }
}
```

The org and the stores sit in the same shape — `resource_id` is just "the place." The UNION is what tells us which kind each place is (and its name), since the `resource_id` on its own can't. The UI reads this once to build the workspace switcher (the keys, labeled by `type`/`name`) and to show/hide features per place (the permission lists).

`resource_id` is **never null** — an org-level grant points at the org itself (org *is* the resource), so there's no ambiguous "null means no access" case. We also don't store a separate resource *type*: the permission name's subject (`store:*` vs `organization:*`) already implies the level.

With this loaded, a single check `can(action, place)` answers any "is this allowed?" question:

```
can("store:refund",      "StoreB")  -> true    (he's Manager there)
can("store:refund",      "StoreA")  -> false   (only Cashier there)
can("organization:read", "org_1")   -> true    (granted at the org)
```

Because the place is always part of the check, a permission earned at one store (or the org) can never leak somewhere it wasn't granted.

### How do we allow an organization admin to create custom roles?

Custom roles are created at the **organization level only** (a store admin doesn't make their own roles — the org owns the role catalog). The `Role` table holds both the global roles we ship and custom roles orgs create. Two columns tell them apart: **`is_managed`** (true = we own/manage it) and **`organization_id`** (null for managed/global roles, the owning org for custom ones). Managed and custom roles are the same entity distinguished by these owner fields — not separate tables.

```
Role
 role_id  name           is_managed  organization_id
 -------  -------------  ----------  ----------------
 1        RootAdmin      true        null               <- global, shipped by us
 4        Cashier        true        null               <- global
 50       NightManager   false       org_1              <- custom, made by org_1's admin
```

Creating a custom role = inserting a `Role` row with `is_managed = false` and `organization_id` set to that admin's organization, then attaching permissions via `Role_Permission`. Custom roles are invisible to other orgs (filtered by `organization_id`). Role names are unique per owner via `(name, organization_id)`; store and org roles never overlap, so no separate scope column is needed.

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
