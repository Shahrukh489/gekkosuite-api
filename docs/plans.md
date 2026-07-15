## Overview

- An **organization** subscribes to **offerings**. An offering is a priced bundle of features and comes
  in two types: a **`PLAN`** (the baseline — e.g. "Basic" or "Pro") and an **`ADDON`** (a stackable extra
  bought on top — e.g. "Marketing"). The link is a **`subscription`** row (org → offering) with a billing
  status; there are no per-store offerings.
- An org normally has **one live `PLAN` subscription plus any number of live `ADDON` subscriptions** (and
  a plan can coexist with a trialing upgrade). The org's **effective features are the union** of the
  features of *all* its live subscriptions' offerings — the base plan's features plus every active
  add-on's.
- Each offering has a **price per store** and a set of **features** (reports, returns, billing,
  multi-store, ai, etc.).
- **Each feature has a scope — `STORE` or `ORGANIZATION`** (see the `feature` table in `database.md`). This decides *where* it applies:
    - **`ORGANIZATION`** features are org-level capabilities (billing, multi-store, cross-store reports). They apply to the organization itself.
    - **`STORE`** features are store-level capabilities (returns, AI recommendations, store reports). Every store in the org gets the offering's store-scoped features.
    - A capability that's needed in *both* places is two separate features — one per scope.
- **New features spread automatically.** Features are read through the offering, so adding or removing a feature on an offering instantly updates every organization subscribed to it (and its stores, for store-scoped features).
- The **bill = the sum of every live offering's per-store price × the number of stores** (base plan +
  active add-ons). There is no separate base rate — the `PLAN` offering's price *is* the base.

```
Organization → subscription(s) → Offering (type PLAN|ADDON, price per store + features)
                                     │
        ┌────────────────────────────┴───────────┐
        │                                        │
   ORGANIZATION-scoped features              STORE-scoped features
   (billing, multi_store, ...)               (returns, ai, reports, ...)
   apply to the org                          every store gets these

Basic     (PLAN,  $50/store) → STORE: [products, sales, reports]   ORG: [billing]
Marketing (ADDON, $30/store) → STORE: [store:email:marketing]      ORG: [marketing_analytics]
Org has 3 stores, subscribed to Basic + Marketing →
  the org has the union of both offerings' org-scoped features;
  each of the 3 stores has the union of both offerings' store-scoped features.
Bill = 3 × (50 + 30) = $240
```


## Features vs Permissions

These are two separate gates, and it's important not to confuse them. A screen or action is shown only when **both** pass:

- A **feature** answers *"does one of the org's live offerings include this capability?"* — a **billing** decision, the same for everyone in the org. It comes from the offering.
- A **permission** answers *"is this particular user allowed to do it?"* — a **per-user** decision, from the role on their membership.

So the feature turns a capability **on** for the whole org (or store); the permission decides whether **you** specifically may use it. You need both.

**Example — the Returns tab at a store.** It shows only if:

1. one of the org's live offerings includes the `returns` feature (store-scoped), **and**
2. the user's role grants the `order:refund` permission.

| Offering includes `returns`? | User has `order:refund`? | Returns tab |
|---|---|---|
| ✅ | ✅ | **shown** |
| ✅ | ❌ | hidden — the user can't refund |
| ❌ | ✅ | hidden — no live offering includes returns |
| ❌ | ❌ | hidden |

**A few people to make it concrete** (org is on Basic + the Marketing add-on above; `returns` comes from Basic):

- **Sara — Cashier at Downtown.** Her store has features `[reports, returns, ai_recommendations]`, but her role only grants `[product:read, sale:create]`. She sees **Sell** and read-only **Products**. No Returns tab — an offering includes returns, but she lacks `order:refund`. She never sees `billing` or `multi_store` at all; those are org-scoped and a store never returns them.

- **Marcus — Manager at Downtown.** Same store features, but his role grants `[product:read, sale:create, order:refund, product:edit]`. He sees **Sell, Products (editable), Returns, Reports** — both gates pass for each.

- **Diego — Org Admin.** In the org context his features are `[billing, multi_store, cross_store_reports, user_management]` and his role grants the matching org permissions, so he sees **Billing, Users, Stores, Cross-store Reports**.

**Why both gates matter — canceling an add-on (or downgrading).** Say Acme cancels the add-on that included `returns`. Nobody's *permissions* change — Marcus still has `order:refund` on his role. But no live offering includes `returns` anymore, so the **Returns tab disappears for everyone**. The offering controls whether a capability exists at all; the role controls who may use it.

**Scope keeps them in the right place.** Store screens are driven only by a store's features and permissions, and org screens only by the org's — so a cashier can never see org-only features like `billing`, because those are never returned by a store endpoint.


## Billing & store count @TODO

The bill is **per-store**: `(sum of the org's live offerings' price per store) × number of active
stores`. So the moment a store is added or removed — or an add-on is turned on or off — the amount owed
changes. This section states, once, how that flows through the API and Stripe — every endpoint that
changes the store count (`POST /stores`, `DELETE /stores`) just references it rather than repeating the
money logic.

**The one rule: the billed quantity always equals the org's active store count.**

```
billed quantity  =  COUNT(*) FROM store WHERE organization_id = ? AND is_deleted = false
```

**Stripe owns the money; we own the count.** We never compute a charge, proration, or credit ourselves.
Each live subscription maps to a Stripe subscription item whose **quantity** is the store count above.
When the count changes, we update that quantity and Stripe does the rest — it **prorates automatically**
(charges the partial-period cost of a new store, credits a removed one).

**On create (`POST /stores`)** — inside one flow:
1. Insert the `store` row (`is_deleted = false`).
2. Recount active stores → new quantity.
3. Update each live subscription item's quantity to the new count. Stripe prorates the addition.

**On delete (`DELETE /stores`)** — same, in reverse: soft-delete the store, recount, lower the Stripe
quantity; Stripe credits the proration.

**Stripe failure does not fail the store operation.** If the Stripe quantity update errors (outage,
transient), the store create/delete still succeeds — a Stripe hiccup must not block running the business.
The mismatch is caught by reconciliation.

**Reconciliation is the safety net.** Because the rule is a simple equality (`Stripe quantity == active
store count`), a nightly job re-counts every org's active stores and corrects any Stripe quantity that
drifted. This makes the per-request Stripe call best-effort: even if it's dropped, the count self-heals
within a day. Store count in our DB is the source of truth; the Stripe quantity is a mirror of it.

**Note — this is separate from the read-only billing gate.** Changing the store count (or the set of
active offerings) changes *what the org owes*; it does not decide whether the org is *frozen*. Whether the
org can write at all is the subscription `status` (`UNPAID` / `CANCELED` → read-only), enforced by
`auth.md`'s billing gate. Adding a store or an add-on raises the bill; not paying that bill is what
eventually flips the org read-only.

> Schema note: the `subscription` table will need a Stripe reference (e.g. `stripe_subscription_item_id`)
> to target the quantity update. Not yet in `database.md` — TODO when the Stripe integration is specced.
