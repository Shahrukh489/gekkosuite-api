# Post-MVP

Things intentionally deferred. Captured so we don't hard-code them as one-off behaviors and
have to claw them back later.

# Org-wide sharing of products and customers

In MVP, products and customers are **store-owned** — each store keeps its own, isolated from other
stores. This is the simplest, safest default. Some organizations will want the opposite: **one shared
catalog and/or one shared customer base** across all their stores (a customer recognized at any store,
a product carried by several).

The plan: an **org-level setting** to turn on sharing — per entity (customers, products, or both).
When on, an item created at one store can be promoted to org-wide visibility. Promotion is **gated by
org approval**: the change raises a notification an org admin approves (or the org configures
**auto-approve** to skip it). Open sub-questions for then:

- **Dedup at share time** — matching the same customer (phone/email) or product (SKU) across stores so
  sharing links rather than duplicates.
- **Granularity** — all-or-nothing vs. per-entity (share customers but not catalog).
- **Direction** — does sharing make a single org-owned record, or keep store records linked into a
  shared view.

This is deliberately deferred: build it when an organization actually needs cross-store sharing, not
before.

## How shared customers work (the safe pattern)

When an org turns sharing on for customers, a store user can create/edit a customer that **all** the
org's stores see. This does **not** need any new actor-type branching — it's the same flat permission
check plus the org's setting. The rules:

1. **The org admin enables it.** A `customer:create` request from a store user is only allowed to write
   a *shared* (org-level) customer if the org's **share-customers setting is on**. Off (the default) →
   the customer stays store-owned. The setting is the switch; the user's type doesn't change.

2. **The permission must NOT be elevated.** `customer:create` stays a normal (non-elevated) permission
   so it can live in a **store role**. (If it were elevated, store users couldn't hold it at all — which
   is the opposite of what shared-customers wants.) So the only thing gating store users from customers
   is whether their role includes the permission — not the permission's level.

3. **The check at the endpoint** is just two things — no branch on `user_type`:
   ```
   allow create/edit customer if:
     a. the user holds customer:create (or customer:edit) via a LIVE role, AND
     b. customer.organization_id == context.organizationId      (tenant boundary)
   ```
   That's it. A store user with the permission qualifies; the customer must be in their own org.

4. **`organization_id` comes from the token, never the request body.** A new customer's
   `organization_id` is set server-side from `context.organizationId`. A store user can therefore only
   ever create a customer in *their own* org — they can't pass a different org id to write into another
   tenant. This single rule is what makes the flat check safe.

5. **RLS on `customer` follows the mode:**
   - **Shared mode (setting on):** `customer` is org-scoped — the RLS policy filters on
     `app.current_org` (`organization_id = current_setting('app.current_org')`). Any store user in the
     org sees the shared customers.
   - **Isolated mode (setting off, the MVP default):** `customer` is store-scoped — the policy filters on
     `app.current_store` (`store_id = current_setting('app.current_store')`), so a store user sees only
     their store's customers.

   So the table carries both `organization_id` (always — the tenant boundary) and `store_id` (the owning
   store in isolated mode), and the RLS policy shape is chosen by the org's sharing setting.

The same pattern applies to shared products (an org-wide catalog) — flat permission + the org's setting
+ org-from-token + org-scoped RLS when sharing is on.


# FEATURE:  Storefront and CRM
Get a storefront online and manage products via this CRM

# FEATURE: AI based creator like lovable but create home page, seller store and get CRM access 
and create all products and employees easily


# FEATURE: Customer login portal


# FEATURE: ABAC Conditions on a permission (limits like "refund up to $500") — stretch goal (maybe MVP)

A role's permission can carry **conditions** — limits checked at the moment the action happens. The classic case: a Cashier *may* refund, but only up to $500; a Manager up to $5000. Same permission (`order:refund`), different limit per role. This is lightweight ABAC layered on the RBAC, and it may be the thing that **replaces custom roles** (see `post-mvp.md`): customers tune limits on the roles we ship rather than authoring roles.

### Two tables: a menu we ship, and the customer's chosen values

- **`permission_condition`** — the **menu**. *We* define which conditions a permission may have. Each entry is a `field` + `operator` + a display `label`. Example entries for `order:refund`:

  ```
  permission_condition
   permission     field      operator  label
   ------------   --------   --------   --------------
   order:refund   amount     <=         Refund cap
   order:refund   age_days   <=         Days to refund
  ```

- **`role_permission_condition`** — the customer's **value**, pointing at a menu entry. Tenant-scoped (`organization_id`, optional `store_id`). The FK to `permission_condition` means a customer can only set a value for a condition we allow — they can't invent a `field` or `operator`.

  ```
  role_permission_condition
   org     store   role      → permission_condition   value
   ------   -----   -------    --------------------    -----
   Org 7   (all)   Cashier    order:refund / amount    500
  ```

  Reads as: "for Org 7's Cashiers, refunds are capped at $500." `store_id` lets one store override (most-specific-wins: store row beats org row).

### How the customer sets a limit

1. The editing screen reads `permission_condition` for `order:refund` → shows "Refund cap (0–1000)".
2. The admin picks the **role** (Cashier) and types **500**.
3. Insert `role_permission_condition (Org 7, Cashier, → that menu entry, value=500)`. The value must be within the menu's `min/max`.

### How it's enforced at runtime

The refund request carries the order, **not** a condition id — so we find conditions by the **action**, then check them:

```
cashier refunds a $600 order →
  1. RBAC:  does the user hold order:refund here?            → no → DENY
  2. find role_permission_condition for (role, org/store, order:refund)
            → none? unconditional → ALLOW
            → found: join permission_condition → field='amount', operator='<=', value=500
  3. ABAC:  evaluate dto[field] operator value  →  dto.amount(600) <= 500  → false → DENY
```

One generic evaluator: `dto[field]  <operator>  value`. **No per-condition code** — `field`/`operator` come from the menu, `value` from the customer's row.

**The field→value mapping is just the name.** `field = 'amount'` reads `dto.amount` — the condition's `field` is the same name as the DTO/entity property. So map the API request to a DTO/entity whose field names match the menu's `field` names, and the lookup is direct. **Fail closed**: if a condition's `field` has no matching DTO property at runtime, **deny** — never skip a limit.

### Who can edit limits — org admin only

**Only an org admin edits condition values** (gated by the `role_condition:edit` permission, which only org-level roles hold). They set a store's limits and scope each one via `role_permission_condition.store_id` (NULL = all the org's stores, set = that store).

A **Store Admin cannot edit limits** — if a store manager wants a higher cap for their employees, they ask the org admin. This keeps it simple and **structurally removes any self-escalation risk**: the only editors are org-level, always setting limits *down* to a store, never their own. (If store-admin self-service is ever wanted, it's an additive change — grant `role_condition:edit` to a store role and add per-permission bounds so they can only move within an owner-set ceiling — but we deliberately don't do that now.)


# FEATURE: Custom Roles

 **Escalation on role *assignment*** (when admins delegate / custom roles arrive) — no
  rule prevents an admin granting a role more powerful than their own, or handing out
  `role:assign` to bootstrap full power.

  **Solution (the bullet-proof, all-cases rule):** a person can only grant a role whose
  permissions are a **subset of their own effective permissions**. This single rule closes
  every escalation path (grant-stronger-role, self-assign, custom-role-stuffing, same-level
  grabs) with no special-cased roles and no maintained "dangerous roles" list — anything
  owner-equivalent automatically fails because it contains powers the assigner lacks.

  ```
  grant_role(assigner, target_user, role R, place P):
    require assigner has `role:assign` effective at P            # may assign here at all
    require permissions(R) ⊆ effective_permissions(assigner @ P)  # can't grant beyond self
    # self is NOT exempt — assigner == target uses the same check
    then write membership_assignment
  ```

  Two things make it actually bulletproof:
  1. Compare **effective** permission *sets* (resolved, incl. org→store inheritance) — not
      role names or a "role rank". Set ⊇ set, computed via the same `can()` resolution.
  2. Check **at grant time, server-side, in the same transaction** that writes
      `membership_assignment` — never trust a precomputed "assignable roles" snapshot.

  This subsumes the simpler "only an owner may grant owner/admin" idea, so we don't need a
  separate elevated-role flag. Build this when delegation / custom roles ship.





# FEATURE: Workflow / event engine

A future event-driven engine (org-admin-configurable from the UI): **event → condition → action**.
More flexible than fixed schema for cross-store and reactive behaviors. Note: this is a real,
substantial subsystem (event sources, triggers, conditions, actions, execution log,
idempotency, failure handling) — design it deliberately when the need is concrete, not bolt-on.

Initial ideas:
- If store X sells something, reduce store Y's quantity (cross-store inventory sharing —
  the online-store-deducts-physical-store case). Replaces the idea of a fixed
  `inventory_group` / shared-stock schema; it's just one workflow rule.
- Look at ultraAIO.

## Automations to build as workflows (not bespoke code, not schema)

These are all **"when X happens, also do Y"** — they belong in the workflow engine, not
scattered as one-off handlers:

- **Default store on onboarding** — `organization.created` → create a starter store. (The
  *constraint* "an org needs a store to sell" stays a rule; the auto-creation is a workflow.)
- **Managed-role assignment heads-up** — when an admin assigns a managed (`is_managed`) role,
  notify them its permissions may change over time. (`role.assigned` → notify.)
- **Customer dedup / merge** — only relevant once org-wide customer sharing is on (see *Org-wide
  sharing* above): when a customer is added at a store, match by phone/email to link an existing
  shared customer vs. create a new one. (`customer.create_attempt` → find-or-link.)
- **Retention / cleanup** — scheduled: hard-delete soft-deleted memberships after N days;
  deactivate users with zero active memberships. (Scheduled workflows.)
- **Plan-change side effects** — provisioning / welcome email / feature toggling when a store
  changes plan. (The feature *derivation* through the plan stays structural; the *reactions*
  are workflows.)
- **Role clone** — copy a built-in role + its permissions into a new custom role. (A templated
  multi-step action; fits the action library later. Low priority.)

