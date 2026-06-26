# Post-MVP

Things intentionally deferred. Captured so we don't hard-code them as one-off behaviors and
have to claw them back later.

# Custom Roles

**Consider: customer-tunable conditions may replace custom roles entirely.** Most real demand
isn't "build arbitrary roles" — it's "make *our* cashiers' refund cap $300, not $500." That's a
**condition tweak on a managed role** (`role_permission_condition`), not a new role. Letting
customers edit conditions on the roles we ship — gated by the **`role_condition:edit`**
permission (put on Store Admin / Org roles, never on Cashier) — delivers the 95% need with
almost none of the custom-role risk:
- It **can't escalate** — editing a condition only changes a *limit* on a permission the role
  already holds; it can never add a permission or cross scope. So the whole assignment-escalation
  / subset-rule problem set below does **not** apply.
- It's a **tiny surface** — edit a `value` on a condition row. No role authoring, no permission
  picker, no lifecycle.
- Optional guardrail: give each condition a min/max the editor can move *within*, so even a
  Store Admin can't set a nonsense $1,000,000 cap.

If this covers the demand, full custom roles may never be needed. The items below only matter
if we still ship full custom roles.

 [ ] **Escalation on role *assignment*** (when admins delegate / custom roles arrive) — no
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

## NOT workflows — keep these inline (recorded so they don't get moved by mistake)

The dividing line: **"the system must never allow X" = invariant (enforce synchronously);
"when X happens, also do Y" = automation (workflow).** Security and integrity are invariants.

- **Role-level / scope write checks** (a store role can't hold an org permission; a role's
  level must match the place it's granted at) — security invariants, must reject in the
  transaction, never async.
- **CHECK / XOR constraints** (membership place, role owner columns) — DB integrity.
- **`deleted_at IS NULL` filtering, permission resolution, blast-radius inheritance** —
  read-time access logic, not automation.
- **Provenance stamping** (origin org on user at creation) — one synchronous field-set.
