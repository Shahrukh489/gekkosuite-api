# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.


# What GekkoSuite is

A **multi-store point-of-sale (POS)** system for a *single company* that runs many stores (not
franchises; ~50–100 stores; non-technical users). The guiding principle is **simple over flexible**.

Two levels:
- **Organization** = the business and the **tenant** (the unit of isolation). Owns suppliers,
  purchasing, expenses, users, stores, and the sharing settings. Money leaving the business is always
  an org action.
- **Store** = the real **business unit** where selling happens. Owns its products (own stock + price),
  customers, sales, and returns. A store is *not* a tenant — it's a resource the org owns that you can
  also scope a person's access to.

By default each store is isolated. An org can turn on **sharing** per entity (`share_products` /
`share_customers`): a product/customer created at one store then also writes a shared org-level record,
so it's recognized org-wide — but **stock and price always stay per-store**. This is why products and
customers each use **two tables**: a store-level one (`store_product`, `store_customer`, always
written) and a shared org-level one (`product`, `customer`, written only when sharing is on).

# The access model (the heart of the design)

Read `docs/auth.md` before touching anything auth-related. The chain:

```
user ──< membership ──< membership_assignment >── role ──< role_permission >── permission
```

- **A user is just a login** — it carries no org-vs-store label. *Where* they can act comes from their
  **memberships**; *what* they can do comes from the **roles** on those memberships.
- A **membership** is either at the `ORGANIZATION` or at a `STORE`. Every membership carries
  `organization_id` (the tenant boundary). A store membership reaches its one store; an org membership
  reaches the whole org and every store in it.
- **Roles are typed** (`STORE` or `ORGANIZATION`) and must match the membership kind.
- **`permission.is_elevated`** marks company-level actions (e.g. `user:create`, `store:create`). An
  elevated permission may only sit in an org role → so it can never reach a store.

The authorization query walks **each membership independently** ("badges"): if any one membership
passes its rulebook (live + right place + right company + role grants the permission), the request is
allowed. It runs **live on every request** — roles/permissions are **never cached in the JWT**, so
revocation is immediate. The token holds only `userId` + `organizationId`.

Request pipeline: **Authenticate** (validate JWT, read `user.is_active` fresh, build context) →
**Authorize** = read-time security (permission check + IDOR resource scoping) + write-time security
(stamp tenant from token, enforce RBAC invariants). Convention: another tenant's resource returns
**404, not 403** (don't leak existence); 403 = "yours, but you lack the permission."

# Defense-in-depth invariants

Rules are guarded in up to three layers, and docs reflect this pattern — keep it consistent:
1. **Service method** (main check, clear error) — e.g. `addPermissionToRole`, `assignRoleToMembership`.
2. **Authorization query** (read-time backstop) — re-checks stored data, never trusts it blindly.
3. **Database** — RLS `USING`/`WITH CHECK` for tenant scoping; triggers for cross-row invariants a
   `CHECK` can't express.

**Row-Level Security (`docs/database.md`)** is the tenant-isolation floor below app scoping. It fails
**silently** if misconfigured, so two conditions are load-bearing: the app must connect as a
**non-superuser, non-owner** role (superusers/owners bypass RLS), and the tenant must be set with
**`SET LOCAL`** (transaction-scoped) so it can't leak across a pooled connection.

# Docs map

- `docs/overview.md` — product vision, feature list by org vs store, key design decisions.
- `docs/tenancy.md` — org/store ownership, sharing, how money flows.
- `docs/auth.md` — users, memberships, roles, permissions, the request lifecycle. **Ends with a
  "Security Review Notes" section of open items** (authentication is the weak area: password hashing,
  brute-force protection, MFA, token revocation, `store_pin`, and an escalation-ceiling rule on role
  assignment are all still unspecified). Consult it before proposing auth changes.
- `docs/plans.md` — plans, add-ons, features, billing. An org subscribes to **offerings** (a base plan +
  stackable add-ons, one table `offering` told apart by `type` PLAN|ADDON); each offering is a priced
  bundle of features. Bill = Σ live offerings' per-store price × store count.
- `docs/database.md` — schema (tables, indexes, RLS). Note: `permission_condition` /
  `role_permission_condition` are referenced in the Indexes/RLS sections but not yet defined as tables.
- `docs/ui.md` — one app, one set of screens; a store switcher sets context, memberships decide
  available contexts, permissions decide which tabs/actions show.
- `docs/post-mvp.md` — deferred features.
- `docs/returns.md` — plan for the in-store returns/RMA system: scope decisions, schema, API, and UI
  needed to process a return against a prior sale. Nothing in it is built yet; orders (`sales_order`)
  have to be migrated for real first — see its "Phase 0" section.
- `docs/customers.md` — plan for a customer data model that covers both B2C (individual) and B2B
  (business) accounts: a shared `customer` core + `customer_type`, a `customer_business_detail` table for
  business-only fields (tax ID, billing terms, credit limit), and `customer_contact` for the multiple
  people who can buy on one business account. Includes compliance notes (exemption certificates, PII
  minimization, PCI-DSS, consent tracking). Nothing in it is built yet.

When editing docs, keep the heavy cross-referencing between files intact (they cite each other by
name), and preserve the existing voice: worked examples, tables, and explicit "why this matters" notes.

# Coding style (C#)

Follow these when writing or editing code in this repo:

- **No expression-bodied members (`=>`) for methods.** Every method uses a block body with braces and an
  explicit `return` — even one-liners. (Lambdas passed as arguments are fine; this rule is about member
  bodies.)
- **Every method has a `<summary>` doc comment**, plus a `<param>` for each parameter (and `<returns>` when
  it returns a value, `<typeparam>` for generics).
- **No section-divider comments** (e.g. `// ----- store-scoped -----`). Group related methods without banners.
- **Inline comments only where the code needs explaining** — short, not verbose, and only for the non-obvious
  (e.g. the RLS `set_config` lines). Don't narrate self-evident code.
- **Keep argument lists on one line** when calling or defining a method — do not put one parameter per
  line. E.g. `await connection.ExecuteAsync("SELECT set_config('app.current_store', @store, true)", new { store = storeId.Value.ToString() });`
- **Interfaces live in an `Interfaces/` subfolder** next to their implementations (namespace unchanged).
- **Repositories** extend `BaseRepository` and pass **raw SQL strings** to its `QueryAsync` /
  `QuerySingleAsync` / `ExecuteAsync` helpers — never open connections directly (RLS tenant-stamping lives
  in `BaseRepository`).
- **DTO naming by layer** — the service always speaks in `Dto`s (both directions); `Request`/`Response`
  are the controller edges. The flow:
  `CreateUserRequest` (API in) → `CreateUserDto` (service consumes) → `UserEntity` (DB row the repo maps)
  → `UserDto` (service returns) → **optionally** `UserResponse` (API out).
    - The controller maps the inbound `Request` → service `Dto`.
    - On the way out it may return the service's `UserDto` **directly**, OR map it to a `UserResponse` when
      the endpoint needs a different/trimmed shape.
    - **Load-bearing rule:** the service-out `Dto` (`UserDto`) must contain **only client-safe fields** (no
      password hash, no internal-only flags) — that's what makes returning it directly safe. The moment an
      endpoint needs to expose something the safe `Dto` shouldn't carry, add a `Response` and map to it.
    - Input and output Dtos are distinct types (`CreateUserDto` has a password; `UserDto` never does).
    - `Entity` and a `Dto` may be one collapsed type while the DB and service shapes are identical; split
      them when they diverge.


