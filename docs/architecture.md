Author: Salman Hoosein
Version : 1.0
Project:
Description: A multi-store POS where one organization onboards its people, grants them access to stores (and optionally the org itself), and runs sales through each store as a business unit.

# System Overview

In plain terms, the system works like this:

- An **organization** onboards its **users** and gives each one access to one or more **stores** — and **optionally to the organization level** too (for people who oversee the whole business, like owners, partners, or investors who need a cross-store view without working a register).
- **Org-level users see everything** across the business — all stores, and the org-wide **customers**, **suppliers**, and **product catalog**.
- **Selling always happens through a store.** A store is the real **business unit** — it's where sales, tax, and money live — so you can't sell "at the org"; you sell at a store. The org is the umbrella that owns the stores.
- Each **store manages and overrides its own products** — it sets its own quantity and price from the shared catalog — so stores run their own day-to-day operation.
- But stores don't start from scratch: they easily **reuse the org's shared customers and suppliers**, so the same customer account or vendor works at any store without re-entering it.

So the org is the shared backbone (people, catalog, customers, suppliers, oversight), and each store is an independent selling unit on top of it.



## Who this is for (the niche)

We serve **tightly-coupled single companies** — one business that owns and runs all its stores — **not franchise systems** where each location is an independent business. A typical org has up to ~100 stores. The users are **non-technical**, so the design is deliberately simple over flexible:

- One organization owns many stores; the org admin sees everything across all of them (customers, suppliers, products, stores).
- **Customers and suppliers are shared org-wide** — one customer account / one vendor record works at every store.
- There is **one product catalog** for the org (deduped on SKU), and each store **overrides its own quantity and price** for what it sells.

Because the stores belong to the same company, we don't build per-store walls, visibility toggles, or franchise-style isolation between locations. Features that imply looser coupling (per-store customer privacy, cross-store inventory sharing, regions/districts as managed entities) are deferred unless a concrete need appears; see `post-mvp.md`.



# Documentation map

The detail lives in focused docs:

- **`tenancy.md`** — organizations & stores: ownership, the shared org-wide catalog/customers/suppliers, what each store manages on its own, and how store funds work.
- **`auth.md`** — users, memberships, roles, permissions, and access (authn & authz).
- **`plans.md`** — plans, features, and billing.
- **`database.md`** — the database schema (tables, indexes).
- **`post-mvp.md`** — deferred features and the rationale.
