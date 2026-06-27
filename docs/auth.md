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


## How does a user sign in? (authentication)

A user signs in with their credentials against the single global `user` record (one login per person). On success they get a session/token the app uses for the rest of their requests.

Authentication only proves *who* they are — it grants no access by itself. Everything they can actually do is decided afterward by **authorization** (memberships, roles, permissions), covered below. A freshly authenticated user with no memberships is logged in but can't do anything until they're given access.


## API auth flow (per request)

Every API request passes through two middleware layers before it reaches the endpoint. The endpoint then does the authorization (covered later).

### Middleware 1 — is the token valid? (authentication)

Verify the JWT:
- **Signature is valid** (signed by us, not tampered).
- **Not expired.**
- (Reject malformed tokens, and revoked sessions if we keep a revocation list.)

We do **not** re-check "does this user belong to this org" against the database — the signed token already binds `userId` to `organizationId` (we issued it), and `user.organization_id` is immutable, so re-querying would only distrust our own signature. The only DB-backed checks worth doing here are revocation / account-active status, not org membership.

Fail → **401 Unauthorized**. Pass → we trust the token's claims, most importantly the user's identity and their **`organization_id`** (their home org).

### Middleware 2 — is the tenancy valid? (which place is this request for)

Every request **explicitly names its target tenant** — the org or a store it acts on — via one of two headers:

- **`X-Target-Store-Id`** — a store target.
- **`X-Target-Organization-Id`** — the org target.

The `Target` prefix is deliberate: these name the **target** (what you're acting on), **never the trust boundary** (which is always the token's org). The header can never grant org scope or switch orgs — it can only point at a tenant inside the token's org.

**The route declares its tenant type — an extra check.** Routes are split by tenant type, and the route *requires* the matching header (the route knows what it operates on, so the server doesn't trust the client's choice blindly):

- **`/orgs/...`** routes (org settings, user management) → require **`X-Target-Organization-Id`**.
- **`/stores/...`** routes (sell, refund, inventory) → require **`X-Target-Store-Id`**.
- A request to a `/stores/...` route carrying an org header (or vice versa) → **400** (wrong target type for this route).
- A genuinely dual route that serves either level (rare) accepts either header.

This means the **route gives the tenant *type* for free** (no guessing, no parsing an ambiguous id), the header gives the **id**, and route↔header agreement is an extra layer — a store-scoped request can't slip into an org route.

**Then validate the target against the token's org** (the boundary check):

- **org target** → its id equals `token.organization_id`? → else **403**.
- **store target** → `store.organization_id == token.organization_id`? → else **403** (store missing or in another org).

**Stamp the resolved target on the request:** `req.context = { userId, organizationId (from token), tenant: { type: ORG|STORE, id } }`. The `tenant` is the validated target every downstream check uses; handlers never re-read headers.

```
request →
  M1 authn:    JWT signature valid + not expired?               → no → 401
  M2 tenancy:  organizationId = token.organization_id           (never from a header — the boundary)
               route requires its matching target header?       → missing/wrong-type → 400
               validate target is inside the org:
                 org target   → target.id == organizationId?               → no → 403
                 store target → store.organization_id == organizationId?   → no → 403
               req.context = { userId, organizationId, tenant: {type, id} }
  → endpoint (authorization happens here — see below)
```

Note: this layer only checks the target tenant is **inside the caller's org** (the boundary). Whether the user can actually *act* there — has a membership/role at that tenant — is the **authorization** step, not here. Two separate gates: tenancy = "is this tenant inside my org?", authz = "am I a member here with the permission?".

### Endpoint authorization — can this user do this action here?

The request now has a trusted `req.context = { userId, organizationId, storeId|null }`. The endpoint declares the one permission it needs and calls a single shared function — `authorize(context, requiredPermission, target?)` — that every endpoint uses (so the rules never differ per endpoint):

```
POST /refunds   →   authorize(context, "order:refund", order)
```

`authorize()` runs these steps in order:

**1. Owner short-circuit (root).** If the user owns this org, allow immediately — the owner is root in their own org and skips the rest.
```
if context.userId == organization.owner_user_id  →  ALLOW
```

**2. Check the user has a role at the target tenant with the permission.** Because the target is explicit (`context.tenant`), this is uniform — "does the user have a live membership *here* that contains the permission?" — with one addition for the blast-radius rule:

- **Direct** — a membership **at the target tenant** (the org if `tenant.type = ORG`, or that store if `STORE`).
- **Inherited** — if the target is a **store**, an **org** membership also reaches it (org roles apply to every store in the org). If the target is the **org**, only an org membership counts — a store user can never reach an org-level action (this is what closes the "org action without a store" gap: the org target requires an org membership).

One query down `membership → membership_assignment → role → role_permission → permission`, filtered to live grants:

```
EXISTS a row where:
    membership.user_id = context.userId
    AND (
          -- direct: a membership at the exact target tenant
          (context.tenant.type = STORE AND membership.store_id        = context.tenant.id)
       OR (context.tenant.type = ORG   AND membership.organization_id = context.tenant.id)
          -- inherited: an org membership reaches a STORE target (blast radius)
       OR (context.tenant.type = STORE AND membership.organization_id = context.organizationId)
        )
    AND membership.deleted_at IS NULL AND membership.is_active = true   -- live membership
    AND (membership_assignment.expires_at IS NULL
         OR membership_assignment.expires_at > now())                  -- unexpired assignment
    AND the role contains the required permission
        (role → role_permission → permission = requiredPermission)
```
No matching row → **403 Deny** (deny by default).

Note the asymmetry that closes gap #7: an **org** target matches **only** org memberships, so a store-only user can't perform an org action no matter what header they send; a **store** target matches the store's own members **plus** org members (inheritance), so org admins reach every store.

**3. Conditions (the ABAC gate — stretch goal).** If the matched role-permission has conditions, evaluate them against the `target`:
```
for each condition (most-specific: store row else org row):
    if NOT ( target[field]  operator  value )  →  403 Deny     -- e.g. order.amount <= 500
```
No conditions → skip (pure RBAC). Missing field at runtime → fail closed (deny).

**4. Allow.** All gates passed; the action proceeds.

```
authorize(context, requiredPermission, target?):
  1. owner?   context.userId == org.owner_user_id                       → ALLOW
  2. roles:   a LIVE role at context.tenant (direct)
              OR an org membership if tenant is a STORE (inherited)
              that contains requiredPermission?                         → none → 403
  3. ABAC:    all conditions on that role-permission pass vs target?    → fail → 403
  4.                                                                    → ALLOW
```

**The two things that must be right:**
- **Inherited org access for store targets only** — an org admin/owner often has *no* store membership; their org role reaches the store. But an **org target requires an org membership** — a store user can't reach org actions. Get this asymmetry right.
- **Filter live grants** (`deleted_at IS NULL`, `is_active`, unexpired) — a removed, suspended, or expired grant must never count.

(Related, separate concern: **IDOR on the `target`.** Before trusting the order being refunded, confirm *its* `store_id` is inside `context` — you don't act on another store's order. This is part of loading the target safely; see the tenant-isolation items in the Security TODO.)


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



## Audit (security-critical events)
- [ ] Role grants/revokes, user creation, permission changes, and balance top-ups must be
      audited (non-optional). Ties into the audit log TODO in `database.md`.

# Needs discussion

## Owner is root (can't be removed)

`organization.owner_user_id` is the org's **root** user — they always have full org access, and
it can't be stripped from them.

**Proposed model (Option B — root as identity, not a grant):** the owner's power comes from
*being* `owner_user_id`, not from a `membership_assignment` row. So `can()` short-circuits at the
top, before any role lookup:

```
can(user, action, target, place):
    org = place.organization
    if user.id == org.owner_user_id:      # ROOT short-circuit — owner of THIS org
        return ALLOW
    ... normal RBAC + ABAC ...
```

Why this is clean:
- **Can't be removed** — there's no grant to delete; the only way to change it is the deliberate
  "transfer ownership" action (changing `owner_user_id`).
- **Per-org** — compared against *the target org's* owner, so owning Org A gives no power in Org B.
- **Cheap** — one comparison against a column already reachable (store → `organization_id` → org).

Open questions:
- Does the owner still get a normal org membership/role row (for visibility in the users list),
  even though their access comes from the short-circuit? (Leaning yes — row for listing,
  short-circuit for power.)
- Who can change `owner_user_id` (transfer ownership), and is it audited? (Leaning: current owner
  only, audited.)
- Should a guard also block deleting the owner's membership/role row, or is the short-circuit
  enough on its own? (Short-circuit makes their access not depend on it, so this is cosmetic.)


## Conditions on a permission (limits like "refund up to $500") — stretch goal (maybe MVP)

A role's permission can carry **conditions** — limits checked at the moment the action happens. The classic case: a Cashier *may* refund, but only up to $500; a Manager up to $5000. Same permission (`order:refund`), different limit per role. This is lightweight ABAC layered on the RBAC, and it may be the thing that **replaces custom roles** (see `post-mvp.md`): customers tune limits on the roles we ship rather than authoring roles.

### Two tables: a menu we ship, and the customer's chosen values

- **`permission_condition`** — the **menu**. *We* define which conditions a permission may have. Each entry is a `field` + `operator` + a display `label`. Example entries for `order:refund`:

  ```
  permission_condition
   permission     field      operator  label
   ------------   --------   --------   --------------
   order:refund   amount     <=         Refund cap
   order:refund   age_days   <=         Days to refund
  ```

- **`role_permission_condition`** — the customer's **value**, pointing at a menu entry. Tenant-scoped (`organization_id`, optional `store_id`). The FK to `permission_condition` means a customer can only set a value for a condition we allow — they can't invent a `field` or `operator`.

  ```
  role_permission_condition
   org     store   role      → permission_condition   value
   ------   -----   -------    --------------------    -----
   Org 7   (all)   Cashier    order:refund / amount    500
  ```

  Reads as: "for Org 7's Cashiers, refunds are capped at $500." `store_id` lets one store override (most-specific-wins: store row beats org row).

### How the customer sets a limit

1. The editing screen reads `permission_condition` for `order:refund` → shows "Refund cap (0–1000)".
2. The admin picks the **role** (Cashier) and types **500**.
3. Insert `role_permission_condition (Org 7, Cashier, → that menu entry, value=500)`. The value must be within the menu's `min/max`.

### How it's enforced at runtime

The refund request carries the order, **not** a condition id — so we find conditions by the **action**, then check them:

```
cashier refunds a $600 order →
  1. RBAC:  does the user hold order:refund here?            → no → DENY
  2. find role_permission_condition for (role, org/store, order:refund)
            → none? unconditional → ALLOW
            → found: join permission_condition → field='amount', operator='<=', value=500
  3. ABAC:  evaluate dto[field] operator value  →  dto.amount(600) <= 500  → false → DENY
```

One generic evaluator: `dto[field]  <operator>  value`. **No per-condition code** — `field`/`operator` come from the menu, `value` from the customer's row.

**The field→value mapping is just the name.** `field = 'amount'` reads `dto.amount` — the condition's `field` is the same name as the DTO/entity property. So map the API request to a DTO/entity whose field names match the menu's `field` names, and the lookup is direct. **Fail closed**: if a condition's `field` has no matching DTO property at runtime, **deny** — never skip a limit.

### Who can edit limits — org admin only

**Only an org admin edits condition values** (gated by the `role_condition:edit` permission, which only org-level roles hold). They set a store's limits and scope each one via `role_permission_condition.store_id` (NULL = all the org's stores, set = that store).

A **Store Admin cannot edit limits** — if a store manager wants a higher cap for their employees, they ask the org admin. This keeps it simple and **structurally removes any self-escalation risk**: the only editors are org-level, always setting limits *down* to a store, never their own. (If store-admin self-service is ever wanted, it's an additive change — grant `role_condition:edit` to a store role and add per-permission bounds so they can only move within an owner-set ceiling — but we deliberately don't do that now.)

### Safety

- **No conditions = pure RBAC.** The ABAC gate is a no-op when there are no rows — today's behavior, unchanged.
- **We own the operator.** The customer only sets a value — a cap can't be flipped into a floor.
- **Only org admins edit values**, always scoped down to a store — so no one can raise their own limit.

# Auth Verification Flow


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
      is inside `req.context` — co-equal with the permission check, not a side note.** Return
      **404** (not 403) for resources outside the caller's tenant (403 confirms it exists).

- [ ] **Owner short-circuit must be airtight.** `userId == org.owner_user_id → ALLOW` bypasses
      *everything*. Therefore: (a) load `owner_user_id` from the DB using the **validated**
      `context.organizationId` — never an owner id from the token or client; (b) **fail closed** —
      if the org can't be loaded, deny, never fall through. This is the single most dangerous line
      in the system; a bug here is god-mode.

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