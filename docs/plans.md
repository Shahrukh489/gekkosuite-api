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





