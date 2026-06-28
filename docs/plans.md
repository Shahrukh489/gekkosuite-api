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


## Why per-store pricing at the org level

It's the simplest thing that matches how these businesses think: one subscription for the company, priced by how many stores they run. The org picks a plan once; adding a store just adds one more unit to the bill and the new store automatically has every feature.


## How features reach a store

To check a feature ("can this store use reports?"), look at the **org's** plan and whether that plan includes the feature. Since every store inherits the org plan, the answer is the same for every store in the org.

**New features spread automatically.** Features are read through the plan, so adding a feature to a plan instantly gives it to **every org on that plan** — and therefore every store under those orgs — with no per-store or per-org updates. Removing a feature works the same way in reverse.


## How it's stored

- `organization.plan_id` — the org's one plan.
- `plan` — the plan, including its **per-store price**.
- `feature` — a feature, identified by a stable **`code`** (e.g. `multi_store`) the code checks for, plus a human **`label`** for display. The `code` never changes once shipped (renaming it would break feature checks); the `label` is safe to change.
- `plan_feature` — which features a plan includes (many-to-many).

The bill is **derived**, not stored: `plan.price_per_store × count(stores in the org)`. Nothing per-store is recorded for billing — the store count comes from `store.organization_id`.

Example plan prices:

```
Basic   $100 / store
Pro     $150 / store
```
