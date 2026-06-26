Author: Salman Hoosein
Version : 1.0
Project:
Description: 

# Features
- Organzation can have many stores
- Stores have plans

# Target Niche 
We serve small and mid-size businesses running a multi-store POS. A typical organization has up to ~100 stores. At this scale we keep the design simple with a monolith application and single database

# Constraints
- Organization must have minimum one store to sell products
    - Create one by default on onboarding

# FAQ

## What is the user login flow?

## How to create a user in a store but not in the organization level?


## How do we ensure a store admin can never assign an organization admin roles?

Every role carries a `role_level` (`STORE` or `ORGANIZATION`) — the level it's meant for, and the only level it can be granted at. Two layers keep a store admin from handing out an org role:

1. **The UI hides them.** A store's "assign role" screen only lists `role_level = STORE` roles, so org roles never even appear as an option.

2. **The write path rejects them — this is the real guard.** Hiding a role in the UI isn't security; someone could still call the API directly. So when a role is granted, the server checks the role's `role_level` against the place before writing the `membership_role` row:

   - granting at a **store** → the role must be `role_level = STORE`
   - granting at an **organization** → the role must be `role_level = ORGANIZATION`

   If they don't match, the request is rejected — no row is written. So a store admin calling the grant endpoint with an org role's id gets turned away, regardless of what the UI showed.

```
grant request: give role R to membership M

  M is a STORE membership, R.role_level = STORE          -> allowed
  M is a STORE membership, R.role_level = ORGANIZATION    -> REJECTED
  M is an ORG membership,  R.role_level = ORGANIZATION     -> allowed
  M is an ORG membership,  R.role_level = STORE            -> REJECTED
```

The rule is simple: **the role's level must equal the membership's place type.** The UI filter is the convenience; the write-path check is the enforcement.



## How does each store keep its own products separate from other stores?

Every product belongs to exactly one store. We record this with a `store_id` on every product row, and a store only ever loads products with its own `store_id`. So one store can never see or change another store's products.

If two stores happen to sell the same item with the same SKU, each store still gets its own separate row — with its own price and stock:

```
product
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
Access has two layers, like an IAM user:

- A **membership** record says a user *belongs to one place* — for example, "John is at Store B." It names the place with one of two fields: `store_id` for a store, or `organization_id` for the organization. Exactly one is filled in — that tells us both the place and whether it's a store or the org. A membership carries no role on its own.
- A **membership-role** record grants a *role on a membership* — "John, at Store B, is a Manager." A membership can have several roles, or none.

Keeping these separate means a user can belong to a place with **no roles** (still listed there, just no access), and removing a role simply deletes that one membership-role record — the membership, the user's other roles, and the account all stay.

A person can have several memberships — one per place they work — so they can be a Cashier at one store and a Manager at another. When the user logs in, we gather all their memberships and the roles on each, and produce one simple list of where they can go and what they can do at each place:

```
John's access
 place    type    name             can do
 ------   -----   --------------   ----------------------------------
 org_1    ORG     Acme Inc         organization:read
 StoreA   STORE   Acme Seattle     store:read, store:sell
 StoreB   STORE   Acme Portland    store:read, store:sell, store:refund
```

Because each record points at a real store or a real organization (a proper database link), a record can never refer to a place that doesn't exist, and deleting a place automatically removes its access records.


## How do we verify which stores a user can act on, given his org role?

A user can act on a store two ways, and we never copy memberships into each store — we resolve access at check time:

1. **Direct** — the user has a **store membership** at that store, with a role granting the permission.
2. **Inherited** — the user has an **organization membership** whose role contains the `store:*` permission. An org role reaches **every store under that org**, so the permission applies to each of them without a per-store membership.

To check *"can this user do `store:edit` on Store 47?"* the server asks two things:

```
ALLOW if:
  (A) a store membership at Store 47 has a role containing store:edit
   OR
  (B) an org membership at Store 47's org has a role containing store:edit
      (the org role reaches down to every store in that org)
```

Branch **(B)** is the inheritance: we take the user's org memberships, expand each to all stores in that org (`store.organization_id = the org`), and apply the org role's `store:*` permissions to them. So an OrgAdmin with `store:edit` can edit any of the org's stores — including stores created *after* the role was granted — with no extra setup.

To list **every store a user can act on**: take the stores from his direct store memberships, plus all stores under any org he has an org membership in (filtered to org roles that actually carry a `store:*` permission). The direct set and the inherited set are combined; a store can appear in both, which is fine — access is the union.

This is the same federated pattern as the org-owner view: the org's reach is computed by joining the org grant to the org's stores on demand, not by storing a copy per store.


## How does an organization or store create its own custom roles?

Besides the built-in roles we ship (like Root Admin and Cashier), customers can create their own roles. A custom role can belong to a whole **organization** (every store in the org can use it) or to a single **store** (only that store uses it). It is visible only to its owner — no other org or store sees it.

All roles live in one `Role` table — built-in and custom, org-level and store-level together. A few fields describe each role, and two of them mean different things:

- **`is_managed`** — *who owns it.* `true` means we built and maintain it; `false` means a customer created it.
- **`role_level`** — *what level it's for.* Either `STORE` or `ORGANIZATION`. This is also the **only level the role can be granted at**: a `STORE` role can only go to a store user, an `ORGANIZATION` role only to an org user.
- **`organization_id`** / **`store_id`** — the owner, set only on *custom* roles. A built-in role has neither (we own it); a custom role has exactly the one that matches its level.
- **`created_user_id`** — the user who created it (empty for built-in roles we ship).

`is_managed` and `role_level` are independent: a role we ship can be either a store role or an org role. For example a built-in "Cashier" is `is_managed = true, role_level = STORE`, while a built-in "Org Owner" is `is_managed = true, role_level = ORGANIZATION`.

```
Role
 name           is_managed  role_level    organization_id  store_id   meaning
 ------------   ----------  ------------  ---------------  --------   --------------------------------
 OrgOwner       true        ORGANIZATION  (empty)          (empty)    built-in, org-level
 Cashier        true        STORE         (empty)          (empty)    built-in, store-level
 NightManager   false       ORGANIZATION  org_1            (empty)    custom, used across all of org_1
 WeekendOpener  false       STORE         (empty)          StoreA     custom, used only at Store A
```

To make a custom role, the owner creates a `Role` row (`is_managed = false`) with a `role_level` and the matching owner field (`organization_id` for an org role, `store_id` for a store role), then chooses its permissions. Names only need to be unique within the owner, so two different orgs (or stores) can each have a "Manager" role without clashing.

**Role level decides where the role is granted — which sets its blast radius.** `role_level` is not "which permissions are allowed in the role." It's where the role attaches, and that determines how far its permissions reach:

- A **STORE** role is granted at one store; its permissions reach **only that store**.
- An **ORGANIZATION** role is granted at the org; its `store:*` permissions reach **every store in that org**, and its `organization:*` permissions act on the org itself.

So the *same* permission has a different reach depending on the role's level. `store:edit` in a store role edits that one store; `store:edit` in an org role edits any store in the org. This is the whole point of an org role — it's a store power with org-wide reach.

**Which permissions a role may hold:**

- A **STORE** role may hold **only `store:*`** permissions (a store role has no business holding `organization:*`).
- An **ORGANIZATION** role may hold **both `store:*` and `organization:*`** — `store:*` to act on the org's stores, `organization:*` to act on the org itself.

Permissions are explicit, not inherited by wildcard: an org role that should manage stores must actually list `store:edit`, `store:read`, etc. If an org role does **not** contain a given `store:*` permission, it **cannot** do that store operation — there's no implicit "org role can do everything to stores." You grant exactly the store powers you intend.

**Why this is safe.** Two write-path checks:

1. **Building a role** — a `STORE` role's permissions must all be `store:*` (reject `organization:*` in a store role). An `ORGANIZATION` role may hold either.
2. **Granting a role** — the role's level must equal the place it's granted at (a store user can't be given an org role, and vice versa).

Together, a store admin can never gain org-level power: an `organization:*` permission can't live in a store role, and an org role can't be granted at a store. The UI also hides mismatched options for convenience, but the write-path checks are the real enforcement.


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


## How to ensure a store admin does not assign a organization level role to a user?

## How does a store get the features in its plan?

Billing is **per store**, so each **store** is on one **plan** (like "Pro"), and each plan includes a set of **features** (like reports or multi-store). The store gets its features *through its plan*. The organization itself has no plan — it's just the container; the bill is the sum of its stores' plans, and different stores can be on different plans.

```
Store → Plan → Features

StoreA is on the "Pro" plan, StoreB is on "Basic"
Pro includes:   multi_store, reports, returns
Basic includes: returns
So StoreA has multi_store, reports, returns — StoreB has only returns
```

To check a feature ("can StoreA use reports?"), we look at whether its plan includes that feature.

**One plan per store** — a store has exactly one plan at a time.

**New features spread automatically.** Because features are read through the plan, adding a feature to a plan instantly gives it to **every store on that plan** — no per-store updates. For example, adding "AI Analytics" to the Pro plan means every store on Pro now has it, automatically. Removing a feature works the same way in reverse.


## (Optional) Can products, users, and orders be created at the org level, or only inside a store?

Yes — but only as a **UI convenience**, not a change to where the data lives. The data still always belongs to a store. When an org-level user creates a product (or a user, etc.) from an org-wide screen, the UI asks them to pick **which store or stores** to save it into, and then writes a copy into each chosen store.

So an org user can bulk-update many stores at once without logging into each one. Under the hood there's no org-level copy of the data — every record is still owned by a store, exactly as before. The org screen is just an easy way to fan one action out across several stores.


## (Optional) Can one store search another store's products, read-only?

Yes, if the user is allowed to. It uses the same two-step approach as the org-wide owner view: find the sibling stores in the same org, then read their products. It only ever **reads** other stores — it never changes them, and each store still fully owns its own products.

Two things keep it safe:

- The user must have a cross-store read permission (such as `product:read_cross_store`).
- The search is read-only by design — it can look at other stores but never edit them.

If it ever gets slow, the same pre-built read copy mentioned in the owner-view answer can speed it up.
