# Plans & Features

How an organization subscribes, what its stores get, and how it's billed.


## The model

- An **organization** is on one **plan** (like "Basic" or "Pro").
- A plan has a **price per store** and a set of **features** (reports, returns, multi-store, etc.).
- **Every store in the org gets all of the plan's features** — stores don't have their own plans. The plan is bought once at the org; the stores inherit it.
- The **bill = the plan's per-store price × the number of stores** in the org.

```
Organization → Plan (price per store + features)
                       │
                       ▼  every store inherits the plan's features
   StoreA   StoreB   StoreC   ...

Pro plan: $150/store, includes [reports, returns, multi_store]
Org has 3 stores  →  all 3 stores have reports, returns, multi_store
Bill = 3 × $150 = $450
```


# FAQ

## How does a store get its features?

To check a feature ("can this store use reports?"), look at the **org's** plan and whether that plan includes the feature. Since every store inherits the org plan, the answer is the same for every store in the org.

**New features spread automatically.** Features are read through the plan, so adding a feature to a plan instantly gives it to **every org on that plan** — and therefore every store under those orgs — with no per-store or per-org updates. Removing a feature works the same way in reverse.



