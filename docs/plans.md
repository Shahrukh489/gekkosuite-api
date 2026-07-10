## Overview

- An **organization** is on one **plan** ("Basic" or "Pro").
- A plan has a **price per store** and a set of **features** (reports, returns, billing, multi-store, ai, etc.).
- **The organization is on the plan; there are no per-store plans.** The plan is bought once at the organization.
- **Each feature has a scope — `STORE` or `ORGANIZATION`** (see the `feature` table in `database.md`). This decides *where* it applies:
    - **`ORGANIZATION`** features are org-level capabilities (billing, multi-store, cross-store reports). They apply to the organization itself.
    - **`STORE`** features are store-level capabilities (returns, AI recommendations, store reports). Every store in the org gets the plan's store-scoped features.
    - A capability that's needed in *both* places is two separate features — one per scope.
- **New features spread automatically.** Features are read through the plan, so adding or removing a feature on a plan instantly updates every organization on that plan (and its stores, for store-scoped features).
- The **bill = the plan's per-store price × the number of stores**.

```
Organization → Plan (price per store + features)
                       │
        ┌──────────────┴───────────────┐
        │                              │
   ORGANIZATION-scoped features    STORE-scoped features
   (billing, multi_store, ...)     (returns, ai, reports, ...)
   apply to the org                every store gets these

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





