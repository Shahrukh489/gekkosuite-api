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

3. The required scope the membership (and its role) must have to call this API endpoint. Each endpoint **explicitly declares** its required scope — `STORE` or `ORGANIZATION` — rather than us inferring it from the URL. This tells us which kind of membership the user needs. For example, the delete-a-store endpoint declares `ORGANIZATION` scope, so we must check that the user has an `ORGANIZATION` scope membership.


### Process

Once we have the target the user is requesting to perform the action on, and the permission needed to use this endpoint. We need to now check the user's membership's and the role on each membership. This is what will let us decide if the  user is actually authorized. For example if the user is requesting to create a product at `store_abc`, then we need to verify he has a membership at `store_abc`, and a role with the permission `product:create`. 


If the endpoint declares a `STORE` scope (a store action), then the following rules must ALL be satisfied in order:

> A store endpoint only ever declares `STORE` scope, so its permission is never elevated (`is_elevated = false`) — elevated permissions live only on `ORGANIZATION`-scoped endpoints. If a store endpoint's permission is ever elevated, treat it as a misconfiguration and **deny**.

**1. Does the store in the request path `/stores/{storeId}/` belong in the `organizationId` that is in the request context built from the JWT token?**

- If not, then return **404** to let the user know this store does not exist in his organization. He can not act on it.

**2. Does the user have an `ORGANIZATION` scope membership in this `organizationId`, with an unexpired role that grants the permission (and the role's `scope` is `ORGANIZATION`, matching the membership; if a mismatched role somehow exists, ignore it)?**

- An `ORGANIZATION` membership reaches every store in the org, so it can perform this store action.
- If yes, user is authorized.
- If no, proceed to check 3.

**3. Does the user have a `STORE` scope membership on the requested store (the `{storeId}` in the path), with an unexpired role that grants the permission (and the role's `scope` is `STORE`, matching the membership; if a mismatched role somehow exists, ignore it)?**

- If yes, user is authorized.
- If no, return **403** — the user has neither an `ORGANIZATION` membership that reaches this store, nor a valid `STORE` membership on it with the permission.


If the endpoint declares an `ORGANIZATION` scope (an org action), then the following rules must ALL be satisfied in order:

**1. Does the user have an `ORGANIZATION` scope membership in the `organizationId` that is in the request context built from the JWT token, with an unexpired role that grants the permission (and the role's `scope` is `ORGANIZATION`, matching the membership; if a mismatched role somehow exists, ignore it)?**

- If yes, user is authorized.
- If no, return **403** — only users with an `ORGANIZATION` membership and a role granting the permission can perform organization actions.


### Query

A user can have many memberships. So we go through each one and ask: "does this membership let the user do the action?" If any single membership passes, the user is authorized. If none do, they are denied.

The endpoint gives us two things to check against: its `requiredScope` (`STORE` or `ORGANIZATION`) and its `requiredPermission`.

For each membership the user holds, we check the following in order:

1. **Is the membership usable?** Skip it if it is not live (removed, suspended, or its role expired), if the role's scope does not match the membership's scope (ignore bad data), or if it is not in the user's organization from the token.

2. **Does the membership reach the target?**
   - For a `STORE` action: an `ORGANIZATION` membership passes (it reaches every store), or a `STORE` membership passes if it is on the requested store.
   - For an `ORGANIZATION` action: only an `ORGANIZATION` membership passes.

3. **Does its role grant the permission?** If the membership reached the target and its role has the `requiredPermission`, the user is authorized.

```
allowed = false

for each membership M the user holds:
    role = the role on M for this request

    -- 1. is this membership usable?
    if M is not live (removed / suspended / role expired):   continue
    if role.scope != M.scope:                                continue   -- ignore bad data
    if M.organization_id != context.organizationId:          continue   -- must be the user's org

    -- 2. does M reach what the endpoint targets?
    if endpoint.requiredScope == 'STORE':
        reached = ( M.scope == 'ORGANIZATION' )                              -- org reaches every store
               or ( M.scope == 'STORE' and M.store_id == {storeId in path} ) -- or the store itself
    else:  -- ORGANIZATION action
        reached = ( M.scope == 'ORGANIZATION' )

    -- 3. does its role grant the permission?
    if reached and role has requiredPermission:
        allowed = true; break

return allowed
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

# Security Review

An adversarial pass over this design, thinking like an attacker trying to break tenant isolation,
escalate privileges, or bypass the login.

**Summary:** the authorization model is strong — the tenant boundary comes from the token, routing is by
declared scope, each membership is checked on its own, elevated permissions are structurally contained,
and there are two fail-closed nets (deny-by-default and RLS). The weakest area is **authentication** —
the login, the token lifecycle, and the register PIN — where most of the top risks live.


## Critical

- **JWT validation is not pinned down.** The whole design trusts that the token is genuine and that its
  `organizationId` is the tenant wall. If the verifier accepts `alg: none`, allows an RS256→HS256
  downgrade, or skips the signature / issuer / audience / expiry checks, an attacker can forge a token
  with any `organizationId` and reach every tenant at once — the highest-impact bug possible here. Pin
  the exact expected algorithm (reject `none`), verify the signature with a rotated key, and validate
  `exp` / `iss` / `aud`.
- **Password hashing is not specified.** Passwords must be stored with a slow, salted KDF (argon2id or
  bcrypt); without it a database dump cracks every account. The schema column is named `password` — call
  it `password_hash` and state the KDF so plaintext is never a temptation.


## High

- **No token revocation.** A token stays valid until it expires, so a fired or demoted employee keeps
  access for the life of their JWT — which makes the `user.is_active` and membership kill switches
  effectively cosmetic. Pick a mechanism: short-lived tokens with refresh, or a revocation deny-list.
- **The register PIN (`store_pin`) is an unguarded second credential.** It's an authentication path with
  no hashing, no rate-limit, no lockout, and no scope rule. A short PIN is trivially brute-forced, and if
  it grants any action it becomes a backdoor around the JWT/RBAC design. Hash it, rate-limit it, and
  scope it tightly — or state clearly that it is not an auth boundary.
- **No brute-force protection on login.** With many low-privilege cashier accounts and no lockout,
  credential stuffing is the realistic way in. Add login rate-limiting and account lockout/backoff.
- **No MFA**, especially for org admins and the owner — the accounts that can drain the whole company.


## Medium

- **Editing and deleting shared records is undefined.** A store user editing a shared `customer` or
  `product` changes a record every store sees, and deleting one another store references could break it.
  The create path is specified; the edit/delete rules are not. Decide them before shipping sharing.
- **No audit logging.** Role grants/revokes, user create/delete, owner transfer, membership changes, PIN
  changes, and sharing flips aren't recorded — so after an incident there's no way to answer "who did
  this," and any privilege abuse is invisible.


## Scorecard

| Area | Grade | Note |
|---|---|---|
| Tenant isolation (cross-org) | A− | token-sourced org boundary; depends on solid JWT validation |
| Authorization model | A | declared scope, per-membership checks, elevated-permission containment |
| Cross-store (same-org) IDOR | A− | `store_id` in every by-id query, plus store-level RLS |
| Privilege-escalation controls | B | escalation-ceiling (subset) rule in place |
| Authentication (login / token / PIN) | D | hashing, brute-force, MFA, token revocation, PIN all open |
| Auditing | D | none |

**Bottom line:** the authorization engine is solid. The ship-blocking work is all in **authentication** —
JWT validation, password hashing, token revocation, the register PIN, and brute-force protection. Close
those and this is a strong design.


