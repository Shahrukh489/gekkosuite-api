# To Be Decided

Open design questions captured but not yet resolved.

---

## Store users writing org-level (shared) resources

**The problem**

Some resources are **org-level / shared** (customer, supplier, catalog product) — they're keyed by
`organization_id`, not `store_id`, and every store in the org sees them. But a **store user** (a
membership at one store) sometimes legitimately needs to create one — e.g. a cashier adds a new
customer at checkout.

This exposes a mismatch the current authz flow doesn't fully handle:

- A store user acts *at a store*, but the row they create lives *at the org*.
- Org-level routes (e.g. `POST /organization/customers`) carry **no `storeId`** in the path, yet the
  authz query's store-user branch matches on `membership.store_id = {storeId from path}` — so it has
  no store to pin them to.
- "Org-level resource" (where the row lives) is **not** the same as "org-only action" (`is_elevated`):
  customers are org-level data but a store user *should* be able to add one; the catalog is org-level
  data a store user should *not* touch.

**Note: this gap was *exposed*, not *caused*, by the recent model changes.** The same mismatch (a
store user writing an org-level row) existed in the old header-based design — it was silently absorbed
because the target was always a store, and an org-level row just took that store's org. Two changes
surfaced it: (1) **path-based routing** removed the store target that used to absorb it (an org route
has no `storeId`), and (2) `user_type` + `is_elevated` gave us the vocabulary to ask "should a store
user reach this org resource?" at all. The strict `user_type = role.user_type` match is **not** the
cause — the customer case satisfies it fine (a store user holds a *store* role with the non-elevated
`customer:create`). `is_elevated` is actually the *solution* (allow customers, block catalog). The one
genuine rigidity `user_type` did add is unrelated: a user can no longer be *both* org and store.

**Possible solutions**

1. **Permission decides, three authz cases (lightweight).** Keep permissions as scope-free verbs;
   `is_elevated` gates whether a store user can reach an org resource at all. Add a third case to the
   authz query:
   - org user, any resource → org membership holds the permission.
   - store user, store-level resource (storeId in path) → membership at *that* store holds it.
   - store user, org-level resource (no storeId) → *any* live store membership of theirs holds the
     (non-elevated) permission; the new row gets `organization_id` from the verified context.
   So `customer:create` (not elevated) → store user allowed; `product:create` (elevated) → blocked.
   Minimal change, keeps "add a customer at the register" instant. Matches the cooperating-single-
   company niche.

2. **Store-local tables + org-approval queue (heavier).** Store users write only to store-local tables;
   promoting anything into the shared org tables requires an **org admin to approve** a pending request
   (notification → approve → write to org table). Explicit and governed, but: blocks instant checkout
   (cashier waits for HQ to approve a customer), is a real subsystem (pending state, notifications,
   approval UI, dedup-on-approval, audit), and is a low-trust / franchise pattern that fights the
   "tightly-coupled single company, simple over flexible" niche. Better suited as a future option if we
   ever serve lower-trust customers.

3. **Resource-scoped permissions (AWS-style).** Permissions carry resource selectors ("edit only
   customers my store touched"). Most granular, but heavy ABAC machinery, hard for non-technical admins,
   and undoes the deliberate scope-free simplification. Reuses the same mechanism as the deferred
   conditions/ABAC feature in `post-mvp.md`. Only worth it if per-resource limits are ever genuinely
   needed.

**Leaning:** option 1 (small authz fix), with `is_elevated` already blocking the sensitive org writes
(catalog, suppliers). Options 2 and 3 recorded as upgrade paths if the trust model ever changes.
