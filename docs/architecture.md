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

## How does each store keep its own products separate from other stores?

Every product belongs to exactly one store. We record this with a `store_id` on every product row, and a store only ever loads products with its own `store_id`. So one store can never see or change another store's products.

If two stores happen to sell the same item with the same SKU, each store still gets its own separate row — with its own price and stock:

```
Store_Product
 store_id   sku       name   price
 --------   --------  -----  -----
 StoreA     SKU-123   Coke   1.50
 StoreB     SKU-123   Coke   1.75
```

Within a single store, the same SKU can't be listed twice. Across stores it's fine — they're independent.


## How does an organization owner see everything across all stores?

We don't keep a separate org-wide copy of the data. Instead, the owner's view is built on demand in two steps:

1. Find the stores that belong to the organization.
2. Read the products, orders, returns, and customers for those stores, and combine them into one list.

This works because every store knows which org it belongs to, and every record (a product, an order, etc.) knows which store it belongs to.

At ~100 stores this is fast. If it ever gets slow, we can keep a pre-built copy of the data for quick reading (a search index or summary table), while the real records still live in each store.


## How do we know what a user can do, and where?

A user's access always has two parts: a **place** (a store, or the organization) and **what they can do there**.

We store this with a few small pieces:

- A **user** is one account (one login).
- A **role** is a named set of permissions, like "Cashier" or "Manager."
- A **user-role** record connects a user to a role *at one place* — for example, "John is a Manager at Store B." Each record names the place with one of two fields: `store_id` for a store, or `organization_id` for the organization. Exactly one is filled in — that tells us both the place and whether it's a store or the org.

A person can have several of these records — one per place they work — so they can be a Cashier at one store and a Manager at another. When the user logs in, we gather all of these and produce one simple list of where they can go and what they can do at each place:

```
John's access
 place    type    name             can do
 ------   -----   --------------   ----------------------------------
 org_1    ORG     Acme Inc         organization:read
 StoreA   STORE   Acme Seattle     store:read, store:sell
 StoreB   STORE   Acme Portland    store:read, store:sell, store:refund
```

Because each record points at a real store or a real organization (a proper database link), a record can never refer to a place that doesn't exist, and deleting a place automatically removes its access records.

## How does an organization or store create its own custom roles?

Besides the built-in roles we ship (like Root Admin and Cashier), customers can create their own roles. A custom role can belong to a whole **organization** (every store in the org can use it) or to a single **store** (only that store uses it). It is visible only to its owner — no other org or store sees it.

All roles live in one `Role` table. A few fields tell us who owns each role:

- **`is_managed`** — `true` means we built and maintain it; `false` means a customer created it.
- **`organization_id`** — set when the role belongs to a whole organization.
- **`store_id`** — set when the role belongs to a single store.

A built-in role has neither owner field set. A custom role has exactly one of them set — that says whether it's an org-wide role or a store-only role.

```
Role
 name           is_managed  organization_id  store_id   meaning
 ------------   ----------  ---------------  --------   --------------------------------
 RootAdmin      true        (empty)          (empty)    built-in, shipped by us
 Cashier        true        (empty)          (empty)    built-in
 NightManager   false       org_1            (empty)    custom, used across all of org_1
 WeekendOpener  false       (empty)          StoreA     custom, used only at Store A
```

To make a custom role, the owner creates a `Role` row (`is_managed = false`) with their `organization_id` *or* their `store_id` filled in, then chooses its permissions. Names only need to be unique within the owner, so two different orgs (or stores) can each have a "Manager" role without clashing.


## What happens to existing users when a built-in role's permissions change?

Users don't keep their own copy of permissions — they point at a role, and the role points at its permissions. So if we change a built-in (managed) role, **everyone who has that role gets the change immediately**, in every organization. There's nothing to re-apply.

This is convenient, but it means an org can't freely edit a built-in role without affecting everyone. So we give them two things:

1. **A heads-up.** When an admin assigns a built-in role, the app notes that we manage it and its permissions may change over time.

2. **A "Clone" option.** An admin can copy a built-in role's permissions into a new custom role of their own. They assign the copy instead — now they can edit it freely, and our future changes to the built-in role won't touch it.

**Example.** Org_1 wants its own Root Admin it fully controls. It clones, rather than editing the shared built-in one:

```
Role
 name        is_managed  organization_id   meaning
 ---------   ----------  ----------------  ------------------------------
 RootAdmin   true        (empty)           built-in, still shared, untouched
 RoleAdmin   false       org_1             org_1's own copy, edits freely
```

Org_1 assigns its members to `RoleAdmin`. The built-in Root Admin keeps working unchanged for every other org.


## How does an organization get the features in its plan?

An organization is on one **plan** (like "Pro"), and each plan includes a set of **features** (like reports or multi-store). The org gets its features *through its plan* — features aren't attached to the org directly.

```
Organization → Plan → Features

org_1 is on the "Pro" plan
Pro includes: multi_store, reports, returns
So org_1 has: multi_store, reports, returns
```

To check a feature ("can org_1 use reports?"), we look at whether its plan includes that feature.

**One plan per org** — an organization has exactly one plan at a time.

**New features spread automatically.** Because features are read through the plan, adding a feature to a plan instantly gives it to **every org on that plan** — no per-org updates. For example, adding "AI Analytics" to the Pro plan means every organization on Pro now has it, automatically. Removing a feature works the same way in reverse.


## (Optional) Can products, users, and orders be created at the org level, or only inside a store?

Yes — but only as a **UI convenience**, not a change to where the data lives. The data still always belongs to a store. When an org-level user creates a product (or a user, etc.) from an org-wide screen, the UI asks them to pick **which store or stores** to save it into, and then writes a copy into each chosen store.

So an org user can bulk-update many stores at once without logging into each one. Under the hood there's no org-level copy of the data — every record is still owned by a store, exactly as before. The org screen is just an easy way to fan one action out across several stores.


## (Optional) Can one store search another store's products, read-only?

Yes, if the user is allowed to. It uses the same two-step approach as the org-wide owner view: find the sibling stores in the same org, then read their products. It only ever **reads** other stores — it never changes them, and each store still fully owns its own products.

Two things keep it safe:

- The user must have a cross-store read permission (such as `product:read_cross_store`).
- The search is read-only by design — it can look at other stores but never edit them.

If it ever gets slow, the same pre-built read copy mentioned in the owner-view answer can speed it up.
