# Overview

> A user is just a login. *Where* they can act comes from the **memberships** they hold; *what* they
> can do there comes from the **roles** on those memberships.

```
user  ──<  membership  ──<  membership_assignment  >──  role  ──<  role_permission  >──  permission
```

- **user** — an identity of a person

- **membership** — a membership gives access to a user at either the `ORGANIZATION` or a `STORE` — never both. A user is one kind or the other.
    - If the user has a `ORGANIZATION` membership, they are allowed to have an `ORGANIZATION` role, these are more powerful roles, and give the user access to all stores in the `ORGANIZATION` and also the `ORGANIZATION` itself.
      - An **organization** membership leaves `store_id` NULL and sets `scope` to `ORGANIZATION`.
    - If the user has a `STORE` membership, they are allowed to have an `STORE` role, these are roles that give the user access only to the `STORE` the membership is a part of.
      - A **store** membership sets `store_id` and sets `scope` to `STORE`.

- **membership_assignment** — an assignment attaches a role on the user's membership.

- **role** — a bundle of permissions. Each role has a `scope` (`STORE` or `ORGANIZATION`), the main difference is that an `ORGANIZATION` role can have permissions that have the flag `is_elevated` to true. Assigning a scope to a role also makes it easy to filter which roles can be assigned to `STORE` or `ORGANIZATION` memberships. For example, you can not assign a role with scope `ORGANIZATION` to a membership of type `STORE` and vice versa. This will prevent a `STORE` user from ever accidentally getting an `ORGANIZATION` role.

- **permission** — this is an action, like `product:read`. It carries an `is_elevated` flag, which lets users know that this permission can ONLY be set on a role with the scope of `Organization`.

---

## Local dev test account

For exercising `POST /auth/login` against a local database, `tools/GekkoSuite.Database/Seed/dev_seed.sql`
inserts one organization + one active user (outside `Migrations/` on purpose — it's a throwaway fixture,
not a real migration; see the script's header comment). Local-only fake data, never a real secret:

```
email:    dev@gekkosuite.local
password: DevPassword123!
```

---

# FAQ

## How is a user created and given access?

A person's login and what they can do are two separate things. Creating a user just makes the login —
they can do nothing until you give them a place and a role there.

1. **Create the user**
2. **Give them a membership** 
3. **Give them a role**


## How do I revoke or suspend someone's access?

You can remove access by how permanent you want it — without ever deleting the person.

- If you want to remove a user's specific role at a `STORE` or `ORGANIZATION`, just delete the `membership_assignment`.
- If you want to deactive the user temporarily at a specific `STORE` or `ORGANIZATION`, but NOT delete any of there existing memberships, set the flag `membership.is_active = false`. 
- If you want to deactive the user temporarily from everywhere, but NOT delete any of there existing memberships,  set `user.is_active = false`
-  If you want to remove the user completely, can soft delete the user, this will set the flag `is_deleted = true` and log the timestamp `deleted_at`. Since it is a soft delete, the user will still remain in the database for record keeping as opposed to a hard delete where all data is deleted permenantly from the database. 


## What is the scope of membership's assigned role ? (`STORE` vs `ORGANIZATION`)

- A role on a **store** membership has access to ONLY that **one store**. It can not perform any action on another store.
- A role on the **organization** membership has access to the **entire org and every store in it**. A `user` with an `ORGANIZATION` membership can act on any store in the organization.

**Example**
- Store Manager at Seattle with `product:edit` → edits Seattle's products only.
- Org Admin with `product:edit` → edits any store's products.


## Who is the owner, and can their access be taken away?

The person who signs up and creates the organization is its **owner**. They have full access, and no
one else can take it from them.

The owner is marked on the organization itself — `organization.owner_user_id`, a column, not a
`membership_assignment` — so it can't be deleted out from under them. The only way ownership changes is
a deliberate **transfer**: the current owner hands it to someone who's already an org admin. No other
admin can revoke the owner's access or seize ownership. 


## How do we know everything a user can do, and where?

At login we gather every membership and the roles on each, and flatten it into one list: where they
can go, and what they can do there.

```
Maria's access
 place        kind    can do
 ----------   -----   -------------------------------------------
 Acme Inc     org     manage stores, manage users, suppliers, purchases   (org membership)
 Seattle      store   read products, sell                                 (store membership)
 Portland     store   read products, sell, refund                         (store membership)
```


## Showing the right roles in the UI

When an org admin attaches a role to a membership, the role picker should only offer roles that fit
that place. Because each role carries a `scope`, this is a direct filter — no guessing from a
role's permissions:

- Attaching to a **store** membership → list only **store roles** (`role.scope = STORE`).
- Attaching to an **organization** membership → list only **org roles** (`role.scope =
  `ORGANIZATION`).


---

# API Authentication and Authorization Flow

This section goes in-depth on the entire AUTH process that happens when an API request is made.

## 1. Authentication 

Authentication is the process of verifying the user is who they say they are. This is the first thing to verify, we do this by validating if the JWT token is legit and not tampered with. If it is not valid, then return **401** immediately to prevent a user from acting like someone else.

If the JWT token is valid then read from the database if  `user.is_active = true`. If the user was disabled then return **401** immediately.

Finally, if the user is authorized then create a request context object with user_id and organization_id from the JWT token. We only take these user details from the JWT token and never from anywhere else.

```
  context = {
    userId,            // from the token
    organizationId,    // from the token
  }
```

## 2. Authorization

Authorization decides if an authenticated user can perform the action they are requesting to do. For instance, if a store admin wants to create a product, he is authorized to do so, but a store reader can never create anything, so they are unauthorized to create a product. Both of them are legit authenticated user's in the organization, but they have different roles and access in the organization.

To verify if the user is authorized, we need following criticial pieces of information:

1. The target `STORE` or `ORGANIZATION` the user is requesting to perform the action on, this will allow us to verify if they have a valid membership and role at this place.
  - If the user is requesting to perform an action on a store, then we need the `store_id`, each API endpoint that acts on a store must have `{storeId}` in the path variable. For example: `POST /stores/{storeId}/products`.
  - If the user is requesting to perform an action on a organization, then we need to get the `organizationId` from the request context from Step 1, since this is coming from the JWT token, we can verify that the user actually belongs to this `organizationId`. For exmaple `POST /organization/purchases`,  get the `organizationId` from `request.context.organizationId` 

2. The required permission the user must have to call this API endpoint, this will allow us to verify if we can execute  the logic in this endpoint. For example if the logic in the endpoint is to delete a product, then we must check if the user has a role with the permission `product:delete`. 

3. The **`requiredMembershipScope`** the membership (and its role) must have to call this API endpoint. Each endpoint **explicitly declares** it — `STORE` or `ORGANIZATION` — rather than us inferring it from the URL. This tells us which kind of membership the user needs. For example, the delete-a-store endpoint declares `ORGANIZATION`, so we must check that the user has an `ORGANIZATION` membership.

4. The required **feature** (if any) the org's plan must include to call this API endpoint. Some endpoints back a *paid* capability — e.g. processing a return, or AI recommendations. Those endpoints **explicitly declare a `requiredFeature`** (e.g. `returns`) **and its `requiredFeatureScope`** (`STORE` or `ORGANIZATION`) — the scope is needed because the same feature code can exist at both scopes (they're separate features). We then check the org's plan includes that feature at that scope. Most endpoints (selling, reading) declare no feature and skip this check. This is a **billing gate, separate from the permission check** — the permission asks "may this user do it," the feature asks "does the org's plan include it." See `plans.md`.


### Process

Once we have the target the user is requesting to perform the action on, and the permission needed to use this endpoint. We need to now check the user's membership's and the role on each membership. This is what will let us decide if the  user is actually authorized. For example if the user is requesting to create a product at `store_abc`, then we need to verify he has a membership at `store_abc`, and a role with the permission `product:create`. 


If the endpoint declares a `STORE` scope (a store action), then the following rules must ALL be satisfied in order:

> A store endpoint only ever declares `STORE` scope, so its permission is never elevated (`is_elevated = false`) — elevated permissions live only on `ORGANIZATION`-scoped endpoints. If a store endpoint's permission is ever elevated, treat it as a misconfiguration and **deny**.

**1. Does the store in the request path `/stores/{storeId}/` belong in the `organizationId` that is in the request context built from the JWT token?**

- If not, then return **404** to let the user know this store does not exist in his organization. He can not act on it.

**2. Does the user have an `ORGANIZATION` scope membership in this `organizationId`, with an unexpired role that grants the permission (and the role's `scope` is `ORGANIZATION`, matching the membership; if a mismatched role somehow exists, ignore it)?**

- An `ORGANIZATION` membership reaches every store in the org, so it can perform this store action.
- If yes, the user is allowed — proceed to check 4.
- If no, proceed to check 3.

**3. Does the user have a `STORE` scope membership on the requested store (the `{storeId}` in the path), with an unexpired role that grants the permission (and the role's `scope` is `STORE`, matching the membership; if a mismatched role somehow exists, ignore it)?**

- If yes, the user is allowed — proceed to check 4.
- If no, return **403** — the user has neither an `ORGANIZATION` membership that reaches this store, nor a valid `STORE` membership on it with the permission.

**4. If the endpoint declares a required feature, do the org's effective features include it at the declared `requiredFeatureScope`?**

- The org's **effective features** are the **union** of the features of every subscription whose `status` is `ACTIVE` **or** `TRIALING` (a trial's features work; a `CANCELED` one contributes nothing) — so a base plan plus an active add-on gives the org both offerings' features.
- If the endpoint declares **no** feature → skip this; the action isn't feature-gated (e.g. selling, reading). User is authorized.
- If the effective features **include** it (matching `requiredFeature` + `requiredFeatureScope`) → user is authorized.
- If they **do not** → return **402 Payment Required** — the user is allowed, but no active offering unlocks this. (Distinct from `403` so the client can prompt them to turn it on.)

**5. Is the org's billing in good standing (the billing gate)?**

- Payment is **org-level** — the org pays one itemized bill for all its subscriptions — so this reads a single column, `organization.billing_status`, not the per-subscription statuses.
- `organization.billing_status` is `ACTIVE`, or `PAST_DUE` (still in the grace period) → allowed. (A free trial isn't a billing state — it owes nothing, so a trialing org is simply `ACTIVE` here; its trial lives on the subscription rows.)
- The org is overdue (`billing_status` is `UNPAID` / `CANCELED`) → **reads are still allowed** (they can see and export their data), but **writes return 402 Payment Required**. Billing endpoints stay open so they can pay and recover.


If the endpoint declares an `ORGANIZATION` scope (an org action), then the following rules must ALL be satisfied in order:

**1. Does the user have an `ORGANIZATION` scope membership in the `organizationId` that is in the request context built from the JWT token, with an unexpired role that grants the permission (and the role's `scope` is `ORGANIZATION`, matching the membership; if a mismatched role somehow exists, ignore it)?**

- If yes, the user is allowed — proceed to check 2.
- If no, return **403** — only users with an `ORGANIZATION` membership and a role granting the permission can perform organization actions.

**2. If the endpoint declares a required feature, do the org's effective features include it at the declared `requiredFeatureScope`?**

- The org's effective features are the **union** of the offering features of every subscription whose `status` is `ACTIVE` or `TRIALING`.
- If the endpoint declares **no** feature → skip this; the action isn't feature-gated. User is authorized.
- If the effective features **include** it (matching `requiredFeature` + `requiredFeatureScope`) → user is authorized.
- If they **do not** → return **402 Payment Required** — the user is allowed, but no active offering unlocks this. (Distinct from `403` so the client can prompt them to turn it on.)

**3. Is the org's billing in good standing (the billing gate)?**

- `organization.billing_status` is `ACTIVE` / `PAST_DUE` (grace) → allowed. (A trialing org is `ACTIVE` here — the trial is a per-subscription state, not a billing one.)
- Overdue (`UNPAID` / `CANCELED`) → **reads still allowed**, but **writes return 402 Payment Required**. Billing endpoints stay open so they can pay and recover.

> These billing checks (feature, billing status) run **after** the permission checks on purpose: *who you are* (authorization) is the hard boundary, checked first; *what your offerings cover and whether you've paid* is only relevant once you're already allowed. So an unauthorized user gets `403` and learns nothing about the org's offerings or billing, while someone who's allowed but missing a feature or overdue gets `402`. Permissions come from the user's role; features come from the org's live subscriptions and billing status from `organization.billing_status` (see `plans.md`).


### Query

A user can have many memberships. So we go through each one and ask: "does this membership let the user do the action?" If any single membership passes, the user is authorized. If none do, they are denied.

The endpoint gives us these to check against: its `requiredMembershipScope` (`STORE` or `ORGANIZATION`), its `requiredPermission`, and — for paid capabilities only — a `requiredFeature` plus its `requiredFeatureScope`.

First we decide if the **user** is allowed (membership + role + permission). If they are, and the endpoint declares a feature, we then check the **org's offerings** include it.

```
allowed = false

for each membership M the user holds:
    role = the role on M for this request

    -- 1. is this membership usable?
    if M is not live (removed / suspended / role expired):   continue
    if role.scope != M.scope:                                continue   -- ignore bad data
    if M.organization_id != context.organizationId:          continue   -- must be the user's org

    -- 2. does M reach what the endpoint targets?
    if endpoint.requiredMembershipScope == 'STORE':
        reached = ( M.scope == 'ORGANIZATION' )                              -- org reaches every store
               or ( M.scope == 'STORE' and M.store_id == {storeId in path} ) -- or the store itself
    else:  -- ORGANIZATION action
        reached = ( M.scope == 'ORGANIZATION' )

    -- 3. does its role grant the permission?
    if reached and role has requiredPermission:
        allowed = true; break

if not allowed:
    return 403                        -- the user isn't allowed

-- 4. feature gate: is this a paid capability covered by the org's effective features?
--    effective features = union of offering features across the org's ACTIVE/TRIALING subscriptions
if endpoint.requiredFeature is set:
    if org's effective features do NOT include (endpoint.requiredFeature, endpoint.requiredFeatureScope):
        return 402                    -- allowed, but no active offering unlocks it

-- 5. billing gate: is the org paid up? (org-level, one bill; reads always allowed; writes blocked when overdue)
if request is a write and organization.billing_status IN ('UNPAID', 'CANCELED'):
    return 402                        -- allowed, but the account is overdue

return 200                            -- allowed, an offering covers it, and billing is in good standing
```


<!-- @TODO: -->
## Working with Shared Resources (Products / Customers)

Some data is shared across the whole org. **Customers** are always shared — a customer created at one store is recognized at every store. **Products** are per-store by default, and only shared when the org turns on the `allow_share_products` setting. Either way, creating one is still just a normal store action; sharing only changes what gets *written* to the database, not how we authorize it.

A few things make this safe:

**The permission is a normal store permission.** `customer:create` and `product:create` are not elevated, so they sit in a normal store role — a cashier can hold them. Authorizing the create is the ordinary check: does the user have a role at this store that grants the permission? There's no special "is this shared?" branch.

**The org and store are set by us, not the caller.** The new record's `organization_id` comes from the token, and its `store_id` comes from the `{storeId}` in the path (already checked by authorization). The caller never sends these, so a user can only ever create within their own org and their own store.

**The database keeps the two tables scoped correctly.** The store-level row (`store_customer` / `store_product`) is store-scoped, so a store only sees its own. The shared row (`customer` / `product`) is org-scoped, so every store in the org can resolve it. Neither can leak across orgs (see `database.md`).

When we create the record, we write the store-level row and, when it's shared, the shared row too — in the same transaction, with the store row linking up to the shared one:

```
create customer on /stores/{storeId}/customers:   -- customers are always shared
  INSERT customer (shared), then INSERT store_customer linked to it        -- one transaction

create product on /stores/{storeId}/products:
  allow_share_products OFF → INSERT store_product only
  allow_share_products ON  → INSERT product (shared), then INSERT store_product linked to it  -- one transaction
```

> **Open risk — editing and deleting a shared record isn't specified yet.** The above only covers
> *creating*. Once a record is shared org-wide, a store editing it changes what every store sees, and
> deleting it could remove a record another store is still using. We still need to decide: can any store
> with the permission edit a shared record, or only the store that created it? Can a shared record be
> deleted while another store references it? **Decide this before shipping sharing** (also flagged in the
> Security Review).


## Safety nets

Everything above works only if every endpoint and query remembers to apply it. But developers forget — a new route might ship without its permission check, or a query might be written without its tenant filter. So we don't rely on memory. We add two system-wide safety nets so the *default* outcome is "denied", and a forgotten check fails closed instead of leaking data.

### 1. Deny-by-default routing

Every endpoint must declare the permission it requires. If a route is added but forgets to declare one, it should be blocked automatically rather than left open.

In ASP.NET Core we do this with a global `FallbackPolicy` set to `RequireAuthenticatedUser`. Any endpoint that has no authorization attribute falls back to this policy and is denied by default. This way, "forgot to add auth" results in a locked door, not an open one.

### 2. Row-Level Security (RLS)

Even if a query forgets its `store_id` / `organization_id` filter, the database itself refuses to return rows from another tenant. On each request, after authentication, we set the verified place onto the database session (`app.current_org`, and `app.current_store` for store actions), and a policy on every table filters to it automatically. So a query can only ever see the current place's rows.

One thing to get right: **scope RLS at both levels.** Org-owned tables filter on the org (`organization_id = app.current_org`). But store-owned tables need to filter on the *store* too (`store_id = app.current_store`), not just the org. The reason is the same as above — two stores in one org share an `organizationId`, so an org-only policy still lets a Store A request see Store B's rows. Filtering on the store closes that gap at the database, so even if a query forgets its `store_id`, the database still won't hand back another store's data.

RLS is a strong backstop, but it only protects you if it is deployed exactly right — and it fails **silently** if it isn't (queries still return data, so nothing looks broken in testing). Two conditions must hold:

- **The app connects as a non-superuser, non-owner role.** Postgres superusers and table owners *bypass* RLS entirely. Run migrations and admin tasks as a separate privileged role, and have the request path use a restricted role.
- **The tenant is set with `SET LOCAL` (transaction-scoped).** This resets at the end of each transaction, so a pooled connection can't carry one request's tenant into the next request (the classic connection-pooling leak).


---

# Further additional security

There are scenarios where even if the user is authenticated and authorized, invalid operations and actions can be performed, the following section describes key areas where to add proper guardrails.

- Do **not** allow a user to have a membership in a STORE and an ORGANIZATION — only one. That way a STORE user can never access ORGANIZATION data. Enforce this on the write path when a membership is added: refuse it if the user already holds a membership of the other scope. (Allowing both is a possible future setting — see `post-mvp.md`.)  

- Do **not** put a user's roles, permissions, or flattened access list in the JWT: a token that carries
its own permissions can't be revoked, so a suspended or demoted user keeps their old access until the
token expires.

- When an action names a resource by its id (read, update, or delete), we must make sure that resource actually belongs to the place in the request — otherwise a user could pass someone else's id and act on it. The safe way is to bake the place right into the query, instead of fetching the row first and checking after. So for a store action we query `WHERE order_id = :orderId AND store_id = :storeIdFromPath`, and for an org action we add `AND organization_id = context.organizationId`. If the id belongs somewhere else, it simply won't match and we return a 404.

  This matters most **between two stores in the same org**. Say Maria works at Store A and calls `GET /stores/{StoreA}/orders/{orderId}` but passes an `orderId` that belongs to Store B. She *is* allowed at Store A, so the check passes — but the order isn't hers. Both stores share the same `organizationId`, so an org-only filter can't tell them apart. Adding `AND store_id = StoreA` to the query is what stops her from seeing Store B's order.

- When inserting a new row, the `organization_id` is always taken from the request context object built in the Authentication step when validating the JWT token. This will prevent us from ever allowing a user to save data in a different `organization_id`. 

- When assigning permissions to a role with the scope `STORE`, denying any permissions with `is_elevated = true`. This will prevent a store membership from every having elevated access.

- When assigning a role to a membership, check if the scope of the role matches the scope of the membership. For example a role with scope `STORE` can only exist on a membership with scope `STORE`. This will prevent from ever assigning a role with scope `ORGANIZATION` to a membership with scope `STORE`.

- When inserting a user's membership, never allow to create a membership in another organization. This will prevent a user gaining membership to other organizations.

- When assigning a role to a user, only allow granting permissions the assigner **already holds** — a user can never grant more than they have (the escalation-ceiling / subset rule). This is the real fix for privilege escalation: it blocks an admin handing their own account a bigger role (self-escalation), granting a bigger role to *another* user, and two admins boosting each other (A grants B, B grants A)
---

# Security Review (Red Team / Blue Team)

An adversarial pass over this design. For each attack (🔴 red team), the defensive fix (🔵 blue team).

**Framing:** no system is "unbreakable" — security is depth, not perfection. The goal isn't a perfect
wall; it's that **no single failure is uncaught** by a second layer. This design already thinks that way
(deny-by-default, RLS floor, read-time backstops). The authorization engine is genuinely strong — every
serious break below is in **authentication and the token** (the front door), not the RBAC chain.


## Critical

**R1 — Forge a token, become any tenant.** The entire tenant wall is `context.organizationId`, taken from
the JWT. If an attacker can mint a token, they set `organizationId` to any org and walk in. Ways in:
`alg: none` accepted, RS256→HS256 algorithm confusion (sign with the public key), a weak or leaked
signing secret, or missing `exp`/`iss`/`aud` checks (replay an old or foreign token).
→ **Blue team:** §1 says "validate the JWT" but not *how*. Require explicitly: a pinned algorithm (reject
`none`, no RS/HS ambiguity), a verified signature with a strong **rotated** key, and `exp` + `iss` + `aud`
all checked. This is the most load-bearing paragraph in the doc and is currently one line.

**R2 — Crack the password store.** No hashing is specified; a DB dump then cracks every account, feeding
every other attack.
→ **Blue team:** store passwords with a slow salted KDF (argon2id/bcrypt). Rename the `password` column
to `password_hash` so plaintext is never a temptation.


## High

**R3 — Steal a token, use it after the victim is fired.** A stolen JWT (XSS, logs, a proxy) keeps working
until `exp`, even after the user is disabled — defeating `user.is_active` and the membership kill switches
for the token's lifetime.
→ **Blue team:** short-lived access tokens + refresh, or a revocation deny-list (jti). Store the token
**httpOnly** (not JS-readable) to cut XSS theft. State the TTL and storage policy in §1.

**R4 — Brute-force the register PIN.** `store_pin` is a short secret with no hashing, no lockout, no
rate-limit. If it authorizes anything, it's a side door around the whole JWT/RBAC design.
→ **Blue team:** decide what the PIN *is*. If it's an auth boundary: hash it (same KDF as passwords),
rate-limit + lock out per membership, and scope it to the narrowest action. If it's only a UX
re-confirm on an already-authenticated session, say so — and ensure it can't stand in for real auth.

**R5 — Credential-stuff the login.** Many low-value cashier accounts + no lockout = spray leaked password
lists until one hits, then pivot to escalation.
→ **Blue team:** login rate-limiting, account lockout/backoff, breached-password rejection, and **MFA for
org admins and the owner** (the accounts that can drain the company).

**R6 — Escalate via a second account.** A compromised low-value account that somehow holds `role:assign`
(or an assign path that doesn't check the *assigner's* own power) creates account B and grants it more
than the attacker has. Two compromised admins can also boost each other (A→B, B→A).
→ **Blue team:** the **subset rule** in *Further additional security* closes this. Verify two things: it's
checked **server-side, in the same transaction** as the write, comparing *effective* permission sets (not
role names); and `role:assign` / `user:create` are themselves **elevated**, so a store role can never hold
them.


## Medium

**R7 — Cross-store IDOR inside one org.** A Store-A user passes a Store-B resource id; authorization says
"you're at Store A" ✅ but the record is B's. Refunds/edits leak across stores if the by-id query lacks
`AND store_id`.
→ **Blue team:** already defended in two layers — `store_id` in every by-id `WHERE`, plus store-level RLS
on `app.current_store`. Residual risk is per-endpoint discipline; route all by-id reads through a shared
repository that *always* injects the place filter, so a raw query can't skip it.

**R8 — Mass-assignment / tenant-stamp bypass.** On create, the attacker POSTs
`"organization_id": <another org>` hoping the server saves the body verbatim.
→ **Blue team:** stamp the tenant from the token, never the body — and make it structural: the request DTO
has no `organization_id`/`store_id` field to bind, plus an RLS `WITH CHECK` on insert. Never
`db.Save(request)` a raw body onto an entity.

**R9 — RLS silently off.** Not an attacker action, but a misconfig that hands over everything: if the app
connects as the table **owner/superuser**, RLS is bypassed and every forgotten tenant filter becomes a
live cross-tenant leak — and it looks normal in testing.
→ **Blue team:** the doc calls out the non-owner role + `SET LOCAL`. Add a **startup self-test** that
queries as a fake tenant and asserts zero rows, so a broken RLS deploy fails loudly. Verify migrations run
as a *different* privileged role than the request path.

**R10 — Editing/deleting shared records is undefined.** A store editing a shared `customer`/`product`
changes what every store sees; deleting one another store references could break it. Create is specified;
edit/delete aren't.
→ **Blue team:** decide the rule (any store with the permission, or only the creating store; block delete
while referenced) before shipping sharing.

**R11 — Last-admin lockout / owner takeover.** Can an admin remove the org's last admin (self-inflicted
DoS) or strip the owner?
→ **Blue team:** the owner-as-column protects the owner. Add a guard that refuses to remove/deactivate the
last effective admin.


## Low

**R12 — Account enumeration.** Login errors that distinguish "no such user" from "wrong password" let an
attacker enumerate valid accounts.
→ **Blue team:** uniform "invalid credentials" for both, constant-time compare, and don't leak which org
an email belongs to. (Extends the 404-not-403 philosophy to the login.)

**R13 — No audit trail.** After an escalation or leak there's no record of who granted what, created whom,
or flipped a setting — so the breach is invisible and its blast radius unknowable.
→ **Blue team:** append-only audit log for security events — role grant/revoke, user create/delete, owner
transfer, membership changes, PIN changes, login failures/lockouts. Detection is a control, not an extra.


## What's already strong (keep it)

- Tenant boundary from the token, never client input.
- Declared-scope routing (not URL-inferred) — less spoofable; makes elevated-permission containment structural.
- Per-membership "any badge passes" evaluation with read-time backstops (`role.scope == membership.scope`).
- Deny-by-default `FallbackPolicy` — forgotten auth = locked door.
- Two-level RLS (org *and* store) — closes the same-org cross-store gap at the database.
- Live role reads (no permissions cached in the JWT) — immediate permission revocation.
- Subset rule for grants — the complete escalation ceiling.
- Elevated-permission containment — even a misplaced elevated permission can't escalate.


## Fix order

| # | Fix | Why first |
|---|---|---|
| 1 | Pin JWT validation (alg, signature, exp/iss/aud, rotated key) | Total compromise; one paragraph closes it |
| 2 | Password hashing (argon2id/bcrypt) + `password_hash` | DB dump = every account |
| 3 | Token lifecycle (short TTL + refresh or deny-list; httpOnly) | Stolen/stale tokens defeat all kill switches |
| 4 | `store_pin` spec (hash, lockout, scope) or declare it non-auth | Silent side door |
| 5 | Login rate-limit + lockout + MFA for admins | Realistic entry point |
| 6 | Verify subset rule + elevated flags on `role:assign`/`user:create` | Caps a single-account compromise |
| 7 | RLS startup self-test + non-owner role verification | Silent-fail floor |
| 8 | Audit log | Detection & forensics |


## Scorecard

| Area | Grade | Note |
|---|---|---|
| Authorization model | A | declared scope, per-membership checks, elevated-permission containment |
| Tenant isolation (cross-org + cross-store) | A− | token-sourced boundary + two-level RLS; rests on solid JWT validation |
| Privilege-escalation ceiling | B+ | subset rule specified; verify it's enforced server-side in-transaction |
| Authentication (login / token / PIN) | D | JWT validation, hashing, token lifecycle, PIN, brute-force all open |
| Auditing / detection | D | none |

**Bottom line:** the authorization engine is excellent — hard to break by design. But a fortress with a
strong vault and a flimsy front door is breached at the door, and every Critical/High finding is
**authentication**: JWT validation, password hashing, token lifecycle, the PIN, and brute-force. "Unbreakable"
isn't the target — **"no single failure is uncaught"** is, and you're close. Close items 1–5, add the audit
log so an attack is *visible*, and this design earns its A.


