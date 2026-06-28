Author: Salman Hoosein
Version : 1.0
Project:
Description: A multi-store POS where one organization onboards its users, grants them access to stores (and optionally the org itself), and runs sales through each store as a business unit.

# Overview

- An **organization** onboards its **users** and gives each one access to one or more **stores** — and **optionally to the organization level** too (for people who oversee the whole business, like owners, partners, or investors who need a cross-store view without working a register).
- **Selling always happens through a store.** A store is the real **business unit** — it's where sales and money come in — so you can't sell "at the org"; you sell at a store.
- Each **store owns its own selling data** — its products (each with their own stock and price), its customers, and its sales and returns. Stores stay in their own domain; one store's products and customers aren't another's.
- The **organization handles the central pieces** — it owns the **suppliers**, does all **purchasing** and **expense** spending, and manages **stores** and **users**. Money going out of the business is an org action.

So each store is an independent selling unit, and the organization sits above it owning procurement, spending, and administration.



## Who this is for (the niche)

We serve **tightly-coupled single companies** — one business that owns and runs all its stores — **not franchise systems** where each location is an independent business. A typical org has up to max 50-100 stores. The users are **non-technical**, so the design is deliberately simple over flexible:

- One organization owns many stores; org-level users handle suppliers, purchasing, expenses, and managing stores and users.
- **Each store owns its own products and customers** — isolation is the default, so stores stay in their own domain.
- **Spending is centralized**: the organization buys inventory and pays expenses (each optionally attributed to a store), so a store is purely a selling front.
- Org-wide sharing of products/customers (one catalog or customer base across stores) is a planned post-MVP setting.


# Documentation map

The detail lives in focused docs:

- **`tenancy.md`** — organizations & stores: who owns what (store-owned products/customers, org-owned suppliers/purchases/expenses), and how money flows.
- **`auth.md`** — users, memberships, roles, permissions, and access (authn & authz).
- **`plans.md`** — plans, features, and billing.
- **`database.md`** — the database schema (tables, indexes).
- **`post-mvp.md`** — deferred features and the rationale.
- **`to-be-decided.md`** — open design questions not yet resolved.
