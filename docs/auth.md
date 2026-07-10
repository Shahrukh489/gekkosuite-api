# Overview

> A user is just a login. *Where* they can act comes from the **memberships** they hold; *what* they
> can do there comes from the **roles** on those memberships.

```
user  ──<  membership  ──<  membership_assignment  >──  role  ──<  role_permission  >──  permission
```

- **user** — an identity of a person

- **membership** — a membership gives acess to a user at the `ORGANIZATION`, a `STORE`, or both.
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


If the user is requesting to perform an action on the store then the following rules must ALL be satisfied in order:

**1. Does the store in the request path `/stores/{storeId}/` belong in the `organizationId` that is in the request context built from the JWT token?**

- If not, then return **404** to let the user know this store does not exist in his organization. He can not act on it.

**2. If the API endpoint permission is elevated (`is_elevated = true`), does the user have an `ORGANIZATION` scope membership in this `organizationId`?**

- If yes, check if the user has an unexpired assigned role with the permission (the role's `scope` must match the membership's `scope`; if a mismatched role somehow exists, ignore it).
    - If yes, user is authorized.
    - If no, return **403**.
- If no, return **403** — only users with an `ORGANIZATION` membership can perform elevated actions.

**3. If the API endpoint permission is not elevated (`is_elevated = false`), does the user have an `ORGANIZATION` scope membership in this `organizationId`?**

- If yes, check if the user has an unexpired assigned role with the permission (the role's `scope` must match the membership's `scope`; if a mismatched role somehow exists, ignore it).
    - If yes, user is authorized.
    - If no, proceed to check 4.
- If no, proceed to check 4.

**4. If the API endpoint permission is not elevated (`is_elevated = false`), does the user have a `STORE` scope membership on the requested store in the path variable in this `organizationId`?**

- If yes, check if the user has an unexpired assigned role with the permission (the role's `scope` must match the membership's `scope`; if a mismatched role somehow exists, ignore it).
    - If yes, user is authorized.
    - If no, return **403**.
- If no, return **403** — the user does not have an `ORGANIZATION` role that has access to all the stores, nor does he have a valid `STORE` membership.


If the user is requesting to perform an action on the organization then the following rules must ALL be satisfied in order:

**1. Does the user have an `ORGANIZATION` scope membership in the `organizationId` that is in the request context built from the JWT token?**

- If not, return **403** — only users with an `ORGANIZATION` membership in the `organizationId` can perform organization actions. 

**2. Does the user have an unexpired assigned role with the permission required by the endpoint?** (the role's `scope` must match the membership's `scope`; if a mismatched role somehow exists, ignore it)

- If yes, user is authorized.
- If no, return **403** — the user belongs to the organization but their role does not grant this permission.


- Sample Query depicting the authorization check:

```
allowed = false

for each membership M the user holds:            -- check every membership on its own
    role = the role on M for this request

    -- shared checks: is this membership even usable, and does it apply to this request?
    if M is not live (removed / suspended / role expired):   continue
    if role.scope != M.scope:          continue   -- ignore mismatched data

    if M is a STORE membership:            -- run the STORE rulebook
        pass = request is a STORE action
               and M.store_id == {storeId from the path}
               and M.organization_id == context.organizationId
               and requiredPermission.is_elevated == false

    if M is an ORGANIZATION membership:    -- run the ORG rulebook
        pass = M.organization_id == context.organizationId
               and ( request is an ORG action
                     or ( request is a STORE action
                          and store({storeId}).organization_id == context.organizationId ) )

    -- the final question, for a membership that applies: does its role grant the permission?
    if pass and role has requiredPermission:
        allowed = true; break

return allowed        -- one membership passed everything → allow ; otherwise → 403
```

@TODO:
Two of these checks are also **safety nets**: `role.scope == M.scope` and the
`is_elevated == false` rule re-verify things that are *also* guaranteed when data is saved (see
*Write-time security*). So even if a bad record ever got into the database — an org-only permission in
a store role, or a role on the wrong kind of membership — the loop simply ignores it. We never trust
the stored data blindly; we re-check it on every request.



##  Further additional security

There are many scenarios where even if the user is authenticated and authorized, invalid operations and actions can be performed, the following section describes key areas where to add proper guardrails.

- Do **not** put a user's roles, permissions, or flattened access list in the JWT: a token that carries
its own permissions can't be revoked, so a suspended or demoted user keeps their old access until the
token expires.

- When doing a Read/Update/Delete operations on a requested resource. We must ensure that the resource
actually belongs to the target in the request. For example if the user requests to delete `order_abc`, we must check if the `order_abc` belongs in the target store.   

- When inserting a new row, the `organization_id` is always taken from the request context object built in the Authentication step when validating the JWT token. This will prevent us from ever allowing a user to save data in a different `organization_id`. 

- When assigning permissions to a role with the scope `STORE`, denying any permissions with `is_elevated = true`. This will prevent a store membership from every having elevated access.

- When assigning a role to a membership, check if the scope of the role matches the scope of the membership. For example a role with scope `STORE` can only exist on a membership with scope `STORE`. This will prevent from ever assigning a role with scope `ORGANIZATION` to a membership with scope `STORE`.



### Working with Shared Resources (Product/Customer/..)

Normally a store's products and customers are its own. But an org can turn on **sharing**, and then a
customer or product created at one store is visible to *every* store in the org (see `tenancy.md`).

1. **Customers are shared; products depend on a setting.** A customer create writes the shared
   `customer` record. A product create writes the shared `product` record only when the org's
   **`allow_share_products`** setting is on (off by default → the product stays the store's own).
   
2. **The permission stays non-elevated.** `customer:create` / `product:create` are **non-elevated**, so
   they can live in a **store role** — a cashier can hold them. (If they were elevated they couldn't be
   in a store role at all, which is the opposite of what sharing wants.) So the only thing gating a
   store user is whether their role includes the permission — not the permission's level, and not the
   membership kind.

3. **The check is the ordinary authorization query** — no branch for "is this shared?", no branch on
   the membership kind. A **store membership** at `{storeId}` *or* an **org membership** qualifies, as
   long as a live role on it holds the permission and the place is inside the caller's org (the tenant
   boundary the query already bakes in). A store cashier and an org admin pass through the identical
   check.

4. **`organization_id` comes from the token, never the request body.** The new record's
   `organization_id` is set server-side from `context.organizationId`, and its `store_id` is the
   `{storeId}` from the path (already validated by the query). So a store user can only ever create
   within *their own* org and *their own* store — they can't pass a different org or store id to write
   into another tenant. This is what makes the ordinary permission check safe here.

5. **RLS matches the two tables' scope.** The store-level copy (`store_customer` / `store_product`) is
   **store-scoped** — a store only ever reads its own rows. The shared record (`customer` / `product`)
   is **org-scoped** — every store in the org resolves it. So sharing-on shows the item org-wide,
   sharing-off keeps it to the store, and neither can leak across orgs (see `database.md`).

Mechanically the store-level row is written; the shared row is written in the **same transaction**,
with the store row linking up to it — for customers, and for products when `allow_share_products` is on:

```
create customer on /stores/{storeId}/customers:
  INSERT customer (shared) , then INSERT store_customer linked to it   -- one txn

create product on /stores/{storeId}/products:
  allow_share_products OFF → INSERT store_product (links to no shared row)
  allow_share_products ON  → INSERT product (shared) , then INSERT store_product linked to it   -- one txn
```

> **⚠️ Open risk — editing/deleting a *shared* record is not yet specified.** The above covers
> **create**. Once a customer or product is shared org-wide, a store user at Store A editing it changes
> a record **every** store sees, and deleting it could pull a record Store B is actively using. The
> `customer:edit` / `product:edit` / `delete` paths on a *shared* row need an explicit rule — probably
> the same check as create (an everyday permission plus the tenant boundary), but "probably" is where bugs
> hide. Questions to resolve: can any store with the permission edit a shared record, or only the store
> that created it? Can a shared record be deleted while another store references it? **Decide before
> shipping sharing.** Tracked in *Security Review Notes* below.


## Safety nets — making mistakes fail closed

Everything above is correct only if every endpoint remembers to apply it. Developers forget: a new
route ships without its permission check, or a query is written without its tenant `WHERE` clause. So
we don't rely on memory — two system-wide safety nets make the *default* outcome "denied," so a
forgotten check fails closed instead of leaking.

**1. Deny-by-default routing — a route must declare its permission, or it's blocked.**

- *ASP.NET Core* — create a global **`FallbackPolicy`** (`RequireAuthenticatedUser`) so any endpoint with no
  authorization attribute is denied by default (public routes need explicit `[AllowAnonymous]`); 

**2. Row-Level Security — the database refuses foreign rows.** Even if a hand-written query forgets its
`store_id` / `organization_id` filter, Postgres RLS filters it out. RLS is set per request from the
verified context (`app.current_org`, and `app.current_store` for store actions) and applies to every
query automatically.

> **RLS only protects you if it's deployed exactly right — it fails *silently* if not.** Two conditions
> must hold, or RLS runs but does nothing:
> - **The app connects as a non-superuser, non-owner role.** Postgres superusers and table owners
>   *bypass* RLS entirely. Run migrations/admin as a separate privileged role; the request path must
>   use a restricted role.
> - **The tenant setting is transaction-scoped** (`SET LOCAL app.current_*`, set per request inside its
>   transaction). Otherwise a pooled connection can carry one request's tenant into the next request —
>   the classic pooling leak.
>
> Both failure modes look fine in normal testing (data still comes back), so they must be verified
> explicitly. See `database.md` for the exact setup.

---

# Security Review (open items — not yet in the design above)

##  High 

- **Password hashing is not specified.** Must be a slow, salted KDF (argon2id or bcrypt). Without it a
  DB dump = every account cracked. Biggest single omission.
- **No brute-force protection** — login rate-limiting, account lockout/backoff. Credential stuffing is
  the realistic entry for a money app with many low-privilege cashier accounts.
- **No MFA**, especially for org admins and the owner — the accounts that can drain the whole company.
- **Token revocation mechanism is undecided** (the doc says "revocation / short-lived + refresh" but
  doesn't commit). Until decided, `user.is_active` / membership kill switches are cosmetic — a fired
  employee's JWT keeps working until it expires. Pick one: short TTL + refresh, or a deny-list.
- **`store_pin` (register PIN) has zero coverage here.** It's an auth path in the schema — needs its
  own hashing, rate-limit, and scope treatment, or it's a weak-secret backdoor.
- **No subset (escalation-ceiling) rule on role assignment.** Nothing stops an org admin from granting
  a role more powerful than their own, or granting `role:assign` / minting another org admin. A single
  compromised admin account = full org takeover with no ceiling. Add "you may only grant permissions
  you already hold."
- **User-create + role-assign is the real crown-jewel path** and isn't specially protected the way the
  owner column is. Guard "admin account compromised → creates a new org admin."
- **Owner "can't be stripped" is asserted, not enforced in the flow.** Add explicit guards: the owner's
  effective admin access can't be removed, and the org can't be left with zero admins.


## Low — clarity (ambiguity is a liability in a security spec)

- **No Audit section** (still open), though "audited" actions are referenced (owner transfer, etc.).
  Require audit logging for security-relevant events: role grant/revoke, user create/delete, owner
  transfer, membership changes, sharing-setting flips.


