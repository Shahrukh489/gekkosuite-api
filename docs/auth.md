# Auth (authentication & authorization)

How people sign in, how they get access to stores and the organization, and how we decide what they're allowed to do where.


## Architecture 

```
user  ──<  membership  ──<  membership_assignment  >──  role  ──<  role_permission  >──  permission
```

The **type** (ORGANIZATION or STORE) runs through the chain: a user's type, the membership's place, and the role's type all line up — a store user gets store roles, an org user gets org roles. Permissions don't have a type; instead an elevated permission can only sit in an org role, while a normal one fits either.

- **user** — one login per person (organization identity), with a fixed `user_type`.
- **membership** — the user belongs at a place (a store, or the org), matching their type.
- **membership_assignment** — a role given to that membership (optionally with an expiry).
- **role** — a named bundle of permissions we ship, tagged ORGANIZATION or STORE.
- **permission** — one allowed action, like `product:read` 

So: a user belongs *somewhere* (membership), is given *roles* there (membership_assignment → role), and each role is a set of *permissions*. The rest of this doc explains each link.


# FAQ

## How is a user created and given access?

A person's login and what they can do are two separate things. Creating a user just makes the login — they can't do anything until you give them access to a place and a role there.

**How it works**
An org admin sets up a user in three steps:

1. **Create the user** — make their login, and decide its **type**: an *organization* user (runs the company, reaches every store) or a *store* user (an employee at one or more stores). This is fixed at creation.
2. **Give them a membership** — add them to a place that matches their type: a store user goes to store(s); an organization user goes to the organization itself.
3. **Give them a role** — choose what they can do at that place. The role must match the user's type too — store users get store roles, organization users get organization roles.

A store user can be added to several stores, with a different role at each. An organization user belongs to the organization once and reaches all of its stores from there.

**Example**
Maria is a *store* user, hired at the Seattle store as a Cashier, then later helps run the Portland store. She has one login, added to both stores — a Cashier role in Seattle and a Manager role in Portland.

**Good to know**
- Only an org admin can create users (it needs the `user:create` permission, which only the Org Admin role has).
- A user's **type** is decided at creation and can't be changed in this version — there's no "promote a store employee to run the company" yet (that's planned for after MVP). To switch someone, create them as a new user of the right type.
- Every user belongs to one **home organization**, set when they're created and never changed — so even if you later remove all their access, you still know which company they came from.

**Under the hood**
The login is the `user` row, carrying a `user_type` (`ORGANIZATION` or `STORE`). A place is a `membership` carrying its own `user_type` (the same `ORGANIZATION`/`STORE` lookup) and always an `organization_id` (the tenant boundary); a store membership also sets `store_id`, an org membership leaves it NULL. The membership's `user_type` must match the user's. A role at that place is a `membership_assignment`, which can optionally carry an `expires_at`. The role's own `user_type` must equal the user's.


## How do I revoke or suspend someone's access?

You can dial access down by how permanent you want it — without ever deleting the person. The account always stays.

**How it works**
Four levels, from narrowest to widest:

- **Take away one role** — they still belong at the place, just with less (or no) ability there.
- **Suspend at one place (temporary)** — switch their access off at a single store but keep everything, so you can switch it back on. Good for "moved off this store for now."
- **Disable the whole account (temporary)** — one switch turns the person off *everywhere* at once, across all their stores, without touching any individual place. Good for "on leave" or an immediate company-wide block.
- **Remove from a place (permanent)** — they no longer belong there. The record is kept for history.

**Example**
A cashier goes on leave → *disable their account* (off everywhere). They're just pulled from one store → *suspend at that place*. They quit → *remove* them from the store. You gave a cashier refund rights by mistake → just *take away that role*.

**Under the hood**
Take-away = delete the `membership_assignment` (or let `expires_at` end it). Suspend one place = `membership.is_active = false`. Disable the account = `user.is_active = false` (the kill switch above all memberships — effective access needs both `user.is_active` and the place's `membership.is_active`). Remove = soft-delete the membership (`deleted_at`). The `user` row itself is never deleted.


## What is a role, and where do roles come from?

A role is a named bundle of things a person is allowed to do (like "Cashier" or "Org Admin"). We build and ship the roles; customers assign them — they don't create their own (yet).

**How it works**
- A **permission** is one allowed action — e.g. read a product, refund an order, assign a role.
- A **role** is a set of permissions.
- Every role is either a **store role** or an **org role**. That's its level, and it sets how far the role reaches (see *How far does a role reach?* below).

**Good to know**
- Some actions only make sense organization-wide (like creating a store, or creating users) — those can only go in **org roles**, never store roles.
- We don't do "allow everything" — a role lists its permissions explicitly, so a new feature reaches nobody until it's deliberately added to a role.

**Under the hood**
Permissions are named `resource:action` (e.g. `product:read`, `order:refund`) and are scope-free in name. The org-vs-store level lives in a shared `user_type` lookup that `user` and `role` reference: the role carries a `user_type` (`STORE` or `ORGANIZATION`) — its level. A permission instead carries an `is_elevated` flag — a one-way gate: a non-elevated permission can go in store *or* org roles, while an elevated one (like `store:create`, `user:create`) can only go in org roles. So a store role can never hold an org-only power.


## What happens to existing users when we update a managed role?

The change applies to everyone with that role, immediately — there's nothing to re-assign.

**How it works**
Users don't carry their own copy of a role's permissions; their access is read fresh on every request from the role itself. So editing a role updates everyone who holds it at once.

**Good to know**
Custom roles (an org cloning a shipped role or building its own) are planned for after MVP.


## How do users get roles at a store or the organization?

You attach a role to a person *at a specific place* — "give Maria the Manager role at the Portland store."

**How it works**
An org admin assigns the role; it lands on the person's membership at that place. One person can hold several roles at a place, or none. A role can also be set to **expire** automatically — handy for temporary or seasonal staff.
A role has a scope that must match the user's type. A store user can never have an organization role, and a
organization user can never have a store role.

**Good to know**
Assigning roles needs the `role:assign` permission, which today only the Org Admin role has.

**Under the hood**
A grant is a `membership_assignment` (membership + role), optionally with `expires_at`.


## Can a store user ever gain organization-level access?

No — and it's not a rule that can be forgotten or bypassed; it's how the system is built.

**How it works**
An org role can only be attached to someone who belongs at the **organization**. A user only belongs at the organization if an org admin put them there by giving them a membership. So a store-only user has no organization membership, and hence can never do any organization level operations.

**Example**
A cashier who only belongs to Store A can never be given "Org Admin," because they're not a member of the organization — only of the store.


## How far does a role reach? (store vs org)

A store role acts on **one store**. An org role acts on the **whole organization and every store in it**.

**How it works**
The *same* permission reaches differently depending on the role that holds it. `order:refund` in a Cashier (store) role refunds at that one store; the same `order:refund` in an Org Admin (org) role refunds at *any* store in the company. An org admin can act everywhere not because of special permissions, but because their role is org-level — org reach simply includes all the stores.

**Example**
- Store Manager at Seattle with `product:edit` → can edit Seattle's products only.
- Org Admin with `product:edit` → can edit any store's products.

## Who is the owner, and can their access be taken away?

The person who signs up and creates the organization is its **owner**. They have full access, and **no one else can take it from them.**

**How it works**
The owner is marked on the organization itself, not given as an ordinary grant — so it can't be deleted out from under them. The system refuses any attempt to remove the owner's admin access while they're still the owner.

The only way ownership changes is a deliberate **transfer**: the current owner hands it to someone else (who must already be an org admin). No other admin can revoke the owner's access or seize ownership.

**Example**
A co-admin tries to remove the founder's access → the request is rejected. The founder later sells the business → they transfer ownership to the new owner, which is recorded and audited.

**Under the hood**
The owner is `organization.owner_user_id` (a column, not a `membership_assignment`). Transfer = updating that column; it's a privileged, audited action only the current owner can do.


## How do we know everything a user can do, and where?

At login we gather all the places a user belongs and the roles at each, and turn it into one simple list: where they can go, and what they can do there.

**Example**
```
Maria's access
 place        type    can do
 ----------   -----   -------------------------------------------
 Acme Inc     ORG     manage stores, manage users, suppliers, purchases
 Seattle      STORE   read products, sell
 Portland     STORE   read products, sell, refund
```

**Good to know**
Every entry points at a real store or organization, so it can never reference a place that doesn't exist — and deleting a place automatically clears the access tied to it.


# API Authentication and Authorization Flow

This is the authentication and authorization flow each request must go through to verify if a user has permission to make the API request.

The flow is two layers: **authenticate** (who are you?) then **authorize** (may you do this here?).

## 1. Authentication — validate the JWT, load identity, build context

A middleware validates the JWT and, on success, **reads the user row** to load the account facts we need for every request, then builds the request **context**. If the token is invalid, nothing about it can be trusted; stop here and return **401**.

**Validation Process**:
- **Signature is valid** (signed by us, not tampered).
- **Not expired.**
- **Pin the expected algorithm** and **reject `alg: none`** — never let the token choose its own algorithm (blocks the `none` bypass and RS256→HS256 confusion attacks).
- **Strong, rotated signing key** — a leaked key means every token is forgeable.
- **Real revocation / short-lived tokens + refresh** — without it, a fired or suspended user's token keeps working until it expires, so `is_active = false` / `deleted_at` have no effect until then. For a money app this is mandatory, not optional.
- TBD more checks like claim validation and other industry practices

After the token checks out, **read `user_type` and `is_active` from the `user` row** — one DB read per request, right here. These are deliberately read **fresh from the DB, never trusted from the token**: a user whose type or account status changed is judged on what's true *now*. They're stamped into the context so the authorization step reuses them (one read, not two).

The context holds only **verified identity** — who the user is, their org (their immutable home org, the tenant), and the freshly-read account facts. It carries **no target**: the thing being acted on comes from the route, not from a header the client set.

```
context = {
  userId,            // from the token
  organizationId,    // from the token — the tenant boundary, never client input
  userType,          // ORGANIZATION | STORE — read FRESH from the user row
  isActive           // account kill switch — read FRESH from the user row
}
```

## 2. Authorization — may this user do this action, here?

Each endpoint declares the one permission it needs, and store-scoped endpoints carry the store in their **path** (`/stores/{storeId}/...`). The organization is never in the path — it's the tenant, taken from the token. Org-level endpoints (e.g. `/organization/suppliers`) carry no place id; the org is implied.

Selling and everything a store owns (its products, customers, sales) are **store actions**. Procurement, suppliers, expenses, and managing stores/users are **org actions**.

- `POST /stores/{storeId}/products`   → permission `product:create` (store action — the store's own products)
- `POST /stores/{storeId}/refunds`    → permission `order:refund`    (store action)
- `POST /organization/purchases`      → permission `purchase:create` (org action — org buys inventory)


**Error convention:** anything outside the user's organization — a store in another org, or a resource not in the acted-on place — returns **404, never 403**, so existence isn't leaked. 403 is reserved for "this is yours, but you lack the permission."

**Early guards (defense in depth).** Before the main query, two cheap checks short-circuit using the facts already in context. Each is *also* guaranteed structurally — they're kept as explicit runtime checks anyway, so a single misconfiguration can't open a hole:

```
if context.isActive = false                          →  403   -- kill switch: off everywhere
if context.userType = STORE AND requiredPermission.is_elevated
                                                     →  403   -- store users can't do org-only actions
```

The second guard is the clean version of "a store user can't do an org action": org actions (procurement, suppliers, expenses, creating stores or users) require an **elevated** permission, and a store user can never hold one — so we reject it up front, by the permission's own `is_elevated` flag (declared on the endpoint), rather than by guessing "is this an org route." Structurally it's already impossible (a store role can't contain an elevated permission), but checking it explicitly is one more runtime guard.

`requiredPermission.is_elevated` is read off the permission the route already declares — `is_elevated` is a static property of each permission in the catalog, so this is a constant the endpoint exposes, **not an extra DB lookup**.

**The single authorization query.** Then one query answers the rest — does the user hold a live role with the required permission, reaching this place, **and is that place inside their org** — with the boundary baked in so it can never be skipped:

```
Does a row exist in:
  membership → membership_assignment → role → role_permission → permission

where:
    membership.user_id = context.userId
    AND permission     = requiredPermission        -- the role has the permission
    AND role.user_type = context.userType          -- role's type matches the user's (runtime re-check)

    -- account live AND membership live (not removed, not suspended) AND role not expired
    AND membership.deleted_at IS NULL
    AND membership.is_active  = true
    AND (membership_assignment.expires_at IS NULL OR membership_assignment.expires_at > now())

    -- the membership is at the place the user's type allows.
    -- Branch on membership.user_type, NOT on which id is null: organization_id is populated on EVERY
    -- membership (it's the tenant boundary), so "org_id is set" no longer distinguishes org from store.
    AND ( context.userType = ORGANIZATION  →  membership.user_type       = 'ORGANIZATION'
                                              AND membership.organization_id = context.organizationId
          context.userType = STORE         →  membership.user_type       = 'STORE'
                                              AND membership.store_id        = {storeId from the path}
                                              -- the store must be inside the caller's org: the tenant
                                              -- boundary. Because the membership now carries org_id,
                                              -- this is a direct column compare — no join to `store`.
                                              AND membership.organization_id = context.organizationId )
```
No matching row → **403 Deny** (deny by default). A `storeId` in another org produces no row (the membership's `organization_id` won't match the token's), so a foreign store is **denied by the same query** — there is no separate boundary step to forget.

Why this is enough, per user type:
- **Store user** — the `membership.store_id = {storeId}` clause already requires they're a member of *that* store, and `membership.organization_id = token.org` requires that store be in their org. Both are columns on the one membership row — no extra join.
- **Organization user** — they reach every store in their org. We only need their org membership to hold the permission; the store they're acting on is theirs as long as it's in their org, which is exactly what the action's own query enforces next (see resource scoping).

The `role.user_type = context.userType` clause is the runtime version of the type↔role rule: a user only holds roles of their own type (enforced on the assignment write-path), but re-asserting it here means a store user can never authorize against an org role, and vice versa, even if a bad row slipped past the write-path guard.

It all has to come from **one** membership-role-permission chain, not a mix — an expired Cashier role that could refund doesn't lend its permission to a separate, still-active Stocker role that can't. Walking the chain as one joined row guarantees this: the live membership, the unexpired assignment, and the permission must all sit on the *same* row.

### Resource scoping (IDOR) — handled by the query, not a separate check

When an action names a specific resource by id (the order to refund, the product to edit), we **don't** load it and then compare its tenant. Instead, every such query is **scoped to the place in the path**, so a foreign resource is simply never found:

```
UPDATE / SELECT ... WHERE order_id = {orderId} AND store_id = {storeId}
  → no row → 404
```

An order belonging to another store doesn't match `store_id = {storeId}`, so it returns **404** with no special handling — the isolation is the `WHERE` clause, not a thing a developer has to remember to add after loading. For an **organization** action, the same idea scopes to the org (`... AND organization_id = context.organizationId`). This makes cross-store/cross-org access (the most common multi-tenant breach) structurally impossible: you can't fetch what your `WHERE` clause excludes.

This is as important as the permission check — **every endpoint that takes a resource id must do it.**

### Making it impossible to forget

The two protections above (the permission check and query scoping) only work if they're applied on *every* endpoint. Relying on developers to remember is how multi-tenant leaks happen. So we don't rely on memory — we make the system refuse to run without them, with two enforced rules:

**1. Deny-by-default routing — a route must declare its permission, or it's blocked.** Every endpoint declares the permission it requires (right on the route). A forgotten declaration should fail loudly and immediately (at deploy), never silently ship an open endpoint — the default is *blocked*, and an endpoint opts in by stating what it needs.

This is a framework-agnostic requirement; two reference implementations:

*FastAPI* — enforce at three moments so a route can't exist and serve traffic without an auth decision:

- **Write time** — a custom router whose route-registration method takes `permission` as a **required argument** (e.g. `SecureRouter.secure(path, permission=...)`). Adding a route without naming a permission is a Python error; the auth dependency is wired in automatically.
- **Boot time** — a startup scan over `app.routes` that **refuses to start the app** if any route lacks the auth marker, with a small explicit, greppable allowlist (`@public`) for genuinely unauthenticated routes like login/health. Catches anyone who used a raw `@router.post(...)` instead of the secure router.
- **Request time** — the auth dependency (`Depends(require(permission))`) actually runs the authorization query before the handler.

*ASP.NET Core* — most of this is built in, so there's less to hand-build:

- **`FallbackPolicy`** — set a global fallback policy (`RequireAuthenticatedUser`) so any endpoint with *no* authorization attribute is denied by default; making a route public requires an explicit `[AllowAnonymous]` (opt-out, greppable). This is the deny-by-default goal without a custom startup scan.
- **`[Authorize("order:refund")]`** — the `resource:action` permission maps onto a named policy; a dynamic `IAuthorizationPolicyProvider` mints the policy from the string so you don't register one per permission by hand.
- **Single authorization handler** — an `AuthorizationHandler<PermissionRequirement>` runs the access query once for all permissions. It's **fail-closed by contract**: `context.Succeed()` is the only way to allow, so doing nothing = deny.

**2. Row-Level Security — the database refuses foreign rows.** Even if a hand-written query forgets its `store_id` / `organization_id` filter, the database itself filters it out. Postgres RLS is set per request from the verified context (the org, and store when store-scoped) and applies to every query automatically — so a sloppy query can't leak another tenant's data. This is the floor *below* the application: query scoping is the first line, RLS is the can't-be-wrong backstop. See `database.md` for the RLS setup.

Together these turn "remember to add the check" into "the system won't run without it": no route runs un-permissioned, and no query returns another tenant's rows.


# Audit 



# Managed Roles and Permissions

- TODO



# Vulnerabilities to Review

Open items from the auth-flow review.

**Vulnerabilities**
- There's no privilege-escalation guard on role assignment — nothing here stops an admin granting a role more powerful than their own (the subset rule lives only in `post-mvp.md`).
- The owner-protection rule (`organization.owner_user_id` can't be stripped) is stated but no step in the flow actually checks it before a `membership_assignment` is deleted.

**Weaknesses**
- Authentication itself (login) is unspecified: no password-hashing choice, login rate limiting, account lockout, or MFA for admins handling money.
- Token revocation is required but the mechanism (deny-list vs. short TTL + refresh) isn't decided, so a fired user's token lifetime is undefined.
- `organization_id` immutability is trusted at authentication but never spec'd as DB-enforced, so the tenant boundary rests on an unguaranteed assumption.

