# Post-MVP

Things intentionally deferred. Captured so we don't hard-code them as one-off behaviors and
have to claw them back later.

# Custom Roles



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
