# Plans

## Overview

- An **organization** is on one **plan** ("Basic" or "Pro").
- A plan has a **price per store** and a set of **features** (reports, ai, analytics, etc.).
- **Every store in the organization gets all of the plan's features** — stores don't have their own plans. The plan is bought once at the organization and the stores inherit it.
-  **New features spread automatically.** Features are read through the plan, so adding or removing a feature to a plan instantly updates for **every organization on that plan**
- The **bill = the plan's per-store price × the number of stores**

```
Organization → Plan (price per store + features)
                       │
                       ▼  every store inherits the plan's features
   StoreA   StoreB   StoreC   ...

Pro plan: $150/store, includes [reports, returns, multi_store]
Org has 3 stores  →  all 3 stores have reports, returns, multi_store
Bill = 3 × $150 = $450
```





