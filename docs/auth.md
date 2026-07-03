# Auth (authentication & authorization)

How people sign in, how they get access to stores and the organization, and how we decide what
they're allowed to do where.


**Overview**

> A user is just a login. *Where* they can act comes from the **memberships** they hold; *what* they
> can do there comes from the **roles** on those memberships.

A user isn't labelled "an org person" or "a store person" — that's decided entirely by **which
places they have a membership to**. The same person can belong to a store, to the organization, or to
both, and that's what sets their reach. Roles, on the other hand, *are* typed (store or organization)
so you can not assing an organization level to a store user. Usually any Role with an "is_elevated=true" permission on it is an organization level role.


## The chain (how the pieces connect)

Read the chain left to right: a **user** has **memberships** (places they belong); each membership is
given **roles**; each role is a bundle of **permissions** (individual allowed actions).

```
user  ──<  membership  ──<  membership_assignment  >──  role  ──<  role_permission  >──  permission
```

- **user** — one login per person (organization identity). A user is placed by the memberships they
  hold; the `user` row itself carries no org-vs-store distinction.
- **membership** — the user has as a membership to either the `ORGANIZATION` or a `STORE`
    - Every membership has an `organization_id. 
    - A **store** membership sets `store_id` (the one store it's at) and sets `user_type` to `STORE`.
    - A **organization** membership leaves `store_id` NULL and sets `user_type` to `ORGANIZATION`.
- **membership_assignment** — a role given to that membership (optionally with an expiration).
- **role** — a named bundle of permissions we ship. Each role has a `user_type` (`STORE` or
  `ORGANIZATION`) — its level. A role's type must match the membership it's attached to: store roles
  go on store memberships, org roles on org memberships.
- **permission** — one allowed action, like `product:read`. Carries an `is_elevated` flag to distinguish this permission as something only for users with an Organization Membership.

So an "org user" is simply **a person who holds an organization membership**; a "store user" is
someone who holds only store memberships. 

# FAQ

## How is a user created and given access?

A person's login and what they can do are two separate things. Creating a user just makes the login —
they can do nothing until you give them a place and a role there.

An org admin sets someone up in three steps:

1. **Create the user** — make their login. That's all. A fresh user belongs nowhere and can do
   nothing.
2. **Give them a membership** — add them to a place: the **organization** if they should oversee the
   whole business, or one or more **stores** if they're an employee. This single choice is what makes
   someone an org person or a store person.
3. **Give them a role** — choose what they can do at that place. The role's type must match the
   membership: store roles on store memberships, org roles on org memberships.

A person can hold an org membership *and* store memberships (unless the org's cross-membership setting
forbids it — see *Can a user be both an org and a store user?*).

**Example**
Maria is added to the Seattle store as a Cashier, then later to Portland as a Manager — one login, two
store memberships, a different role at each. If the owner later wants Maria to oversee the whole
company, they **add an organization membership** with an org role; she now belongs at the org too,
with no change to the user record itself.


## How do I revoke or suspend someone's access?

You can remove access by how permanent you want it — without ever deleting the person.

- **Take away one role** — delete the `membership_assignment` (or let `expires_at` end it). They still
  belong at the place, just with less ability there.
- **Suspend at one place (temporary)** — `membership.is_active = false`. Switches their access off at a
  single place but keeps everything, so you can switch it back on.
- **Disable the whole account (temporary)** — `user.is_active = false`. One switch turns the person off
  *everywhere* at once. Effective access needs both `user.is_active` and the place's
  `membership.is_active`.
- **Remove from a place (permanent)** — soft-delete the membership (`deleted_at`). The record is kept
  for history; the `user` row itself is never deleted.


## What is a role, and where do roles come from?

A role is a named bundle of things a person is allowed to do (like "Cashier" or "Org Admin"). We build
and ship the roles; customers assign them — they don't author their own yet.

- A **permission** is one allowed action — e.g. read a product, refund an order, create a user.
- A **role** is a set of permissions, plus a **type**: every role is either a **store role** or an
  **organization role**. That type sets where the role may be attached and the system rejects any invalid role assignments, for example assigning an organization level role to a user with a store membership. 
- We don't do "allow everything" — a role lists its permissions explicitly, so a new feature reaches
  nobody until it's deliberately added to a role.


## What does `is_elevated` permission mean?

`is_elevated` is a single true/false flag on each **permission** answering one question: *is this a
powerful, company-level action, or an everyday one?* It splits every permission into two piles:

| Permission        | `is_elevated` | Why                                                   |
|-------------------|---------------|-------------------------------------------------------|
| `product:read`    | **false**     | A cashier does this all day. Everyday store work.     |
| `order:refund`    | **false**     | A store manager does this. Everyday store work.       |
| `customer:create` | **false**     | Happens at the register. Everyday store work.         |
| `store:create`    | **true**      | Creating a whole store is a company-owner action.     |
| `user:create`     | **true**      | Making new logins is a company-owner action.          |
| `supplier:create` | **true**      | Managing the org's suppliers is company-level.        |

- **`is_elevated = false`** → an ordinary action; fine for a store employee.
- **`is_elevated = true`** → a company-level action; only someone acting *for the whole org* may do it.

So `is_elevated` marks *which permissions are org-only*, independently of any role.


## How does `is_elevated` keep org-only powers out of stores?

Two guards work together — the role's type, and the permission's flag — so the bad combination can't
even be built:

> An **elevated permission can only sit in an org role.** And an **org role can only be attached to an
> organization membership.** So an elevated permission can never reach a store.

It's a one-way gate enforced on the write path:

- **Building a role:** adding an elevated permission to a role requires that role to be an org role.
  A store role simply can't contain `user:create` or `store:create`.
- **Assigning a role:** an org role can only be attached to an org membership; a store role only to a
  store membership. The role type must match the membership kind.

**Example.** `store:create` is elevated, so it can only live in an org role:

- A "Cashier" (store role) **cannot contain** `store:create` — the write path rejects it. So a cashier
  can never hold it, on any membership. ✅ safe.
- Diego's "Org Admin" (org role) contains `store:create` and sits on his **Acme org** membership →
  it works. He's acting for the whole company, which is exactly who may create stores. ✅ correct.

The two checks reinforce each other: even if one were misconfigured, the other still stands between a
store and an org-only power.


## Can a store user ever gain organization-level access?

Only by being **given an organization membership** — which only an org admin can do, by assigning an
org role to an org membership. Someone with only store memberships has no org reach: store roles can't
hold elevated permissions, and org roles can't be attached to their store memberships. So an everyday
employee can never act on the organization.

**Example**
A cashier who belongs only to Store A, can't be handed "Org Admin," because that's an org role and they
have no org membership to attach it to.


## Can a user be both an org and a store user?

That's an **org-level choice**, controlled by one setting:

> **`allow_user_cross_memberships`** (an organization setting)
> - **ON** — a user may hold an organization membership *and* store memberships at the same time.
> - **OFF (the default)** — each user is locked to a single kind: at the moment a membership is
>   *added*, the write path enforces —
>   - if the user already has an **org** membership → refuse to add a **store** membership;
>   - if the user already has any **store** membership → refuse to add an **org** membership.

With it **OFF** (the default) an org person is an org person and a store employee is a store employee,
never both — the simplest thing for non-technical customers. With it **ON**, one person can span both
levels — handy for a small business where an owner also works a register, or where a store employee is
later given an org membership to help oversee the company.

**Note:** Toggling on and off can result in Organization Admin setting having to choose what membership a user should keep if he has two memberships.

## How far does a role reach? (store vs org)

Reach follows the **membership the role hangs on**:

- A role on a **store** membership reaches that **one store**.
- A role on the **organization** membership reaches the **whole org and every store in it**.

The *same* permission reaches differently depending on the membership it's exercised through:
`order:refund` in a Cashier role on the Seattle membership refunds at Seattle only; `order:refund` in
an Org Admin role on the org membership refunds at *any* store. An org admin acts everywhere not
because of special permissions, but because their membership is at the org — org reach includes all
the stores.

**Example**
- Store Manager at Seattle with `product:edit` → edits Seattle's products only.
- Org Admin with `product:edit` → edits any store's products.


## Who is the owner, and can their access be taken away?

The person who signs up and creates the organization is its **owner**. They have full access, and no
one else can take it from them.

The owner is marked on the organization itself — `organization.owner_user_id`, a column, not a
`membership_assignment` — so it can't be deleted out from under them. The only way ownership changes is
a deliberate **transfer**: the current owner hands it to someone who's already an org admin. No other
admin can revoke the owner's access or seize ownership. Transfer is a privileged, audited action.


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
that place. Because each role carries a `user_type`, this is a direct filter — no guessing from a
role's permissions:

- Attaching to a **store** membership → list only **store roles** (`role.user_type = STORE`).
- Attaching to an **organization** membership → list only **org roles** (`role.user_type =
  ORGANIZATION`).


# API Authentication and Authorization Flow

Two layers: **authenticate** (who are you?) then **authorize** (may you do this here?).

## 1. Authentication — validate the JWT, load identity, build context

A middleware validates the JWT — signature, expiry, **pinned algorithm** (reject `alg: none`), a
strong **rotated** signing key, and **real revocation / short-lived tokens + refresh** so a suspended
user's token stops working promptly. If anything fails, stop and return **401**.

On success, read **`is_active` fresh from the `user` row** — one DB read per request — so a disabled
account is judged on what's true now, never on a stale token. Then build the request **context**:


```
if context.isActive = false                 →  403   -- kill switch: off everywhere
```

```
context = {
  userId,            // from the token
  organizationId,    // from the token — the tenant boundary, never client input
  isActive           // account kill switch — read FRESH from the user row
}
```

The context holds only **verified identity** — who the user is, their home org (the tenant), and the
freshly-read account status. It carries **no target**: the thing being acted on comes from the route,
not from a header the client set.

## 2. Authorization — may this user do this action, here?

Each endpoint declares the one permission it needs. Store actions carry the store in the **path**
(`/stores/{storeId}/...`); org actions (`/organization/...`) carry no place id — the org comes from
the token.

Selling and everything a store owns (its products, customers, sales) are **store actions**.
Procurement, suppliers, expenses, and managing stores/users are **org actions**.

- `POST /stores/{storeId}/products`   → permission `product:create` (store action)
- `POST /stores/{storeId}/refunds`    → permission `order:refund`    (store action)
- `POST /organization/purchases`      → permission `purchase:create` (org action)

**Error convention:** anything outside the user's organization — a store in another org, or a resource
not in the acted-on place — returns **404, never 403**, so existence isn't leaked. 403 is reserved for
"this is yours, but you lack the permission."


**The authorization query.** Does the user hold a live membership that has a role with the required permission at the store or organization level based on the request?
- If the request is to make a change in the Organization itself, does the user have an organization membership that has a role with permissions to make that change.
- If the request is to make a change in a Store, does the user have an store or organization membership that has a role with permissions to make that change for that Store

**Important!**
- Must make sure that a user has a membership at the place you want to make a change, if user has membership in Store A to create:product and in Store B he can only read:product. If a request comes from user to create:product, it should never allow it in Store B. Must check the entire chain:  membership → membership_assignment → role → role_permission → permission.


**Query**
```
Does a row exist in:
  membership → membership_assignment → role → role_permission → permission

where:
    membership.user_id = context.userId
    AND permission     = requiredPermission        -- the role has the permission

    -- account live AND membership live (not removed, not suspended) AND role not expired
    AND membership.deleted_at IS NULL
    AND membership.is_active  = true
    AND (membership_assignment.expires_at IS NULL OR membership_assignment.expires_at > now())

    -- the membership is at the place the route addresses, inside the caller's org.
    -- Branch on membership.user_type, not on which id is null: organization_id is carried on EVERY
    -- membership (the tenant boundary), so user_type is what tells an org from a store membership.
    AND ( this is an ORG action   →  membership.user_type = 'ORGANIZATION'
                                     AND membership.organization_id = context.organizationId

          this is a STORE action  →  ( membership.user_type = 'STORE'
                                       AND membership.store_id = {storeId from the path}
                                       AND membership.organization_id = context.organizationId )

          -- OR an org membership may perform a store action on any store in its org:
          OR ( membership.user_type = 'ORGANIZATION'
               AND membership.organization_id = context.organizationId
               AND store({storeId}).organization_id = context.organizationId ) )
```

No matching row → **403 Deny** (deny by default). A `storeId` in another org produces no row (the
membership's `organization_id` won't match the token's), so a foreign store is denied by the same
query.

Two things to notice:

1. **A store action can be satisfied two ways** — by a matching *store* membership, **or** by the
   user's *org* membership (which reaches every store in the org). A single query allows either,
   because "an org membership reaches all stores" is the point.
2. **Org-only powers are already handled upstream.** Because an elevated permission can only sit in an
   org role, and an org role only on an org membership, a store membership can never satisfy an
   elevated permission — it's structurally impossible before the query even runs.

It all has to come from **one** membership-role-permission chain, not a mix — an expired Cashier role
that could refund doesn't lend its permission to a still-active Stocker role that can't. Walking the
chain as one joined row guarantees the live membership, the unexpired assignment, and the permission
all sit on the *same* row.



### Resource scoping (IDOR)

When an action names a specific resource by id (the order to refund, the product to edit), we must make sure that the database query has a WHERE clause matching the store_id or organization_id for this resource_id. 

```
UPDATE / SELECT ... WHERE order_id = {orderId} AND store_id = {storeId}
  → no row → 404
```

An order belonging to another store doesn't match `store_id = {storeId}`, so it returns **404** with no
special handling — the isolation is the `WHERE` clause, not something a developer must remember after
loading. For an **organization** action the same idea scopes to the org (`... AND organization_id =
context.organizationId`). **Every endpoint that takes a resource id must do this.**


### Working with Shared Resources (Product/Customer/..)

Shared resources are those that can be updated both by a store user and an organization user. For example customers belong to entire organization, but a user in any store can add them to the organization.

When an org turns sharing on, a **store user can create a customer (or product) that every store in
the org sees.** A store user writing something the whole org shares sounds like it needs special
handling — it doesn't. It's the **same flat permission check** plus the org's setting. The reasoning:

1. **The org setting is the switch — nothing else changes.** The store user's create is only allowed
   to write the *shared* (org-level) record when the org's **`share_customers` / `share_products`
   setting is on**. Off (the default) → the item stays the store's own. The membership, the role, and
   the permission are identical either way; the org's setting alone decides whether the shared row is
   written.

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
   into another tenant. This is what makes the flat check safe.

5. **RLS matches the two tables' scope.** The store-level copy (`store_customer` / `store_product`) is
   **store-scoped** — a store only ever reads its own rows. The shared record (`customer` / `product`)
   is **org-scoped** — every store in the org resolves it. So sharing-on shows the item org-wide,
   sharing-off keeps it to the store, and neither can leak across orgs (see `database.md`).

Mechanically that means the store-level row is **always** written, and when sharing is on the shared
row is written too — in the **same transaction**, with the store row linking up to it:

```
create customer on /stores/{storeId}/customers:
  share_customers OFF → INSERT store_customer (links to no shared row)
  share_customers ON  → INSERT customer (shared) , then INSERT store_customer linked to it   -- one txn
```


### Reducing Developer Auth Errors

It is possible that a developer forgets to add RBAC checks on an API endpoint or forgets a WHERE clause in a SQL query, to prevent the code from running we do the following in .Net:

**1. Deny-by-default routing — a route must declare its permission, or it's blocked.**

- *ASP.NET Core* — create a global **`FallbackPolicy`** (`RequireAuthenticatedUser`) so any endpoint with no
  authorization attribute is denied by default (public routes need explicit `[AllowAnonymous]`); 

**2. Row-Level Security — the database refuses foreign rows.** Even if a hand-written query forgets its
`store_id` / `organization_id` filter, Postgres RLS filters it out. RLS is set per request from the
verified context (`app.current_org`, and `app.current_store` for store actions) and applies to every
query automatically.


