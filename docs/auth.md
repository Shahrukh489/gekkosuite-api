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

This is the same family as Kubernetes (`ClusterRole` vs namespaced `Role`) and Azure (a role applied at a scope) — generic permissions, with the *level* set separately. We differ in one way, on purpose: those systems set the scope at **assignment** time (the same role can be attached at any level), whereas we fix the scope **on the role** itself — a role is born `STORE` or `ORG` and can only be granted at that kind of place. We don't need their flexibility: we have just two fixed levels (store and org) and no deeper nesting, so baking the level into the role is simpler and matches how the roles are actually used ("Cashier" is inherently a store role).

**Some permissions are org-only.** Things like `store:create` or `user:create` only make sense org-wide. Each permission carries a **`scope`** that says which role scopes may hold it:

- `scope = STORE` — may go in **store roles and org roles** (e.g. `order:sell`, `customer:add`).
- `scope = ORGANIZATION` — may go in **org roles only** (e.g. `store:create`, `user:create`).

This is an **eligibility guard**, not the level the permission operates at: a store role can never be given an `ORGANIZATION`-scoped permission. For our managed (shipped) roles we already honor this; it becomes load-bearing once **custom** roles let an org admin pick permissions — the picker only offers `STORE`-scoped permissions for a store-scoped role, and the write path rejects an `ORGANIZATION` permission in a store role.

Permissions are **explicit, never wildcards** — a role lists exactly the permissions it has. We don't grant `*` / "everything," so a new permission added later reaches nobody until it's deliberately added to a role.

Because users point at a role and the role points at its permissions (nobody keeps their own copy), changing a role's permissions takes effect immediately for everyone who has that role.



## How do users get roles at a store or the organization?

Roles attach to a **membership** — "give role R to this user at this place" — by adding a `membership_assignment` on the user's membership. A user can hold several roles at a place, or none, and an assignment can optionally **expire** (`expires_at`) for temp/seasonal staff. **Only an org admin assigns roles** (store users don't assign).


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

## Who is the root admin and owner is root?

The person who signs up and onboards the organization is the org's **root** user — they always have full org access, and it can't be stripped from them. They have the most powerful role: Organization Admin. Only they can give access
of this role to other users, and no other user can strip it from them.

If someone else wants access to be root organization admin the current organization owner must update the organization settings to make them the new primary Organization admin.


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




# API Authentication and Authorization Flow

This is the authentication and authorization flow each request must go through to verify if a user has permission to make the API request.

### 1. JWT Token Validation Middleware — Authenticatio? 

This middleware only validates the JWT token is valid and not expired or tampered. It is the first check, if the token is invalid then nothing about it can be trusted and we should not proceed further, the user is NOT authenticated. return 401.

**Validation Process**:
      - **Signature is valid** (signed by us, not tampered).
      - **Not expired.**

### 2. Tenancy Validation Middleware?

This middleware is to validate the user has access to the target organization or store he is requesting to perform an action. In order to get the target organization or store, one and ONLY one of the following headers must be present, if both are present then return 403 immediately:

- **`X-Target-Store-Id`** — a store target.
- **`X-Target-Organization-Id`** — the org target.

Whats imporant to note here is that we are not validating if the user has actual authorization to perform the requested action on the target organization or store. We are only checking if the target organization is the one the user is in, or if the target store is the one in the users organization. This is a early guardrail to prevent a user wanting to perform an action on a target in a different organization. Now if a user does have access to the target, we dont check here if he can actually do the action on the target, for example: create product. That is something each api endpoint must validate if user has the proper role and permission to create a product.

**Validation Process**:

- If the header X-Target-Organization-Id is present, the UUID organization_id value of the header must be the same organization_id as in the users JWT token, a user can not act on another organization. 
  - **`jwt_token.organization_id = X-Target-Organization-Id`**

- If the header X-Target-Store-Id is present, the UUID store_id value of the header must be in the organization in the users JWT token, a user can not act on a store in a different organization. 
      1. Organization_Stores = Get all stores in this user's organization -> `Select from store where organization_id = jwt_token.organization_id`
      2. Verify the X-Target-Store-Id  exists in the list from step 1m `X-Target-Store-Id in Organization_Stores`

After validating, the middleware stamps the resolved, **validated** values onto a request context that every downstream handler reads — handlers never re-read the raw headers (a header is an untrusted *claim*; the context is a *verified fact*):

```
context = {
  userId,                 // from the token
  organizationId,         // from the token (the boundary)
  targetTenantType,       // ORG | STORE  (which header was present + the route)
  tenantId                // the validated org or store id
}
```

```
request →
  M1 authn:    JWT signature valid + not expired? → no → 401
  M2 tenancy:  
               validate target is inside the org:
                X-Target-Organization-Id ==  token.organization_id ? → no → 403
                store(X-Target-Store-Id).organization_id ==  token.organization_id ?  → no → 403
               stamp context = { userId, organizationId, targetTenantType, tenantId }
  → endpoint (authorization happens here — see below)
```

### 3. API Endpoint Validation - Authorization.

Once we have confirmed the user is authenticated (step 1) and that the target organization or store is inside the user's organization (step 2), the endpoint does the final check: **is this user authorized to perform this specific action on this target?** Each endpoint declares the one permission it needs, and we verify the user holds it at the target.

- API: `POST /products`, Permission: `product:create`
- API: `POST /refunds`, Permission: `order:refund`

The target was already resolved in step 2 into the context (`context.targetTenantType` = ORG | STORE, `context.tenantId` = the validated id), so every endpoint runs the same shared check — the rules never differ per endpoint.

**Authorization Process** (in order):

**1. Owner short-circuit (root).** If the user is the organization's owner (`organization.owner_user_id`), allow immediately — the owner is root in their own org and skips the rest. (Loaded fresh from the DB using the validated `organizationId`, never from the token.)

**2. Membership + permission check.** Find a **live** role the user holds that reaches the target tenant and contains the required permission. The target is explicit, so this is one uniform question — "does the user have a live membership *here* with a role that contains the permission?" — plus the blast-radius rule:

- **Direct** — a membership **at the target tenant** (the org if `targetTenantType = ORG`, or that store if `STORE`).
- **Inherited** — if the target is a **STORE**, an **org** membership also reaches it (an org role applies to every store in the org). If the target is the **ORG**, only an org membership counts — a store-only user can never reach an org action.

One query down the chain `membership → membership_assignment → role → role_permission → permission`, filtered to live grants:

```
EXISTS a row where:
    membership.user_id = context.userId
    AND (
          -- direct: a membership at the exact target tenant
          (context.targetTenantType = STORE AND membership.store_id        = context.tenantId)
       OR (context.targetTenantType = ORG   AND membership.organization_id = context.tenantId)
          -- inherited: an org membership reaches a STORE target (blast radius)
       OR (context.targetTenantType = STORE AND membership.organization_id = context.organizationId)
        )
    AND membership.deleted_at IS NULL AND membership.is_active = true   -- live membership
    AND (membership_assignment.expires_at IS NULL
         OR membership_assignment.expires_at > now())                  -- unexpired assignment
    AND the role contains the required permission
        (role → role_permission → permission = requiredPermission)
```
No matching row → **403 Deny** (deny by default).

**3. Conditions (ABAC gate — stretch goal).** If the matched role-permission has conditions (limits like "refund up to $500"), evaluate each against the action's target object:

```
for each condition (most-specific: store row else org row):
    if NOT ( target[field]  operator  value )  →  403 Deny     -- e.g. order.amount <= 500
```
No conditions → skip (pure RBAC). A referenced field missing at runtime → **fail closed** (deny).

**4. Allow.** All gates passed — the action proceeds.

```
endpoint authz (required permission declared by the route):
  1. owner?      context.userId == organization.owner_user_id            → ALLOW
  2. membership: a LIVE membership at the target (context.tenantId) (direct),
                 OR an org membership if targetTenantType = STORE (inherited),
                 whose role contains the required permission?            → none → 403
  3. conditions: all conditions on that role-permission pass vs target?  → fail → 403
  4.                                                                     → ALLOW
```

**The things that must be right:**
- **Inherited org access for STORE targets only** — an org admin/owner often has *no* store membership; their org role reaches the store. But an **ORG target requires an org membership** — a store-only user can't reach org actions. Get this asymmetry right.
- **Filter live grants** (`deleted_at IS NULL`, `is_active`, unexpired) — a removed, suspended, or expired grant must never count. Keep this in one shared resolver so no endpoint forgets it.
- **IDOR on the target object** — if the action names a resource by id (e.g. the order being refunded), load it and confirm *its* `store_id`/`organization_id` matches `context.tenantId` before acting. You don't refund another store's order even with `order:refund`. (See Security gaps below.)





# Security gaps & hardening (must-do before launch)

The flow above (authn → tenancy → authz) is structurally sound, but a sound flow is not a secure
system — each layer must assume the one before it can fail. "Cannot be hacked" is not a real goal;
**defense in depth** is. These are the gaps that are genuinely exploitable as written, worst first.

## Critical — exploitable today

- [ ] **IDOR on the target object — the #1 multi-tenant breach.** Authz proves "can do
      `order:refund` *in this store*", but NOT that *this specific order* belongs to this store.
      Attack: a Store-A cashier with refund rights sends `{ order_id: <a Store-B order> }` — token
      valid, tenancy passes, authz passes, and they refunded another store's order. **Every
      endpoint that takes a resource id must load it and verify its `store_id`/`organization_id`
      matches `context.tenantId` — co-equal with the permission check, not a side note.** Return
      **404** (not 403) for resources outside the caller's tenant (403 confirms it exists).


- [ ] **Harden JWT verification — this is where auth systems actually die.** "Valid signature +
      not expired" must explicitly include:
      - Reject **`alg: none`** and pin the **expected algorithm** (block RS256→HS256 confusion).
      - Strong, rotated signing key; a leaked key = every token forgeable.
      - **Real revocation / short-lived tokens + refresh.** Without it, a fired/suspended user's
        token works until expiry — so `is_active = false` / `deleted_at` do nothing until then.
        For a money app this is mandatory, not optional.

## High

- [ ] **Authz decision and the write must be atomic (TOCTOU).** Check-then-write can go stale.
      For money ops, do the authorization check and the write (and the balance deplete) **in one
      transaction with row locking** — or two concurrent refunds both pass and both deplete.
- [ ] **Live-grant filter in ONE resolver.** The `deleted_at IS NULL AND is_active AND not
      expired` filter is load-bearing; the exploit is a developer forgetting it on one query.
      Enforce it in a single access-resolution function/view — never copy-pasted WHERE clauses.
- [ ] **Rate limiting + account lockout** on login/token endpoints — without it, credential
      stuffing and brute force are open.

## Medium

- [x] **Org-level action by a store-only user — RESOLVED by the explicit-target design.** The
      target tenant is named explicitly in `X-Tenant-Id` (org or store), and authz requires a
      membership *at that exact tenant*: an **org** target matches **only** org memberships, so a
      store-only user can't reach an org action regardless of headers. (A store target also allows
      org members, via inheritance.) Keep the asymmetry exactly as documented in the authz step.
- [ ] **Don't trust anything security-relevant from the token beyond identity + org.** Roles and
      permissions are resolved fresh per request (we do this — keep it; never cache them in the
      token, or revocation/role changes won't take effect).
- [ ] **Tenant-scoped error responses** — 404 (not 403) for cross-tenant resources, so errors
      don't leak existence.

## Non-negotiable summary
1. IDOR check on every resource id (load → verify in `context` → 404 if not).
2. Hardened JWT (pin alg, reject `none`, rotate key, real revocation / short tokens).
3. Owner short-circuit: DB-loaded by validated org, fail-closed.
4. Authz + write atomic; balance deplete under a row lock.
5. Live-grant filter in one resolver, never copy-pasted.
6. Rate limiting + lockout on auth endpoints.