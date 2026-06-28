# Auth (authentication & authorization)

How people sign in, how they get access to stores and the organization, and how we decide what they're allowed to do where.


## The model in one picture

 Read it left to right:

```
user  ──<  membership  ──<  membership_assignment  >──  role  ──<  role_permission  >──  permission
(login +   (belongs at        (a role assigned          (bundle of   (the role's          (resource:
 type:      one place:         on that membership,       perms +      permissions)         action,
 ORG/STORE) a store or         optionally expiring)      a type:                           scope-free,
            the org)                                     ORG/STORE)                         is_elevated)
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

**In short**
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
The login is the `user` row, carrying a `user_type` (`ORGANIZATION` or `STORE`). A place is a `membership` (a `store_id` or `organization_id`) — its kind must match the user's type. A role at that place is a `membership_assignment`, which can optionally carry an `expires_at`. The role's own `user_type` must equal the user's.


## How do I revoke or suspend someone's access?

**In short**
You can dial access down by how permanent you want it — without ever deleting the person. The account always stays.

**How it works**
Three levels:

- **Take away one role** — they still belong at the place, just with less (or no) ability there.
- **Suspend (temporary)** — switch their access off but keep everything, so you can switch it back on. Good for "on leave" or a temporary block.
- **Remove from a place (permanent)** — they no longer belong there. The record is kept for history.

**Example**
A cashier goes on leave → *suspend* them. They quit → *remove* them from the store. You gave a cashier refund rights by mistake → just *take away that role*.

**Under the hood**
Take-away = delete the `membership_assignment` (or let `expires_at` end it). Suspend = `membership.is_active = false`. Remove = soft-delete the membership (`deleted_at`). The `user` row is never touched.


## What is a role, and where do roles come from?

**In short**
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

**In short**
The change applies to everyone with that role, immediately — there's nothing to re-assign.

**How it works**
Users don't carry their own copy of a role's permissions; their access is read fresh on every request from the role itself. So editing a role updates everyone who holds it at once.

**Good to know**
Custom roles (an org cloning a shipped role or building its own) are planned for after MVP.


## How do users get roles at a store or the organization?

**In short**
You attach a role to a person *at a specific place* — "give Maria the Manager role at the Portland store."

**How it works**
An org admin assigns the role; it lands on the person's membership at that place. One person can hold several roles at a place, or none. A role can also be set to **expire** automatically — handy for temporary or seasonal staff.

**Good to know**
Assigning roles needs the `role:assign` permission, which today only the Org Admin role has.

**Under the hood**
A grant is a `membership_assignment` (membership + role), optionally with `expires_at`.


## Can a store user ever gain organization-level access?

**In short**
No — and it's not a rule that can be forgotten or bypassed; it's how the system is built.

**How it works**
An org role can only be attached to someone who belongs at the **organization**. A user only belongs at the organization if an org admin put them there by giving them a membership. So a store-only user has no organization membership, and hence can never do any organization level operations.

**Example**
A cashier who only belongs to Store A can never be given "Org Admin," because they're not a member of the organization — only of the store.


## How far does a role reach? (store vs org)

**In short**
A store role acts on **one store**. An org role acts on the **whole organization and every store in it**.

**How it works**
The *same* permission reaches differently depending on the role that holds it. `order:refund` in a Cashier (store) role refunds at that one store; the same `order:refund` in an Org Admin (org) role refunds at *any* store in the company. An org admin can act everywhere not because of special permissions, but because their role is org-level — org reach simply includes all the stores.

**Example**
- Store Manager at Seattle with `product:edit` → can edit Seattle's products only.
- Org Admin with `product:edit` → can edit any store's products.


## Who is the owner, and can their access be taken away?

**In short**
The person who signs up and creates the organization is its **owner**. They have full access, and **no one else can take it from them.**

**How it works**
The owner is marked on the organization itself, not given as an ordinary grant — so it can't be deleted out from under them. The system refuses any attempt to remove the owner's admin access while they're still the owner.

The only way ownership changes is a deliberate **transfer**: the current owner hands it to someone else (who must already be an org admin). No other admin can revoke the owner's access or seize ownership.

**Example**
A disgruntled co-admin tries to remove the founder's access → the request is rejected. The founder later sells the business → they transfer ownership to the new owner, which is recorded and audited.

**Under the hood**
The owner is `organization.owner_user_id` (a column, not a `membership_assignment`). Transfer = updating that column; it's a privileged, audited action only the current owner can do.


## How do we know everything a user can do, and where?

**In short**
At login we gather all the places a user belongs and the roles at each, and turn it into one simple list: where they can go, and what they can do there.

**Example**
```
Maria's access
 place        type    can do
 ----------   -----   -------------------------------------------
 Acme Inc     ORG     manage stores, manage users, edit any product
 Seattle      STORE   read products, sell
 Portland     STORE   read products, sell, refund
```

**Good to know**
Every entry points at a real store or organization, so it can never reference a place that doesn't exist — and deleting a place automatically clears the access tied to it.



# API Authentication and Authorization Flow

This is the authentication and authorization flow each request must go through to verify if a user has permission to make the API request.

## 1. JWT Token Validation Middleware — Authentication

This middleware only validates that the JWT token is valid and not expired or tampered with. It is the first check: if the token is invalid then nothing about it can be trusted and we should not proceed further — the user is NOT authenticated. Return **401**.

**Validation Process**:
- **Signature is valid** (signed by us, not tampered).
- **Not expired.**
- **Pin the expected algorithm** and **reject `alg: none`** — never let the token choose its own algorithm (blocks the `none` bypass and RS256→HS256 confusion attacks).
- **Strong, rotated signing key** — a leaked key means every token is forgeable.
- **Real revocation / short-lived tokens + refresh** — without it, a fired or suspended user's token keeps working until it expires, so `is_active = false` / `deleted_at` have no effect until then. For a money app this is mandatory, not optional.
- TBD more checks like claim validation and other industry practices

## 2. Tenancy Validation Middleware — Authorization (boundary)

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
  M3 authz:    read user_type fresh from DB
               store user targeting the org?                              → yes → 403
               a live membership (at the target the user's type allows)
                 whose role holds the required permission?                → no → 403
               resource named by id belongs to the target?               → no → 404
  → action runs
```

## 3. Endpoint Authorization — can the user do this action here?

Once we have confirmed the user is authenticated (step 1) and that the target organization or store is inside the user's organization (step 2), the endpoint does the final check: **is this user authorized to perform this specific action on this target?** Each endpoint declares the one permission it needs, and we verify the user holds it at the target.

- API: `POST /products`, Permission: `product:create`
- API: `POST /refunds`, Permission: `order:refund`

**Authorization Process**:

**Membership + permission check.** The user's **type** decides the whole shape of this check, so we branch on it first. (Type is read fresh from the database on each request — never trusted from the token — so a user whose access changed is judged on what's true *now*.)

- **Organization user** — they belong to the organization and reach every store in it. M2 already proved the target (store *or* org) is inside their organization, so there's nothing more to locate: just confirm their org membership holds a role with the required permission.
- **Store user** — they only act on stores they're a member of, and can never touch the organization. If the target *is* the organization → **403** immediately (impossible by type). Otherwise, confirm they have a membership at *that* store holding a role with the required permission.

Because type already pins the org-vs-store level, the old "does this org membership reach the target store?" lookup disappears — an org user's reach is implied by their type plus M2.

In SQL, this means walking `membership → membership_assignment → role → role_permission → permission` as one joined row:

```
Read user.user_type FRESH from the DB.

if user_type = STORE AND context.targetTenantType = ORG  →  403   -- impossible by type

Does a row exist in:
  membership → membership_assignment → role → role_permission → permission

where:
    membership.user_id = context.userId
    AND permission     = requiredPermission        -- the role has the permission

    -- the membership is live (not removed, not suspended) and the role hasn't expired
    AND membership.deleted_at IS NULL
    AND membership.is_active  = true
    AND (membership_assignment.expires_at IS NULL OR membership_assignment.expires_at > now())

    -- the membership is at the place the user's type allows
    AND ( user_type = ORGANIZATION  →  membership.organization_id = context.organizationId
          user_type = STORE         →  membership.store_id        = context.targetTenantId )
```
No matching row → **403 Deny** (deny by default).

(The `role.user_type` match is now implied — a user only holds roles of their own type — but the assignment write-path enforces it, so it stays a backstop, not a runtime branch.)

It all has to come from **one** membership-role-permission chain, not a mix — an expired Cashier role that could refund doesn't lend its permission to a separate, still-active Stocker role that can't. (Walking the chain as one joined row guarantees this: the live membership, the unexpired assignment, and the permission must all sit on the *same* row, so a dead role can't lend its permission to a living one.)


**2. Resource ownership check.** If the action names a specific resource by id (the order to refund, the product to edit), load that resource and check it belongs to the target:

```
load the resource by id
if resource's store/org != the target  →  404
```

Having `order:refund` at Store A does **not** let you refund a Store-B order. Without this check, a user could touch another store's data just by passing its id — the most common multi-tenant breach. Return **404** (not 403) for anything outside the target, so you don't reveal that it exists.

This is as important as the permission check — **every endpoint that takes a resource id must do it.**


# Audit 



# Managed Roles and Permissions

- TODO



# Vulnerabilities to Review

Open items from the auth-flow review. Items already handled in the SQL above (membership liveness filters, the `user_type` gate replacing the old `role.scope` match, store-existence note) are not repeated here.

**Vulnerabilities**
- There's no privilege-escalation guard on role assignment — nothing here stops an admin granting a role more powerful than their own (the subset rule lives only in `post-mvp.md`).
- The owner-protection rule (`organization.owner_user_id` can't be stripped) is stated but no step in the flow actually checks it before a `membership_assignment` is deleted.

**Weaknesses**
- Authentication itself (login) is unspecified: no password-hashing choice, login rate limiting, account lockout, or MFA for admins handling money.
- Token revocation is required but the mechanism (deny-list vs. short TTL + refresh) isn't decided, so a fired user's token lifetime is undefined.
- "Return 404 not 403 so existence isn't leaked" is stated only for the IDOR check, not as a global convention, so other endpoints may still leak via 403.
- `organization_id` immutability is trusted by M1 but never spec'd as DB-enforced, so the tenant boundary rests on an unguaranteed assumption.

**Missing**
- The Audit section is an empty stub — no security-relevant events (role grant/revoke, user create, owner transfer, refunds) are logged anywhere.
- The Managed Roles and Permissions section is `TODO` — the seed roles and full permission catalog are undefined.
- No concurrency/locking note for money actions (refunds, balance deplete) that this flow gates, so double-spend under simultaneous requests is unaddressed.
- No rate limiting on sensitive write endpoints (refunds, user creation) beyond the missing login throttle.