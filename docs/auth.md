# Auth (authentication & authorization)

How people sign in, how they get access to stores and the organization, and how we decide what they're allowed to do where.


## The model in one picture

Access is a chain of small pieces. Read it left to right:

```
user  ──<  membership  ──<  membership_role  >──  role  ──<  role_permission  >──  permission
(login)    (belongs at        (a role granted     (bundle of    (the role's          (resource:
           one place:          on that             perms +       permissions)         action,
           a store or          membership)         a scope:                           scope-free)
           the org)                                STORE/ORG)
```

- **user** — one login per person (global identity).
- **membership** — the user belongs at a place (a store, or the org).
- **membership_role** — a role given to that membership.
- **role** — a named bundle of permissions we ship.
- **permission** — one allowed action, like `product:read` (scope-free; the role's `scope` sets the level).

So: a user belongs *somewhere* (membership), is given *roles* there (membership_role → role), and each role is a set of *permissions*. The rest of this doc explains each link.


## How does a user sign in? (authentication)

A user signs in with their credentials against the single global `user` record (one login per person). On success they get a session/token the app uses for the rest of their requests.

Authentication only proves *who* they are — it grants no access by itself. Everything they can actually do is decided afterward by **authorization** (memberships, roles, permissions), covered below. A freshly authenticated user with no memberships is logged in but can't do anything until they're given access.


## How is a user created and given access?

A user's **identity** and their **access** are separate: the `user` row is just the login (one per person, global to the system); what they can do comes entirely from memberships and roles. So a brand-new user exists but can do nothing until access is granted.

An **org admin** creates and sets up users — they need the `user:create` permission (in their org-scoped role). The flow is three steps:

1. **Create the user** — insert the `user` row (the login). It records `organization_id` (their home org), `created_by_user_id`, and `created_at`. Store-level users can't create accounts.
2. **Add a membership** — give the user a place: a **store membership** (`store_id`) or an **organization membership** (`organization_id`). This says *where* they belong. A membership has no role on its own.
3. **Attach a role** — add a `membership_role` on that membership. This says *what* they can do there.

A user can have several memberships (one per place they work) and several roles per membership, so they can be a Cashier at one store and a Manager at another.

The `user.organization_id` is the **home org**, set once and never changed — so even if every membership is later removed, you still know which org the account belongs to.


## How do I revoke or suspend a user's access?

There are three levels, depending on how permanent you want it:

- **Take away one role** — remove the `membership_role`. The user still belongs at the place, just with less (or no) access there.
- **Suspend at a place (temporary)** — set the membership's `is_active = false`. Access is switched off but the row stays, so you can flip it back on. Use this for "on leave" / "temporarily blocked."
- **Remove from a place (permanent)** — soft-delete the membership (`deleted_at`). Access is gone; the row is kept for history.

In all three the **`user` account itself remains** — you're changing access, not deleting the person. (`is_active` = reversible off-switch; `deleted_at` = removed-but-kept-for-history. Both stop access; the difference is intent.)


## What is a role, and where do roles come from?

A **role** is a named set of permissions, like "Cashier" or "Org Owner." Roles are built and maintained by us and shipped with the system. Customers assign these roles to their users; they don't author roles themselves.

A **permission** is a single allowed action, named **`resource:action`** (e.g. `product:read`, `order:refund`, `role:assign`):

- **`resource`** — what's acted on: `product`, `order`, `role`, `store`, `user`, etc.
- **`action`** — the verb: `read`, `create`, `refund`, `assign`, etc.

Permissions are **scope-free** — `product:edit` says nothing about org vs. store. The **level lives on the role**, not on the permission. Each role has a **`scope`** of `STORE` or `ORGANIZATION`:

- A **store role** (`scope = STORE`) — its permissions reach the one store it's granted at.
- An **org role** (`scope = ORGANIZATION`) — its permissions reach the **whole org and all its stores**.

So the *same* permission has different reach depending on the role that holds it. `product:edit` in a Cashier (store) role edits that one store's products; the same `product:edit` in an Org Admin (org) role edits any store's products in the org. This is the standard "role + scope" model (the same shape as Kubernetes `Role` vs `ClusterRole`, or Azure's role-at-a-scope).

**Org-only powers are just permissions we put only in org roles.** Things like `store:create` or `user:create` only make sense org-wide, so they appear only in org-scoped roles and never in store roles. There's no special marking on the permission — it's controlled by which role we ship it in.

Permissions are **explicit, never wildcards** — a role lists exactly the permissions it has. We don't grant `*` / "everything," so a new permission added later reaches nobody until it's deliberately added to a role.

Because users point at a role and the role points at its permissions (nobody keeps their own copy), changing a role's permissions takes effect immediately for everyone who has that role.


## How do users get roles at a store or the organization?

Roles attach to a **membership** — "give role R to this user at this place" — by adding a `membership_role` on the user's membership. A user can hold several roles at a place, or none. **Only an org admin assigns roles** (store users don't assign).


## How do we ensure a store user can never get organization access?

It's structural — there's no special guard to bypass:

- An **org role** (`scope = ORGANIZATION`) can only be granted on an **organization membership**; a **store role** (`scope = STORE`) only on a **store membership**. The role's scope must match the membership's place.
- **Only an org admin assigns roles**, and a user only has an organization membership if an org admin gave them one.
- So "can this user have an org role?" is answered by the `membership` table: **does the user have a row with `organization_id` set?** If not, there's no org membership to attach an org role to, and the UI never offers one. A pure store user has no organization membership → no org role → no org access.

In short: org access requires an org membership, only org admins create those, so a store-only user can never reach the org.


## How do permissions reach? (blast radius)

A role's **scope** decides how far its permissions reach:

- A **store role** reaches **only the one store** its membership is at.
- An **org role** reaches the **org itself and every store under it** — so its permissions apply at any of the org's stores.

So the *same* permission reaches differently depending on the role's scope. `order:refund` in a store role refunds at that one store; `order:refund` in an org role refunds at any store in the org. An org admin can act across all stores not because of special permissions, but because his role is org-scoped — org scope is a superset of store reach.

Permissions are explicit, never wildcards: a role lists exactly what it can do, and a new permission reaches nobody until it's added to a role.


## How do we know what a user can do, and where?

A user's access always has two parts: a **place** (a store, or the organization) and **what they can do there**. When the user logs in, we gather all their memberships and the roles on each, and produce one simple list of where they can go and what they can do at each place:

```
John's access
 place    type    name             can do
 ------   -----   --------------   ----------------------------------
 org_1    ORG     Acme Inc         store:create, user:create, product:edit (all stores)
 StoreA   STORE   Acme Seattle     product:read, order:sell
 StoreB   STORE   Acme Portland    product:read, order:sell, order:refund
```

Because each record points at a real store or a real organization (a proper database link), a record can never refer to a place that doesn't exist, and deleting a place automatically removes its access records.


# Security — TODO

The RBAC *model* above is sound, but a system is only as secure as its enforcement. The
following are not yet specified and must be designed before this can be called secure. Each
is a real requirement, not an optional nicety.

## Authentication (currently hand-waved — needs a real section)
- [ ] **Password storage** — hash with a slow, salted algorithm (argon2id or bcrypt); never
      plaintext/MD5/SHA1. Add a `password_hash` column to `user`.
- [ ] **Sessions / tokens** — choose server session vs. JWT; define expiry, rotation, and
      revocation; store tokens in httpOnly, secure cookies (not localStorage).
- [ ] **Brute-force protection** — rate limiting and account lockout on login.
- [ ] **MFA** — at least for org admins (they control users, roles, and funds).
- [ ] **Password reset** — secure, single-use, expiring tokens; treat as an attack surface.
- [ ] **Login auditing** — record success/failure, IP, timestamp.

## Enforcement (the core "is it secure" rule — state it explicitly)
- [ ] **Server-side authorization on every request.** All access checks happen on the
      backend, per request, for every action. The UI hiding options is **convenience, not
      security** — never trust the client. This is the single most important rule.
- [ ] **Deny by default.** No matching grant → denied. State it as the default everywhere.
- [ ] **Canonical check function** — one `can(user, permission, target)` that every endpoint
      calls, including the org→store inheritance (direct membership OR org membership whose
      role grants the permission, expanded over the org's stores). Don't reinvent per-endpoint.
- [ ] **Privileged actions are permission-gated** — "only an org admin can create users /
      assign roles" must be enforced by checking the actor's `user:create` / `role:assign`
      permission (in an org-scoped role) server-side, not a hardcoded role-name check.

## Tenant isolation (multi-tenant — prevents cross-org data leaks)
- [ ] **Derive the tenant from the session, never from client input.** Don't trust a
      client-supplied `organization_id` / `store_id`.
- [ ] **IDOR prevention** — for every resource accessed by id, verify it belongs to a place
      the caller has access to (e.g. requesting `store_id=999` must fail unless the caller can
      reach that store). Every query is scoped to the caller's org/store.

## Privilege escalation
- [ ] **Can't grant above yourself** — guard against an admin assigning a role (or creating a
      user) with more power than the actor holds; restrict who can create org owners/admins.

## Access-resolution invariants (load-bearing, easy to forget)
- [ ] Every access query filters `membership.deleted_at IS NULL` **and** `is_active = true`.
      Enforce this in **one** access-resolution function or DB view, not copy-pasted WHERE
      clauses — one forgotten filter re-grants a removed/suspended user.

## Audit (security-critical events)
- [ ] Role grants/revokes, user creation, permission changes, and balance top-ups must be
      audited (non-optional). Ties into the audit log TODO in `database.md`.

