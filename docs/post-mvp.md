# Post-MVP

Things intentionally deferred. Captured so we don't hard-code them as one-off behaviors and
have to claw them back later.

# Storefront and CRM
Get a storefront online and manage products via this CRM

# AI based creator like lovable but create home page, seller store and get CRM access 
and create all products and employees easily


# Customer login portal


## Conditions on a permission (limits like "refund up to $500") — stretch goal (maybe MVP)

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

### Safety

- **No conditions = pure RBAC.** The ABAC gate is a no-op when there are no rows — today's behavior, unchanged.
- **We own the operator.** The customer only sets a value — a cap can't be flipped into a floor.
- **Only org admins edit values**, always scoped down to a store — so no one can raise their own limit.


# Custom Roles

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


# Changing a user's type (promote / demote)

In MVP `user.user_type` is **immutable** — set at creation, no change path. To move someone
between organization and store, you create a new user of the right type. This section is the
deferred feature that makes the type changeable in place.

**The feature:** an org admin can flip a user between `STORE` and `ORGANIZATION`.

- **Gated by a dedicated permission `user:set_type`** — held only by org-level roles. Changing
  a user from STORE to ORGANIZATION grants company-wide reach, so this permission is as powerful
  as `role:assign` to the Org Admin role and must be treated that way.
- **Audited** — every type change is a security-relevant event (it grants or removes org-wide
  reach), logged with who/when/old→new (see the Audit section in `auth.md`).
- **Owner can't be demoted** — a user who is `organization.owner_user_id` cannot be changed to
  STORE; the system refuses it (you'd be demoting the company owner to an employee).
- **Mismatched memberships are cleared on change** — because memberships must match the user's
  type, flipping the type soft-deletes the now-invalid memberships (a promoted store user's
  store memberships are cleared; the admin then adds the org membership). The UI must show a
  **warning** before the change, listing what access will be removed, so it's never a surprise.


# Store admins create store users

In MVP only an org admin can create users (`user:create`, on the Org Admin role only). Post-MVP,
a store admin can be given `user:create` too — but **restricted to creating `STORE`-type users**
(never organization users). This lets a store run its own hiring without ever being able to mint
someone with company-wide reach. The restriction is part of the create path: a store-level holder
of `user:create` may only set `user_type = STORE`.

This is **not** a separate-tenant feature — it's the ordinary create-user flow (see `auth.md`),
just scoped to the inviting store admin. It stays inside the one org tenant; a store is still a
resource, not a tenant. Key decisions for when it's built:

- **Forced `user_type = STORE`** — a store admin can never create an organization user (that's the
  escalation hole this closes). Enforced on the create path, not trusted from the request.
- **Membership locked to the inviter's own store** — the new membership's `store_id` must be a
  store the inviter holds `role:assign` at, not an arbitrary id from the request (same principle
  as the IDOR check: the target store is validated, never trusted).
- **Role subset (escalation) guard** — they can only grant a STORE role whose permissions are a
  subset of their own effective permissions (the rule under *Custom Roles* above).
- **Invite by email → find-or-link** — if the person is already a user in the org, don't create a
  second login; add a new store membership to the existing user (the multi-store case). Only
  create a fresh `user` row when no match exists. Same shape as the customer dedup workflow.
- **Don't leak the person's other stores** — because Store A can't see Store B's users, the
  find-or-link must match by email/identity without revealing whether or where that person already
  works in the org. The inviting store admin learns only "added to your store," never their other
  memberships.


## Workflow / event engine

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
- **Customer dedup / merge** — when a customer is first added at a store, match by phone/email
  to link the existing org-level customer vs. create a new one. (`customer.create_attempt` →
  find-or-link.)
- **Retention / cleanup** — scheduled: hard-delete soft-deleted memberships after N days;
  deactivate users with zero active memberships. (Scheduled workflows.)
- **Plan-change side effects** — provisioning / welcome email / feature toggling when a store
  changes plan. (The feature *derivation* through the plan stays structural; the *reactions*
  are workflows.)
- **Role clone** — copy a built-in role + its permissions into a new custom role. (A templated
  multi-step action; fits the action library later. Low priority.)

