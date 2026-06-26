# Auth (authentication & authorization)

How people sign in, how they get access to stores and the organization, and how we decide what they're allowed to do where.


## The model in one picture

Access is a chain of small pieces. Read it left to right:

```
user  ──<  membership  ──<  membership_role  >──  role  ──<  role_permission  >──  permission
(login)    (belongs at        (a role granted     (a named    (the role's          (subject:
           one place:          on that             bundle of   permissions)         resource:
           a store or          membership)         permissions)                     action)
           the org)
```

- **user** — one login per person (global identity).
- **membership** — the user belongs at a place (a store, or the org).
- **membership_role** — a role given to that membership.
- **role** — a named bundle of permissions we ship.
- **permission** — one allowed action, like `store:product:read`.

So: a user belongs *somewhere* (membership), is given *roles* there (membership_role → role), and each role is a set of *permissions*. The rest of this doc explains each link.


## How does a user sign in? (authentication)

A user signs in with their credentials against the single global `user` record (one login per person). On success they get a session/token the app uses for the rest of their requests.

Authentication only proves *who* they are — it grants no access by itself. Everything they can actually do is decided afterward by **authorization** (memberships, roles, permissions), covered below. A freshly authenticated user with no memberships is logged in but can't do anything until they're given access.


## What is a user, and how do they get access?

A user's **identity** and their **access** are separate things:

- The `user` table is the **global identity** for the whole system — one login per person. It is not owned by an org or a store; a user is a single row in this one table.
- **Access** is expressed only through `membership` rows. Belonging to a store is a store membership; belonging to the organization is an org membership.

A **membership** says a user *belongs to one place* — for example, "John is at Store B." It names the place with one of two fields: `store_id` for a store, or `organization_id` for the organization. Exactly one is filled in — that tells us both the place and whether it's a store or the org. A membership carries no role on its own.

A **membership-role** record grants a *role on a membership* — "John, at Store B, is a Manager." A membership can have several roles, or none. Keeping these separate means a user can belong to a place with **no roles** (still listed there, just no access).

A person can have several memberships — one per place they work — so they can have one role at one store and another at a different store.


## Who can create a user?

**Only an organization admin can create users.** A new user is created at the org level by someone with the org-admin role, who then grants the new user access to the stores (or the org) they should work at. Store-level users cannot create accounts.

When a user is created, the `user` row records provenance, set once and immutable:

- `organization_id` — their **home org**. Permanently answers "where did this user belong," regardless of later membership changes.
- `origin_store_id` — the store they were created at, if any.
- `created_by_user_id` / `created_at` — who made the account and when.

Because access lives in memberships (which can be removed) and identity lives on the `user` row, a user can have all memberships removed and still have a known origin — they never become an unexplainable orphan.

**Removing access keeps history.** Memberships are **soft-deleted** (`deleted_at` set, row retained), so you keep an audit trail of where a user used to have access. Every access query filters `deleted_at IS NULL`, so a removed membership grants nothing — but the record (and the user) remain for history.


## How do I revoke or suspend a user's access?

There are three levels, depending on how permanent you want it:

- **Take away one role** — remove the `membership_role`. The user still belongs at the place, just with less (or no) access there.
- **Suspend at a place (temporary)** — set the membership's `is_active = false`. Access is switched off but the row stays, so you can flip it back on. Use this for "on leave" / "temporarily blocked."
- **Remove from a place (permanent)** — soft-delete the membership (`deleted_at`). Access is gone; the row is kept for history.

In all three the **`user` account itself remains** — you're changing access, not deleting the person. (`is_active` = reversible off-switch; `deleted_at` = removed-but-kept-for-history. Both stop access; the difference is intent.)


## What is a role, and where do roles come from?

A **role** is a named set of permissions, like "Cashier" or "Org Owner." Roles are built and maintained by us and shipped with the system. Customers assign these roles to their users; they don't author roles themselves.

A **permission** is a single allowed action, named **`subject:resource:action`** (e.g. `store:product:read`, `store:order:refund`, `organization:role:assign`). The three parts are:

- **`subject`** — the scope: `store` or `organization`. This is also what the permission acts within.
- **`resource`** — what's acted on: `product`, `order`, `role`, etc.
- **`action`** — the verb: `read`, `create`, `refund`, `assign`, etc.

The `subject` tells you whether a permission is a store-level or org-level power — `store:*:*` permissions act on a store, `organization:*:*` permissions act on the org.

Because users point at a role and the role points at its permissions (nobody keeps their own copy), changing a role's permissions takes effect immediately for everyone who has that role.


## What roles do we ship?

We ship a fixed set of managed roles — customers pick from these, they don't make their own. The exact list will grow, but the shape is:

```
role         level         what it's for
----------   ------------  ----------------------------------------
Org Owner    organization  full control of the org and all its stores
Org Admin    organization  manage the org's users, stores, settings
Store Manager store         run a store: products, pricing, staff, purchasing
Cashier      store          sell and process returns at a store
```

Org-level roles are granted on an org membership; store-level roles on a store membership. ("Org admin" throughout this doc means a user holding an org-level role like Org Owner / Org Admin.)


## How do users get roles at a store or the organization?

Roles attach to a **membership** — "give role R to this user at this place" — by adding a `membership_role` on the user's membership. A user can hold several roles at a place, or none. **Only an org admin assigns roles** (store users don't assign).


## How do we ensure a store user can never get organization access?

It's structural — there's no special guard to bypass:

- Roles are **managed** (we build them), so a store user's roles only ever carry `store:*:*` permissions; we never ship a store role with org powers.
- **Only an org admin assigns roles**, and they assign a role *on a membership*. An **org role can only be put on an organization membership** — and a user only has an org membership if an org admin gave them one.
- So the question "can this user have an org role?" is answered by the `membership` table: **does the user have a row with `organization_id` set?** If not, the UI doesn't offer org roles and there's nowhere to attach one. A pure store user has no organization membership → no org role → no org access.

In short: org access requires an org membership, only org admins create those, so a store-only user can never reach the org.


## How do permissions reach? (blast radius)

Where a role is granted — the membership's place — determines how far its permissions reach:

- A role granted at **one store** has its permissions reach **only that store**.
- A role granted at the **org** has its `store:*:*` permissions reach **every store in that org**, and its `organization:*:*` permissions act on the org itself.

So the *same* permission has a different reach depending on where the role is granted. `store:product:edit` granted at a store edits that one store's products; granted at the org it edits any store's products in the org. This is the whole point of an org role — it's a store power with org-wide reach.

Permissions are explicit, not inherited by wildcard: an org role that should manage stores must actually list `store:product:edit`, `store:product:read`, etc. If an org role does **not** contain a given `store:*:*` permission, it **cannot** do that store operation.


## How do we know what a user can do, and where?

A user's access always has two parts: a **place** (a store, or the organization) and **what they can do there**. When the user logs in, we gather all their memberships and the roles on each, and produce one simple list of where they can go and what they can do at each place:

```
John's access
 place    type    name             can do
 ------   -----   --------------   ----------------------------------
 org_1    ORG     Acme Inc         organization:store:create, organization:user:manage
 StoreA   STORE   Acme Seattle     store:product:read, store:order:sell
 StoreB   STORE   Acme Portland    store:product:read, store:order:sell, store:order:refund
```

Because each record points at a real store or a real organization (a proper database link), a record can never refer to a place that doesn't exist, and deleting a place automatically removes its access records.


## How do we verify which stores a user can act on, given his org role?

A user can act on a store two ways, and we never copy memberships into each store — we resolve access at check time:

1. **Direct** — the user has a **store membership** at that store, with a role granting the permission.
2. **Inherited** — the user has an **organization membership** whose role contains the `store:*:*` permission. An org role reaches **every store under that org**, so the permission applies to each of them without a per-store membership.

To check *"can this user do `store:product:edit` on Store 47?"* the server asks two things:

```
ALLOW if:
  (A) a store membership at Store 47 has a role containing store:product:edit
   OR
  (B) an org membership at Store 47's org has a role containing store:product:edit
      (the org role reaches down to every store in that org)
```

Branch **(B)** is the inheritance: we take the user's org memberships, expand each to all stores in that org (`store.organization_id = the org`), and apply the org role's `store:*:*` permissions to them. So an OrgAdmin with `store:product:edit` can edit any of the org's stores' products — including stores created *after* the role was granted — with no extra setup.

To list **every store a user can act on**: take the stores from his direct store memberships, plus all stores under any org he has an org membership in (filtered to org roles that actually carry a `store:*:*` permission). The direct set and the inherited set are combined; a store can appear in both — access is the union.

This is the same federated pattern as the org-owner view: the org's reach is computed by joining the org grant to the org's stores on demand, not by storing a copy per store.
