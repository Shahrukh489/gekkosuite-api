# Architecture Review — POS Multi-Store SaaS

Reviewer assessment of the design across `database.md`, `auth.md`, `tenancy.md`, `plans.md`,
`post-mvp.md`. This reviews the **design and data model** — there is no application code yet,
so runtime/security/ops domains are rated on what is *specified*, not *implemented*.

**Overall: 8.5 / 10** — a clean, coherent, deliberately-scoped design. Strongest traits:
disciplined scope-matching to the niche, and a genuinely well-normalized data model. It is not
a 10 because several critical paths (authentication, authorization *enforcement*, audit,
testing/ops, the API contract) are still TODO or unspecified — real work, not polish.

---

## 1. Data Model & Schema — 9/10

**Strengths**
- Clean tenant hierarchy `organization → store`; org-level shared entities (`product` catalog,
  `customer`, `supplier`) with per-store overrides (`product_store` for qty/price). No
  duplicated catalog, no drift.
- Correct money modeling: `NUMERIC(12,2)`; price/cost **snapshots** on line items
  (`unit_price`, `unit_cost`) so historical totals are immutable.
- Supertype/subtype split (`membership` + `*_membership_detail`) avoids NULL-filled rows while
  keeping the hot table lean.
- Thorough explicit FK indexing (Postgres doesn't auto-index FKs) + useful composites.
- Polymorphic place reference via two nullable FKs + XOR CHECK keeps real FKs and DB-enforced
  integrity instead of a type-string.

**Weaknesses**
- `expense` is dual-shaped (store OR org via nullable `store_id`); the "store must belong to
  `organization_id`" rule is **write-path only**, not a DB CHECK — the one integrity rule the
  DB can't enforce alone.
- Circular FK `organization.default_store_id ↔ store.organization_id` — handled, but an
  onboarding-order gotcha.
- Several status fields are free `TEXT` (`sales_order.status`, `purchase_order.status`) with no
  CHECK/enum — drift risk.

**Suggestions**
- Add CHECK or enum to status columns once states stabilize.
- Consider a composite-FK trick or trigger to enforce `expense` store↔org consistency if it
  ever matters more.

**Missing**
- No `created_at`/`updated_at` on most tables (only some). For audit/debugging, near-universal
  timestamps help.
- Soft-delete is only on `membership`; decide whether orders/customers ever need it.

**Why 9 not 10**: one un-enforceable integrity rule (`expense`), free-text statuses, and
uneven timestamps — small but real gaps in an otherwise excellent model.

## 2. Authorization (RBAC) Model — 9/10

**Strengths**
- Final shape is clean and standard: `permission` = `resource:action` (scope-free, explicit,
  no wildcards); `role.scope` (STORE/ORG) carries level; place comes from the `membership` the
  role attaches to; reach = role.scope × place (org role → all org's stores, expanded on demand
  via `store.organization_id`).
- Legit scoped RBAC (K8s Role/ClusterRole, Azure role-at-scope family), correctly simplified to
  two fixed levels. Scope-on-role is the right trade for no-nesting.
- "Store user can never get org access" is **structural** (org role needs an org membership;
  only org admins create those), not a bypassable guard.
- No wildcards → no silent privilege creep when permissions are added later.
- Custom-role infra pre-laid (`is_managed`, `organization_id`, `scope` + CHECK) → additive,
  no migration.

- `permission.scope` (STORE/ORGANIZATION) guards which role scopes may hold a permission —
  STORE permissions go in store or org roles, ORGANIZATION permissions only in org roles. This
  pre-closes the privilege-escalation gap for future custom roles (a store role can't be given
  an org-only power like `store:create`).

**Weaknesses**
- The blast-radius resolution (org role expanding over all stores) is described but exists only
  as prose — its correctness depends entirely on one not-yet-written `can()` function.

**Suggestions**
- Specify the canonical `can(user, permission, target)` algorithm in `auth.md` as pseudocode,
  not just examples.

**Missing**
- No role/permission *seed list* — the actual managed roles and the full permission catalog
  aren't enumerated anywhere.

**Why 9 not 10**: the model is right but its enforcement (`can()`) is unwritten, and the seed
roles/permissions catalog is missing.

## 3. Authentication — 4/10 (specified only)

**Strengths**
- Correctly separated from authorization; `auth.md` Security TODO names the real requirements.

**Weaknesses / Missing (all unbuilt)**
- No `password_hash` column or hashing choice (argon2id/bcrypt).
- No session/token model (type, expiry, rotation, revocation, storage).
- No brute-force protection (rate limit/lockout), no MFA (esp. for org admins handling funds),
  no password-reset flow, no login auditing.

**Suggestions**
- Promote the TODO into a real spec before any build: hashing, sessions, MFA-for-admins,
  reset, lockout.

**Why 4 not higher**: this is where most breaches happen and almost nothing is specified yet.
It's listed, not designed.

## 4. Security & Enforcement — 6/10 (model sound, enforcement unbuilt)

**Strengths**
- The authz *model* is secure by construction (structural, deny-able, no wildcards).
- The right invariants are identified in `post-mvp.md` ("never allow X" vs "when X do Y").

**Weaknesses / Missing**
- Server-side, per-request, deny-by-default enforcement is **implied, not stated as law**.
- Tenant isolation / IDOR prevention (derive tenant from session; verify every resource belongs
  to the caller's place) is TODO — the classic multi-tenant breach vector.
- The `deleted_at IS NULL` + `is_active` access filter is load-bearing and easy to forget; not
  yet centralized in one resolution function/view.
- No privilege-escalation guard (granting a role above your own).

**Suggestions**
- State the enforcement law explicitly; centralize access resolution; add tenant-scoping to
  every query; add an escalation guard.

**Why 6 not higher**: correct model, but the controls that make it *secure in practice* are all
still open.

## 5. Scalability & Performance — 8/10

**Strengths**
- Monolith + single Postgres is the correct, justified call for the stated scale (10k orgs /
  50k stores / 250k users, ~hundreds req/s). Microservices/Mongo rejected for the right reasons.
- Sound documented escalation: read replicas → cache → read model for org-wide views → shard by
  `organization_id` (clean tenant boundary).
- Indexing supports the hot paths.

**Weaknesses**
- Org-wide "see everything" reads and org-role→all-stores expansion degrade first; mitigation
  (read model) is described but not designed.
- Audit log (when added) will be the fastest-growing table; partitioning noted but not speced.

**Missing**
- No concurrency story for the balance-deplete transactions (`purchase_balance`/`expense_balance`)
  under simultaneous purchases — needs row locking / serialization to avoid oversell.

**Why 8 not higher**: the scaling path is right but unbuilt, and balance-deplete concurrency is
an unaddressed correctness-under-load risk.

## 6. Simplicity & Reusability — 9/10

**Strengths**
- Simple where it counts: managed-only roles, scope-free permissions, derived billing, no
  franchise walls. Each is a deliberate "don't over-engineer" call.
- Strong reuse: org-shared catalog/customers/suppliers; one role bundle reused across stores;
  the `resource:action` simplification removed a whole class of decisions.
- Deferral discipline (`post-mvp.md`) keeps MVP lean while recording the upgrade path.

**Weaknesses**
- A few `@TODO` stubs remain in `tenancy.md` FAQs.

**Why 9 not 10**: essentially as simple as it can be for the domain; only loose ends are doc
stubs.

## 7. Extensibility — 8/10

**Strengths**
- Clean growth seams: workflow/event engine for cross-store automations; custom-role columns
  pre-laid; regions/districts as tags promotable to entities; payments as a future integration
  (correctly not building a money-mover now).
- Accountant-ready money model (sales in / COGS / opex, per-store rolling to org).

**Weaknesses**
- The workflow engine is a large unbuilt subsystem that several deferred features depend on —
  concentration risk if it slips.

**Why 8 not higher**: great seams, but much future capability hinges on one big unbuilt engine.

## 8. Documentation Quality — 8.5/10

**Strengths**
- Focused docs (tenancy/auth/plans/db/post-mvp) + architecture index; first-reader-friendly
  FAQs; unusually good "why" comments in the schema.

**Weaknesses / Missing**
- `@TODO` stubs in tenancy FAQs; `database.md` no longer has its ER-diagram/query sections
  (only DDL + indexes) yet other docs still imply queries exist.
- No single glossary of the core entities in one place.

**Why 8.5 not higher**: minor staleness and missing diagrams/glossary.

---

## Domains previously not broken out

## 9. API / Interface Contract — 2/10 (essentially absent)
- `openapi.json` is effectively empty; there is **no API surface design** — endpoints, request/
  response shapes, error model, pagination, idempotency keys for money ops.
- **Suggestion**: define the API contract (at least for auth, RBAC checks, and the money
  write-paths) before building. The "API aggregates flat query rows" decision is stated but no
  endpoints exist.
- **Why 2**: a SaaS needs an API; none is specified.

## 10. Testing & Verification — 1/10 (not addressed)
- No test strategy, no fixtures, no statement of what "correct" means for the money invariants
  (balance deplete, snapshot pricing) or the authz rules.
- **Suggestion**: specify invariant tests (authz `can()` truth table; balance-deplete under
  concurrency; tenant-isolation negative tests) as first-class.
- **Why 1**: untouched, and the money + authz paths are exactly what must be tested.

## 11. Operability / Observability — 2/10 (not addressed)
- No logging/metrics/tracing plan, no migrations strategy, no backup/restore/DR, no
  environment/config story. Audit log is TODO.
- **Suggestion**: add an ops section (migrations tool, backups, request tracing — the audit
  TODO already wants correlation ids).
- **Why 2**: necessary for a money system, currently absent.

## 12. Data Integrity & Money Correctness — 7/10
- **Strengths**: price snapshots, single-source-of-funds, balance-deplete-in-one-transaction
  rule, COGS/opex separation — the *intent* is correct and accountant-ready.
- **Weaknesses**: the critical money invariants live in prose ("insert + decrement in one
  transaction; reject if over balance") with no concurrency/locking spec and no DB-level
  guarantee; no double-entry/ledger for auditability of balances; refunds/returns don't yet
  restore stock or balances in the model.
- **Why 7**: the model is right but the hardest part (money correctness under concurrency,
  reconcilable balances) is specified only at the prose level.

---

## Honest bottom line
- **Good design?** Yes — clean, normalized, consistent, impressively scoped.
- **Scalable / reusable / simple?** Yes — among the strongest dimensions, with a correct growth
  path.
- **Secure?** The *model* is; the *system* is not yet (authn, enforcement, tenant isolation,
  audit are TODO).
- **Can it do many things and more?** Yes — strong extensibility seams; deferral discipline lets
  it grow without rewrite.
- **Why not 10 overall?** Because a 10 means nothing critical is missing, and several
  load-bearing paths — authentication, authorization *enforcement*, money-under-concurrency,
  API contract, testing, ops — are still unwritten. That's substantial real work, not nitpicks.
  As a *design document* it's an 8.5; as a *shippable system* it isn't there yet, by design.

### Top priorities to raise the score
1. Authentication spec (hashing, sessions, MFA-for-admins, reset, lockout).
2. One canonical server-side `can(user, permission, target)` — deny-by-default, tenant-scoped,
   centralizing the `deleted_at`/`is_active` filter.
3. Money correctness: concurrency/locking for balance-deplete; consider a balance ledger;
   define refund→stock/balance restoration.
4. API contract + invariant test suite (authz truth table, tenant-isolation negatives,
   balance-deplete under load).
5. Ops basics: migrations, backups, audit log + tracing.
