# Auth (authentication & authorization)

How people sign in, how they get access to stores and the organization, and how we decide what they're allowed to do where.


## The model in one picture

Access is a chain of small pieces. Read it left to right:

```
user  ──<  membership  ──<  membership_assignment  >──  role  ──<  role_permission  >──  permission
(login)    (belongs at        (a role assigned          (bundle of   (the role's          (resource:
           one place:          on that membership,       perms +      permissions)         action,
           a store or          optionally expiring)      a scope:                          scope-free)
           the org)                                      STORE/ORG)
```

- **user** — one login per person (global identity).
- **membership** — the user belongs at a place (a store, or the org).
- **membership_assignment** — a role given to that membership (optionally with an expiry).
- **role** — a named bundle of permissions we ship.
- **permission** — one allowed action, like `product:read` (scope-free; the role's `scope` sets the level).

So: a user belongs *somewhere* (membership), is given *roles* there (membership_assignment → role), and each role is a set of *permissions*. The rest of this doc explains each link.


# FAQ

## How is a user created and given access?

A user's **identity** and their **access** are separate: the `user` row is just the login (one per person, global to the system); what they can do comes entirely from memberships and roles. So a brand-new user exists but can do nothing until access is granted.

An **org admin** creates and sets up users — they need the `user:create` permission (in their org-scoped role). The flow is three steps:

1. **Create the user** — insert the `user` row (the login). It records `organization_id` (their home org), `created_by_user_id`, and `created_at`. Store-level users can't create accounts.
2. **Add a membership** — give the user a place: a **store membership** (`store_id`) or an **organization membership** (`organization_id`). This says *where* they belong. A membership has no role on its own.
3. **Attach a role** — add a `membership_assignment` on that membership. This says *what* they can do there (and, optionally, until when via `expires_at`).

A user can have several memberships (one per place they work) and several roles per membership, so they can be a Cashier at one store and a Manager at another.

The `user.organization_id` is the **home org**, set once and never changed — so even if every membership is later removed, you still know which org the account belongs to.


## How do I revoke or suspend a user's access?

There are three levels, depending on how permanent you want it:

- **Take away one role** — remove the `membership_assignment`. The user still belongs at the place, just with less (or no) access there. (Or set `expires_at` to auto-end it at a chosen time.)
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

So the *same* permission has different reach depending on the role that holds it. `product:edit` in a Cashier (store) role edits that one store's products; the same `product:edit` in an Org Admin (org) role edits any store's products in the org.

This is the same idea as Kubernetes (`ClusterRole` vs `Role`) and Azure (a role applied at a scope): generic permissions, with the level kept separate. The one difference: those systems pick the level when the role is *assigned*, so one role can be used at any level. We bake the level into the **role** itself — a role is born `STORE` or `ORG`. We don't need their flexibility (we only have two levels and no nesting), and it matches how roles are actually used: a "Cashier" is always a store role.

**Some permissions are org-only.** Things like `store:create` or `user:create` only make sense org-wide. Each permission carries a **`scope`** that says which role scopes may hold it:

- `scope = STORE` — may go in **store roles and org roles** (e.g. `order:sell`, `customer:add`).
- `scope = ORGANIZATION` — may go in **org roles only** (e.g. `store:create`, `user:create`).

This just controls **which roles a permission is allowed in** — a store role can never hold an `ORGANIZATION` permission. Our shipped roles already follow this. It matters most later, when custom roles let an org admin pick permissions: the picker only offers `STORE` permissions for a store role, and the server rejects an `ORGANIZATION` permission added to one.

Permissions are **explicit, never wildcards** — a role lists exactly the permissions it has. We don't grant `*` / "everything," so a new permission added later reaches nobody until it's deliberately added to a role.

Because users point at a role and the role points at its permissions (nobody keeps their own copy), changing a role's permissions takes effect immediately for everyone who has that role.



## How do users get roles at a store or the organization?

Roles attach to a **membership** — "give role R to this user at this place" — by adding a `membership_assignment` on the user's membership. A user can hold several roles at a place, or none, and an assignment can optionally **expire** (`expires_at`) for temp/seasonal staff. **Only an org admin assigns roles** (store users don't assign).

**No privilege escalation via assignment.** An admin must not be able to grant more power than they hold. The rule: **you can only grant a role whose permissions are a subset of your own** — so you can't hand out (or self-assign) a role stronger than yours, and you can't bootstrap to full power by granting yourself `role:assign`/owner. This is checked at grant time, server-side, against your *effective* permissions. (Full design in `post-mvp.md`; it becomes load-bearing the moment role assignment is delegated or custom roles ship.)


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

## Who is the owner (root)?

The person who signs up and onboards the organization is the org's **owner** — recorded as `organization.owner_user_id`. The owner holds the most powerful role (Organization Admin) and their access **cannot be stripped by anyone else**.

For that "cannot be stripped" guarantee to be real, it must be **enforced**, not just stated. The chosen mechanism:

- The owner is identified by `organization.owner_user_id` (a column, not a grant).
- The server **refuses to delete or deactivate the owner's org-admin `membership_assignment`** while they are the `owner_user_id` — any such request is rejected. (The owner's power lives in being the owner; the assignment just makes it concrete and can't be removed out from under them.)
- The **only** way the owner changes is a deliberate **ownership transfer**: the current owner updates `organization.owner_user_id` to another user (who must already be an org admin). This is a privileged, audited action only the current owner can perform.

So no admin — not even another Organization Admin — can revoke the owner's access or seize ownership; only the owner can hand it over.


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


## How does login work, and how is it hardened?

A user logs in with credentials; on success we issue the JWT the rest of the flow trusts. **Issuing the token is where the most common breaches happen**, so this path must be hardened (most of these are server/endpoint concerns, not schema):

- **Password storage** — hash with a slow, salted algorithm (**argon2id** or **bcrypt**); never plaintext/MD5/SHA-1. Store in a `password_hash` column on `user`.
- **Rate limiting** — throttle login attempts per account and per IP, to blunt credential stuffing and brute force.
- **Account lockout / backoff** — temporary lockout or escalating delay after repeated failures.
- **MFA** — at least for **org admins and the owner** (they control users, roles, funds). Strongly recommended for everyone.
- **Password reset** — single-use, expiring, signed reset tokens; treat reset as its own attack surface (don't reveal whether an email exists).
- **Token lifetime** — keep access tokens short-lived with a refresh token, so revocation/suspension takes effect quickly (see M1's revocation note).
- **Login auditing** — record successes and failures (who, when, IP) for forensics.

These are the **authentication** controls; everything after login (M1–M3 below) is about *trusting and using* the token, not issuing it.


# API Authentication and Authorization Flow

This is the authentication and authorization flow each request must go through to verify if a user has permission to make the API request.

### 1. JWT Token Validation Middleware — Authentication

This middleware only validates that the JWT token is valid and not expired or tampered with. It is the first check: if the token is invalid then nothing about it can be trusted and we should not proceed further — the user is NOT authenticated. Return **401**.

**Validation Process**:
- **Signature is valid** (signed by us, not tampered).
- **Not expired.**
- **Pin the expected algorithm** and **reject `alg: none`** — never let the token choose its own algorithm (blocks the `none` bypass and RS256→HS256 confusion attacks).
- **Strong, rotated signing key** — a leaked key means every token is forgeable.
- **Real revocation / short-lived tokens + refresh** — without it, a fired or suspended user's token keeps working until it expires, so `is_active = false` / `deleted_at` have no effect until then. For a money app this is mandatory, not optional.


### 2. Tenancy Validation Middleware — Authorization (boundary)

This middleware validates that the **target** organization or store the user wants to act on is inside the user's own organization. To name the target, exactly **one** of the following headers must be present — if both (or neither) are present, return **400**:

- **`X-Target-Store-Id`** — a store target.
- **`X-Target-Organization-Id`** — the org target.

Important: this is **not** checking whether the user can perform the requested action (e.g. create a product) — only that the target belongs to their organization. It's an early guardrail against acting on a target in a *different* organization. Whether the user can actually do the action is checked per-endpoint in step 3.

**Validation Process**:

- If **`X-Target-Organization-Id`** is present, it must equal the `organization_id` in the user's JWT token — a user can never act on another organization.
  - `jwt_token.organization_id == X-Target-Organization-Id`  → else **403**
- If **`X-Target-Store-Id`** is present, the store must belong to the user's org (from the token) — a user can never act on a store in a different organization.
  - `store(X-Target-Store-Id).organization_id == jwt_token.organization_id`  → else **403** (or 404 if the store doesn't exist)

Once validated, the middleware saves these values on a request **context** that the rest of the code reads. Nothing downstream re-reads the raw headers — a header is something the client *claimed*; the context is something we *verified*.

```
context = {
  userId,                 // from the token
  organizationId,         // from the token (the boundary)
  targetTenantType,       // ORG | STORE  (which header was present + the route)
  targetTenantId          // the validated org or store id
}
```

```
request →
  M1 authn:    JWT signature valid + not expired?                        → no → 401
  M2 tenancy:  exactly one target header present?                        → no → 400
               validate target is inside the org:
                 X-Target-Organization-Id == token.organization_id?      → no → 403
                 store(X-Target-Store-Id).organization_id
                                          == token.organization_id?      → no → 403 (or 404 if missing)
               stamp context = { userId, organizationId, targetTenantType, targetTenantId }
  → endpoint (authorization happens — step 3 below)
```

### 3. Endpoint Authorization — can the user do this action here?

Once we have confirmed the user is authenticated (step 1) and that the target organization or store is inside the user's organization (step 2), the endpoint does the final check: **is this user authorized to perform this specific action on this target?** Each endpoint declares the one permission it needs, and we verify the user holds it at the target.

- API: `POST /products`, Permission: `product:create`
- API: `POST /refunds`, Permission: `order:refund`

The target was already resolved in step 2 into the context (`context.targetTenantType` = ORG | STORE, `context.targetTenantId` = the validated id), so every endpoint runs the same shared check — the rules never differ per endpoint.

**Authorization Process** (in order):

**1. Membership + permission check.** The question is simple: **does the user have access here, with the permission this action needs?** We answer it by looking for one valid grant for the user at the target.

A grant counts only if the user reaches the target:

- **Direct** — they're a member of the target itself (the org, or that store).
- **Inherited** — if the target is a **store**, being a member of the **org** also counts (an org role reaches every store). If the target is the **org**, only an org membership counts — a store user can't reach org actions.

A grant counts only if it's still alive — the membership isn't deleted or suspended, and the role assignment hasn't expired.

**One important rule:** all of this must be true on the **same grant**. It's not enough that the user has *some* live access and *separately* has *some* role with the permission — the live membership, the unexpired assignment, and the role with the permission must be one connected chain. (Otherwise: an expired Cashier role still carrying `order:refund`, plus a separate still-active Stocker role, could wrongly combine to allow a refund.)

In SQL, this means walking `membership → membership_assignment → role → role_permission → permission` as one joined row:

```
EXISTS one joined chain
   membership → membership_assignment → role → role_permission → permission
where:
    membership.user_id = context.userId

    -- the membership reaches the target
    AND (
          (context.targetTenantType = STORE AND membership.store_id        = context.targetTenantId)
       OR (context.targetTenantType = ORG   AND membership.organization_id = context.targetTenantId)
       OR (context.targetTenantType = STORE AND membership.organization_id = context.organizationId) -- org reaches its stores
        )

    -- the membership is alive (not deleted, not suspended)
    AND membership.deleted_at IS NULL AND membership.is_active = true

    -- this role assignment hasn't expired
    AND (membership_assignment.expires_at IS NULL OR membership_assignment.expires_at > now())

    -- safety: a store membership only counts store roles, an org membership only org roles
    AND (
          (membership.store_id        IS NOT NULL AND role.scope = 'STORE')
       OR (membership.organization_id IS NOT NULL AND role.scope = 'ORGANIZATION')
        )

    -- THIS role contains the required permission
    AND permission = requiredPermission
```
No matching row → **403 Deny** (deny by default).

**2. Resource ownership check.** If the action names a specific resource by id (the order to refund, the product to edit), **load that resource and verify it belongs to the target tenant** before acting:

```
load the resource by id
if resource.store_id / resource.organization_id != context.targetTenantId  →  404
```

Having `order:refund` at Store A does **not** let you refund a Store-B order. Without this check, a user could touch another store's data just by passing its id — the most common multi-tenant breach. Return **404** (not 403) for anything outside the tenant, so you don't reveal that it exists.

This is as important as the permission check — **every endpoint that takes a resource id must do it.**

```
endpoint authz (required permission declared by the route):
  1. membership: a LIVE membership at the target (context.targetTenantId) (direct),
                 OR an org membership if targetTenantType = STORE (inherited),
                 whose role contains the required permission?            → none → 403
  2. IDOR:       resource(id).tenant == context.targetTenantId?         → no   → 404
```



# Audit 

# Managed Roles and Permissions

- TODO
