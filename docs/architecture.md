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

### How does each store keep its own products without clashing with other stores?

**Short answer:** every product belongs to exactly one store. The same item in two stores is simply two separate rows.

Each product row carries a `store_id`. That single column keeps stores apart — when Store A loads its products, it only ever asks for rows where `store_id = StoreA`, so it can never see or touch Store B's products.

Even if two stores sell the *same* item with the same SKU, they each get their own row:

```
Store_Product
 store_product_id  store_id  sku       name   price
 ----------------  --------  --------  -----  -----
 101               StoreA    SKU-123   Coke   1.50
 102               StoreB    SKU-123   Coke   1.75   <- same SKU, own row, own price
```

Within a single store a SKU can't be listed twice (enforced by a unique rule on `store_id` + `sku`), but other stores are free to use that same SKU. So stores stay fully independent and can price the same product differently.

### How does an organization owner see everything across all stores?

**Short answer:** we don't keep a separate org-wide copy. The owner's data is gathered live by listing the org's stores and reading from each.

Every store knows its org (`Store.organization_id`), and every store-owned row knows its store (`store_id`). So "show me everything in the org" is two steps: find the org's stores, then read the rows for those stores.

```sql
-- Step 1: which stores belong to this org?
SELECT store_id FROM Store WHERE organization_id = 'org_1';
-- -> ['StoreA', 'StoreB', 'StoreC']

-- Step 2: read whatever you need for those stores
SELECT * FROM Store_Product WHERE store_id IN ('StoreA', 'StoreB', 'StoreC');
SELECT * FROM Store_Order   WHERE store_id IN ('StoreA', 'StoreB', 'StoreC');
```

The same two steps work for orders, returns, customers, and staff. We chose this over copying data up to the org so there's only ever **one source of truth** (the store) and nothing to keep in sync. At our scale (~100 stores) this is fast. If it ever slows down, we can add a pre-built read copy (a search index or summary table) without changing where the real data lives.

### How do we know *what* and *where* a user has access to?

**Short answer:** one endpoint (called right after login) returns every place the user belongs to — their org and their stores — with the exact permissions they have at each place.

A user's access is two things together: the **place** (a store, or the org) and **what they can do there**. We never look at these separately, because the same permission can mean different things at different places. For example, John can refund at Store B but not at Store A — so "can John refund?" only makes sense when you also say *where*.

**Where the data comes from.** Two membership tables tell us the places: `Store_Membership` (their stores) and `Organization_Membership` (their org). From each membership we follow its role to that role's permissions. We read both tables and keep the place attached to every permission:

```sql
-- store-level access
SELECT sm.store_id AS place, p.name AS permission
FROM Store_Membership sm
JOIN Role_Permission rp ON rp.role_id = sm.role_id
JOIN Permission p       ON p.permission_id = rp.permission_id
WHERE sm.user_id = 'John'

UNION ALL

-- org-level access
SELECT om.organization_id AS place, p.name AS permission
FROM Organization_Membership om
JOIN Role_Permission rp ON rp.role_id = om.role_id
JOIN Permission p       ON p.permission_id = rp.permission_id
WHERE om.user_id = 'John';
```

(The UNION here is honest — it combines two genuinely different lists, org access and store access — not a workaround for an ambiguous column.)

**What the endpoint returns.** A single response listing the org and each store, with names, roles, and the permissions at each place — everything the UI needs to draw the app:

```http
GET /me/access
```

```json
{
  "user": { "user_id": "John", "name": "John" },
  "organization": {
    "id": "org_1",
    "name": "Acme Inc",
    "role": "RootAdmin",
    "permissions": ["organization:read"]
  },
  "stores": [
    {
      "id": "StoreA",
      "name": "Acme Seattle",
      "role": "Cashier",
      "permissions": ["store:read", "store:sell"]
    },
    {
      "id": "StoreB",
      "name": "Acme Portland",
      "role": "Manager",
      "permissions": ["store:read", "store:sell", "store:refund"]
    }
  ]
}
```

From this one payload the UI knows the user's org and stores (for the workspace switcher) and exactly which buttons to show at each (from the per-place permissions).

**Checking a single action.** With that data loaded, the app offers one helper, `can(action, place)`, where `place` is the org id or a store id:

```
can("store:refund",      "StoreB")  -> true    (Manager there)
can("store:refund",      "StoreA")  -> false   (only Cashier there)
can("organization:read", "org_1")   -> true    (granted at the org)
```

Because the place is always part of the check, a permission earned at one store (or at the org) can never leak somewhere it wasn't granted.

### How can an admin create their own custom roles?

**Short answer:** custom roles live in the same `Role` table as our built-in ones; two columns mark who owns each role.

- `is_managed` — `true` means **we** built and maintain it; `false` means a customer made it.
- `organization_id` — empty for our global roles; set to the owning org for custom ones.

```
Role
 role_id  name          is_managed  organization_id   notes
 -------  -----------   ----------  ----------------  --------------------------
 1        RootAdmin     true        (none)            global, ships with the app
 4        Cashier       true        (none)            global
 50       NightManager  false       org_1             custom, created by org_1
```

To create a custom role, an admin inserts a `Role` row (`is_managed = false`, their `organization_id`) and picks its permissions in `Role_Permission`. That role is visible only to their org. Role names only need to be unique per owner, so two different orgs can each have a "Manager" role without clashing.

### What happens to existing users when we change a global role's permissions?

**Short answer:** the change applies to everyone with that role immediately — because permissions are looked up through the role, not copied onto each user.

A user doesn't store a snapshot of permissions; they point at a role, and the role points at its permissions. So if we edit a global (`is_managed = true`) role, the next permission check for **every** user with that role — in every org — sees the new rules. Nothing to re-sync.

That's powerful, so two product rules soften it:

1. **Warn on assignment.** When an admin picks a global role, the UI notes that we manage it and its permissions may change over time.
2. **Offer "Clone."** An admin can copy a global role's permissions into a new custom role (`is_managed = false`, their org). They assign the copy instead, which freezes it from our future changes and lets them edit it freely.

**Example — org_1 wants a Root Admin it controls.** It clones, rather than editing the global one:

```
Role
 role_id  name        is_managed  organization_id
 -------  ---------   ----------  ----------------
 1        RootAdmin   true        (none)            <- global, still shared, untouched
 60       RoleAdmin   false       org_1             <- org_1's own copy, edits freely
```

org_1 assigns its members to role 60. The global Root Admin keeps working unchanged for every other org.

### How do we tell apart tables we own vs. tables customers fill?

**Short answer:** by the table's job. Some tables only we write to, some only customers, and one (`Role`) is shared and marked by `is_managed`.

| Kind | Who creates rows | Tables |
|------|------------------|--------|
| **Ours only** | Only us | `Plan`, `Feature`, `Permission` — customers just reference these |
| **Shared** | Both | `Role` — `is_managed = true` is ours, otherwise it's a customer's custom role |
| **Customer-owned** | Customers | `Organization`, `Store`, and every `Store_*` table |

So for any row, "who owns this?" is answered by which table it's in — and for the one shared table, by its `is_managed` flag.

### How does an organization get the features included in its plan?

**Short answer:** features come through the plan, not the org. The org picks one plan; the plan lists its features.

```
Organization ──(plan_id)──> Plan ──< Plan_Feature >── Feature
```

To find an org's features, follow its plan:

```
org_1 is on the "Pro" plan
Pro includes -> [multi_store, reports, returns]
=> org_1 has: multi_store, reports, returns
```

A feature check ("can org_1 use reports?") just asks whether its plan includes that feature.

**One plan per org** — `plan_id` sits on the `Organization` row, so an org has exactly one plan at a time. (If we later need plan history, we'd add a separate table for it.)

**New features spread automatically.** Because features are read through the plan (never copied onto each org), adding one is a single insert into `Plan_Feature`, and **every org on that plan gains it at once**:

```sql
INSERT INTO Plan_Feature (plan_id, feature_id) VALUES ('Pro', 'ai_analytics');
-- Every organization on the Pro plan now has AI Analytics, instantly.
```

### (Optional) Can products, users and orders be created at the org level, or must you be in a specific store?

*(pending — product decision)*

### (Optional) Can one store search another store's products, read-only?

**Short answer:** yes, if the user has permission. It's the same "list the org's stores, then read" approach, limited to reading.

```sql
-- Stores in the same org as the current user
SELECT store_id FROM Store WHERE organization_id = 'org_1';

-- Search products across those stores (read-only)
SELECT store_id, name, price
FROM Store_Product
WHERE store_id IN ('StoreA', 'StoreB', 'StoreC')
  AND name ILIKE '%coke%';
```

Two things keep it safe: the user must hold a permission like `product:read_cross_store`, and the query only ever **reads** other stores — it never writes to them. Each store still fully owns its own products.
