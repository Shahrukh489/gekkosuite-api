# Auth (authentication & authorization)

How people sign in, how they get access to stores and the organization, and how we decide what
they're allowed to do where.

> New to this? Read the next two boxes top to bottom and you'll have the whole idea before any SQL.

**The one-line idea**

> A user is just a login. *Where* they can act comes from the **memberships** they hold; *what* they
> can do there comes from the **roles** on those memberships.

**The shape**

A person isn't labelled "an org person" or "a store person" — that's decided entirely by **which
places they have a membership to**. The same person can belong to a store, to the organization, or to
both, and that's what sets their reach. Roles, on the other hand, *are* typed (store or organization)
so the system can keep an org-only role off a store and an everyday store role off the org.


## The chain (how the pieces connect)

Read the chain left to right: a **user** has **memberships** (places they belong); each membership is
given **roles**; each role is a bundle of **permissions** (individual allowed actions).

```
user  ──<  membership  ──<  membership_assignment  >──  role  ──<  role_permission  >──  permission
```

- **user** — one login per person (organization identity). The user carries **no type** — they're
  placed by their memberships, not by a label.
- **membership** — the user belongs at a place: a **store** (`store_id` set) or the **organization**
  (`organization_id` set). The place is what distinguishes org access from store access. A user can
  hold several memberships — store memberships, an org membership, or both.
- **membership_assignment** — a role given to that membership (optionally with an expiry).
- **role** — a named bundle of permissions we ship. Each role has a `user_type` (`STORE` or
  `ORGANIZATION`) — its level. A role's type must match the membership it's attached to: store roles
  go on store memberships, org roles on org memberships.
- **permission** — one allowed action, like `product:read`. Carries an `is_elevated` flag (see below).

So an "org user" is simply **a person who holds an organization membership**; a "store user" is
someone who holds only store memberships. Take away the org membership and they're a store person —
there's no separate label to also change.


# How it works (common questions)

Each question below is self-contained — skim the headers and read the ones you care about.

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

A person can hold an org membership *and* store memberships. Each membership is placed and roled on its
own.

**Example**
Maria is added to the Seattle store as a Cashier, then later to Portland as a Manager — one login, two
store memberships, a different role at each. If the owner later wants Maria to oversee the whole
company, they **add an organization membership** with an org role; she now belongs at the org too,
with no change to the user record itself.

**Under the hood**
The login is the `user` row (no type column). A place is a `membership` (`store_id` *or*
`organization_id`). A role at that place is a `membership_assignment` with an optional `expires_at`.
The role's `user_type` must equal the membership's kind.


## How do I revoke or suspend someone's access?

You can dial access down by how permanent you want it — without ever deleting the person.

- **Take away one role** — delete the `membership_assignment` (or let `expires_at` end it). They still
  belong at the place, just with less ability there.
- **Suspend at one place (temporary)** — `membership.is_active = false`. Switches their access off at a
  single place but keeps everything, so you can switch it back on.
- **Disable the whole account (temporary)** — `user.is_active = false`. One switch turns the person off
  *everywhere* at once. Effective access needs both `user.is_active` and the place's
  `membership.is_active`.
- **Remove from a place (permanent)** — soft-delete the membership (`deleted_at`). The record is kept
  for history; the `user` row itself is never deleted.

Removing someone's org membership cleanly drops them back to a store person — there's no leftover
label to contradict the now-absent membership.


## What is a role, and where do roles come from?

A role is a named bundle of things a person is allowed to do (like "Cashier" or "Org Admin"). We build
and ship the roles; customers assign them — they don't author their own yet.

- A **permission** is one allowed action — e.g. read a product, refund an order, create a user.
- A **role** is a set of permissions, plus a **type**: every role is either a **store role** or an
  **org role**. That type sets where the role may be attached and how far it reaches.
- We don't do "allow everything" — a role lists its permissions explicitly, so a new feature reaches
  nobody until it's deliberately added to a role.

The role's type is what lets the UI offer the right roles for a place, and lets the system reject a
nonsensical pairing (an org role on a store, or a store role on the org) before it's ever saved.

**Under the hood**
Permissions are named `resource:action` (e.g. `product:read`, `order:refund`) and are scope-free in
name. The org-vs-store level lives in a shared `user_type` lookup the `role` references. A permission
additionally carries `is_elevated` — see the next two sections.


## What does `is_elevated` mean?

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

**Worked example.** `store:create` is elevated, so it can only live in an org role:

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
A cashier who belongs only to Store A can't be handed "Org Admin," because that's an org role and they
have no org membership to attach it to.


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

"kind" is the membership's place (`organization_id` set → org; `store_id` set → store). Every entry
points at a real store or organization, so it can never reference a place that doesn't exist — and
deleting a place clears the access tied to it.


# Showing the right roles in the UI

When an org admin attaches a role to a membership, the role picker should only offer roles that fit
that place. Because each role carries a `user_type`, this is a direct filter — no guessing from a
role's permissions:

- Attaching to a **store** membership → list only **store roles** (`role.user_type = STORE`).
- Attaching to an **organization** membership → list only **org roles** (`role.user_type =
  ORGANIZATION`).

```
GET /memberships/{id}/assignable-roles
  → returns roles whose user_type matches the membership's kind
```

The same field powers a clean roles screen — group the catalog into "Store roles" and "Organization
roles" so an admin sees at a glance where each one belongs. Because the role's type also drives the
write-path guards, the UI can never offer a pairing the backend would reject: what you see in the
picker is exactly what's assignable.


# The request flow (for engineers)

This is the per-request machinery — what actually happens when an API call comes in. If you just
wanted the concepts, you can stop above; this section is the implementation detail.

Two layers: **authenticate** (who are you?) then **authorize** (may you do this here?).

## 1. Authentication — validate the JWT, load identity, build context

A middleware validates the JWT — signature, expiry, **pinned algorithm** (reject `alg: none`), a
strong **rotated** signing key, and **real revocation / short-lived tokens + refresh** so a suspended
user's token stops working promptly. If anything fails, stop and return **401**.

On success, read **`is_active` fresh from the `user` row** — one DB read per request — so a disabled
account is judged on what's true now, never on a stale token. Then build the request **context**:

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

**Early guard.**

```
if context.isActive = false                 →  403   -- kill switch: off everywhere
```

**The single authorization query.** One query decides the rest — does the user hold a live role with
the required permission, reaching this place, and is that place inside their org — with the boundary
baked in so it can't be skipped:

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

    -- the membership is at the place the route addresses, inside the caller's org
    AND ( this is an ORG action   →  membership.organization_id = context.organizationId

          this is a STORE action  →  membership.store_id = {storeId from the path}
                                     AND store(storeId).organization_id = context.organizationId

          -- OR an org membership may perform a store action on any store in its org:
          OR ( membership.organization_id = context.organizationId
               AND store({storeId}).organization_id = context.organizationId ) )
```

No matching row → **403 Deny** (deny by default). A `storeId` in another org produces no row (its
`organization_id` won't match the token's), so a foreign store is denied by the same query — there's
no separate boundary step to forget.

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

### Resource scoping (IDOR) — handled by the query, not a separate check

When an action names a specific resource by id (the order to refund, the product to edit), we **don't**
load it and then compare its tenant. Every such query is **scoped to the place in the path**, so a
foreign resource is simply never found:

```
UPDATE / SELECT ... WHERE order_id = {orderId} AND store_id = {storeId}
  → no row → 404
```

An order belonging to another store doesn't match `store_id = {storeId}`, so it returns **404** with no
special handling — the isolation is the `WHERE` clause, not something a developer must remember after
loading. For an **organization** action the same idea scopes to the org (`... AND organization_id =
context.organizationId`). **Every endpoint that takes a resource id must do this.**

### Making it impossible to forget

The protections above only work if applied on *every* endpoint, so we make the system refuse to run
without them:

**1. Deny-by-default routing — a route must declare its permission, or it's blocked.**

- *FastAPI* — a custom router whose registration method takes `permission` as a **required argument**
  (adding a route without one is a Python error); a **boot-time scan** that refuses to start if any
  route lacks the auth marker, with a small greppable `@public` allowlist for login/health; and the
  auth dependency that runs the query at request time.
- *ASP.NET Core* — a global **`FallbackPolicy`** (`RequireAuthenticatedUser`) so any endpoint with no
  authorization attribute is denied by default (public routes need explicit `[AllowAnonymous]`); the
  `resource:action` permission maps to a named policy via a dynamic `IAuthorizationPolicyProvider`;
  and a single fail-closed `AuthorizationHandler` where `context.Succeed()` is the only way to allow.

**2. Row-Level Security — the database refuses foreign rows.** Even if a hand-written query forgets its
`store_id` / `organization_id` filter, Postgres RLS filters it out. RLS is set per request from the
verified context (`app.current_org`, and `app.current_store` for store actions) and applies to every
query automatically. Query scoping is the first line; RLS is the can't-be-wrong backstop. See
`database.md` for the setup.


# How real systems do this

This model — identity placed by membership, roles typed by level — is the mainstream industry pattern.
Almost no major platform puts a "type" on the *person*; access comes from **roles bound to a place (a
scope)**. The names worth knowing:

**Kubernetes RBAC — the closest match.** A user has *no type at all*. Access is a **RoleBinding**.
Kubernetes ships the same role in two flavors: a `Role` (acts in **one** namespace) and a
`ClusterRole` (acts **everywhere**) — the type rides on the role, the user stays plain. That mirrors
us exactly: store roles vs org roles, attached to store vs org memberships.

**Google Cloud & Azure — scope lives on the grant.** In GCP IAM you grant a role *at a node in a
hierarchy* (organization → folder → project → resource); at the project it scopes there, at the org
node it inherits down to everything. Azure RBAC is the same (assignments carry a *scope*). Our
store-membership vs org-membership is the same shape: a grant low in the tree vs at the top. No user
type anywhere.

**AWS IAM.** A principal has no type; what they can do is policies, each naming a **Resource** (the
scope). Account/org-only powers are handled *structurally* by **AWS Organizations + SCPs** above the
accounts — the same shape as our elevated-permissions-only-in-org-roles rule. "Org-only" is a boundary
above, not a flag on the user.

**Multi-tenant SaaS (GitHub, Slack, Stripe, Shopify) — closest to our domain.** One identity, many
memberships, role on the membership. A GitHub user belongs to many orgs and teams; "org owner" vs
"repo admin" is the *same person* holding memberships at two levels — not two user types. Shopify Plus
separates an **Organization** admin from **store staff** by *which membership you hold*.

**Where each of our pieces lands:**

| Our piece                                      | Industry name                          | Who does it                       |
|------------------------------------------------|----------------------------------------|-----------------------------------|
| Membership = *where* you can act               | scoped role binding / grant-at-scope   | Kubernetes, GCP, Azure, AWS — all |
| Role typed by level (store vs org)             | `Role` vs `ClusterRole`                 | Kubernetes                        |
| Reach follows the membership it's bound to     | hierarchy node / binding scope         | Kubernetes, GCP, Azure            |
| `is_elevated` (org-only actions)               | account/org-level actions, bounded above (SCPs, ClusterRole-only verbs) | AWS Organizations, Kubernetes |
| No type on the **person**                      | the norm                               | Kubernetes, GCP, Azure, AWS, SaaS |

The takeaway: keep the *scope on the binding* (our membership) and a *type on the role* (so roles stay
self-describing and the UI can filter), and there's no need for a type on the person — the membership
already places them.
