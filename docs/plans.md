## Overview

- An **organization** subscribes to **plans** (usually one, e.g. "Basic" or "Pro"). The link is a
  **`subscription`** row (org → plan) with a billing status; there are no per-store plans.
- A plan has a **price per store** and a set of **features** (reports, returns, billing, multi-store, ai, etc.).
- **An org can hold more than one live subscription** — e.g. a paid Basic plus a free Pro trial. When it
  does, the org's **effective features are the union** of all its live subscriptions' plans (the Pro
  trial adds Pro's features on top of Basic). Most orgs have just one.
- **Each feature has a scope — `STORE` or `ORGANIZATION`** (see the `feature` table in `database.md`). This decides *where* it applies:
    - **`ORGANIZATION`** features are org-level capabilities (billing, multi-store, cross-store reports). They apply to the organization itself.
    - **`STORE`** features are store-level capabilities (returns, AI recommendations, store reports). Every store in the org gets the plan's store-scoped features.
    - A capability that's needed in *both* places is two separate features — one per scope.
- **New features spread automatically.** Features are read through the plan, so adding or removing a feature on a plan instantly updates every organization subscribed to that plan (and its stores, for store-scoped features).
- The **bill = the plan's per-store price × the number of stores** (per paid subscription).

```
Organization → subscription(s) → Plan (price per store + features)
                                     │
        ┌────────────────────────────┴───────────┐
        │                                        │
   ORGANIZATION-scoped features              STORE-scoped features
   (billing, multi_store, ...)               (returns, ai, reports, ...)
   apply to the org                          every store gets these

Pro plan: $150/store
  ORGANIZATION features: [billing, multi_store, cross_store_reports]
  STORE features:        [reports, returns, ai_recommendations]
Org has 3 stores →
  the org has the org-scoped features;
  each of the 3 stores has the store-scoped features.
Bill = 3 × $150 = $450
```


## Features vs Permissions

These are two separate gates, and it's important not to confuse them. A screen or action is shown only when **both** pass:

- A **feature** answers *"does the org's plan include this capability?"* — a **billing** decision, the same for everyone in the org. It comes from the plan.
- A **permission** answers *"is this particular user allowed to do it?"* — a **per-user** decision, from the role on their membership.

So the feature turns a capability **on** for the whole org (or store); the permission decides whether **you** specifically may use it. You need both.

**Example — the Returns tab at a store.** It shows only if:

1. the plan includes the `returns` feature (store-scoped), **and**
2. the user's role grants the `order:refund` permission.

| Plan has `returns`? | User has `order:refund`? | Returns tab |
|---|---|---|
| ✅ | ✅ | **shown** |
| ✅ | ❌ | hidden — the user can't refund |
| ❌ | ✅ | hidden — the plan doesn't include returns |
| ❌ | ❌ | hidden |

**A few people to make it concrete** (org is on the Pro plan above):

- **Sara — Cashier at Downtown.** Her store has features `[reports, returns, ai_recommendations]`, but her role only grants `[product:read, sale:create]`. She sees **Sell** and read-only **Products**. No Returns tab — the plan has returns, but she lacks `order:refund`. She never sees `billing` or `multi_store` at all; those are org-scoped and a store never returns them.

- **Marcus — Manager at Downtown.** Same store features, but his role grants `[product:read, sale:create, order:refund, product:edit]`. He sees **Sell, Products (editable), Returns, Reports** — both gates pass for each.

- **Diego — Org Admin.** In the org context his features are `[billing, multi_store, cross_store_reports, user_management]` and his role grants the matching org permissions, so he sees **Billing, Users, Stores, Cross-store Reports**.

**Why both gates matter — a downgrade.** Say Acme drops from Pro to Basic, and Basic doesn't include `returns`. Nobody's *permissions* change — Marcus still has `order:refund` on his role. But his store's features no longer include `returns`, so the **Returns tab disappears for everyone**. The plan controls whether a capability exists at all; the role controls who may use it.

**Scope keeps them in the right place.** Store screens are driven only by a store's features and permissions, and org screens only by the org's — so a cashier can never see org-only features like `billing`, because those are never returned by a store endpoint.


## Billing & store count @TODO

The bill is **per-store**: `plan's price per store × number of active stores`. So the moment a store is
added or removed, the amount owed changes. This section states, once, how that flows through the API and
Stripe — every endpoint that changes the store count (`POST /stores`, `DELETE /stores`) just references
it rather than repeating the money logic.

**The one rule: the billed quantity always equals the org's active store count.**

```
billed quantity  =  COUNT(*) FROM store WHERE organization_id = ? AND is_deleted = false
```

**Stripe owns the money; we own the count.** We never compute a charge, proration, or credit ourselves.
Each paid subscription maps to a Stripe subscription item whose **quantity** is the store count above.
When the count changes, we update that quantity and Stripe does the rest — it **prorates automatically**
(charges the partial-period cost of a new store, credits a removed one).

**On create (`POST /stores`)** — inside one flow:
1. Insert the `store` row (`is_deleted = false`).
2. Recount active stores → new quantity.
3. Update the Stripe subscription item's quantity to the new count. Stripe prorates the addition.

**On delete (`DELETE /stores`)** — same, in reverse: soft-delete the store, recount, lower the Stripe
quantity; Stripe credits the proration.

**Stripe failure does not fail the store operation.** If the Stripe quantity update errors (outage,
transient), the store create/delete still succeeds — a Stripe hiccup must not block running the business.
The mismatch is caught by reconciliation.

**Reconciliation is the safety net.** Because the rule is a simple equality (`Stripe quantity == active
store count`), a nightly job re-counts every org's active stores and corrects any Stripe quantity that
drifted. This makes the per-request Stripe call best-effort: even if it's dropped, the count self-heals
within a day. Store count in our DB is the source of truth; the Stripe quantity is a mirror of it.

**Note — this is separate from the read-only billing gate.** Changing the store count changes *what the
org owes*; it does not decide whether the org is *frozen*. Whether the org can write at all is the
subscription `status` (`UNPAID` / `CANCELED` → read-only), enforced by `auth.md`'s billing gate. Adding a
store raises the bill; not paying that bill is what eventually flips the org read-only.

> Schema note: the `subscription` table will need a Stripe reference (e.g. `stripe_subscription_item_id`)
> to target the quantity update. Not yet in `database.md` — TODO when the Stripe integration is specced.



