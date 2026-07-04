# Overview

| | |
|---|---|
| **Project** | Multi-store POS |
| **Author** | Salman Hoosein |
| **Version** | 1.0 |

A multi-store point-of-sale system for a single company that runs many stores. One **organization**
onboards its users, grants them access to stores (and optionally to the org itself), and runs every
sale through a store as its own business unit. The organization handles the money and admin side —
suppliers, purchasing, expenses, users, and stores — while each store focuses on selling.


# High-level design

- **An organization owns many stores.** It onboards its **users** and gives each access to one or more
  **stores** — and optionally to the **organization level** too, for people who oversee the whole
  business (owners, partners, investors who need a cross-store view without working a register).
- **Selling always happens through a store.** A store is the real **business unit** — where sales and
  money come in. You can't sell "at the org"; you sell at a store.
- **Each store owns its selling data** — its products (each with their own stock and price), its
  customers, and its sales and returns. By default a store stays in its own domain; one store's
  products and customers aren't another's.
- **Products and customers can optionally be shared org-wide.** The organization can turn on sharing
  per entity (products, customers, or both). Off by default (each store isolated); on means an item
  created at one store is recognized at every store — while **stock and price stay per-store**.
- **The organization handles the central pieces** — it owns the **suppliers**, does all **purchasing**
  and **expense** spending, manages **stores** and **users**, and decides whether products and
  customers are shared. Money leaving the business is always an org action.

So each store is an independent selling front, and the organization sits above it — owning
procurement, spending, administration, and the sharing settings.


# Who this is for (the niche)

We serve **tightly-coupled single companies**: one business that owns and runs all its stores — **not
franchises**, where each location is an independent business. A typical org has up to ~50–100 stores,
and its users are **non-technical**, so the design deliberately favors **simple over flexible**:

- One organization owns many stores; org-level users handle suppliers, purchasing, expenses, and
  managing stores and users.
- Each store owns its own products and customers **by default** — isolation is the norm. An org that
  wants one shared catalog or customer base across its stores turns on sharing (a per-entity setting).
- **Spending is centralized**: the organization buys inventory and pays expenses (each optionally
  attributed to a store), so a store is purely a selling front.


# Features

Everything the application does, split by **who can do it** — the organization (central management,
org-level users) and the store (the selling unit, store users). Each row is a single capability.

**Organization** — org-level users only; reaches every store in the org.

| Feature | What it allows |
|---|---|
| Act on any store | Perform any store action on any of the org's stores — an org role reaches all of them. |
| Cross-store reports | Aggregate reports rolled up across all stores (sales, inventory, performance); the org Dashboard is the company-wide overview. |
| Purchase orders | Buy inventory from suppliers; each purchase optionally attributed to a store. |
| Expenses | Record non-inventory spending; each expense optionally attributed to a store. |
| Suppliers | Create and manage the org's suppliers. |
| Users | Create and manage users (both org and store users). |
| Roles | Assign roles to users |
| Stores | Create and manage stores. |
| Products | Manage Products across all stores. |
| Customers | Manage Customers across all stores. |
| Plan | Hold the org's plan; every store inherits its features. |
| AI Chatbot | Feed KB to AI chatbot, and let each store owner chat with it. |
| AI Recommendations | Get Organization Based Recommendations. |
| Sharing settings | Turn org-wide sharing of products and/or customers on or off (off by default). On = an item created at one store is recognized org-wide; stock and price stay per-store. |
| Org-level updates | Only org users can make org-level changes — store users can never reach the org. |

**Store** — store users, scoped to the store(s) they belong to.

| Feature | What it allows |
|---|---|
| Sell | Ring up sales at the store. |
| Returns | Process returns and refunds for the store's own orders. |
| Products | Manage the store's own products (SKUs, stock, price). With sharing on, a new product is also added to the org-wide catalog; stock and price stay the store's own. |
| AI Chatbot | Chat with Organization AI KB for support. |
| AI Recommendations | Get Store Based Recommendations. |
| Customers | Manage the store's own customers. With sharing on, a new customer is also recognized org-wide. |
| Customer Requests | Make requests about what they need and want. |
| Reports | View the store's own reports, plus the purchases/expenses attributed to it. |
| Store-only scope | A store user only ever acts on their own store's data — never another store's, never the org's. |

**Onboarding note:** during onboarding, ask the org whether it wants products and/or customers shared,
so the sharing settings are set from the start. They can be changed later.


**Customers Portal** — Customers can login, see order history, make requests, get alerts on deals with AI.


# Key design decisions

The choices below keep stores simple to run — a store admin just manages inventory, customers, sales,
and returns — while everything that affects the whole company stays with organization users.

**Creating users and assigning roles is org-level only.**

- A new user becomes part of the organization, so onboarding them is something the org should approve —
  the way most companies work. A store admin can instead raise a request; the org gets notified and
  approves or denies.
- This removes the risk of a store admin accidentally creating another admin, or granting someone
  elevated access inside a store that the org never intended.
- A store admin still has real control at their store: they can **disable** a user and **remove a
  user's role**. They just can't **create**, **delete**, or **assign roles** — those are org actions.

**Products live in per-store tables.**

- No store can corrupt another store's data; each store manages its own copy.
- If a store needs visibility into a nearby store's stock, the org can grant a user a **read-only
  products** role scoped to that other store — controlling exactly who sees what across stores.
- When an org wants a single catalog, it turns on **product sharing**: a product created at one store
  is also written to the org-wide catalog, while each store keeps its own stock and price.

**Customers live in per-store tables.**

- Customers are local to their store, so accounts and store-run promotions/discounts stay independent,
  and no store can affect another store's customer records.
- When an org wants one customer base, it turns on **customer sharing**, so a customer created at one
  store is recognized at every store — the basis for cross-store loyalty (e.g. points across all stores
  for the same customer).

**The organization does all purchasing and expenses.**

- The organization holds the vendor relationships, so it makes the purchases.
- Stores can't spend money on things the org hasn't approved; if a store needs something, it raises a
  request.
- This gives the org owner full control of spending — stores make money on the org's behalf, and the
  org spends to support them.


# Documentation map

The detail lives in focused docs:

- **`tenancy.md`** — organizations & stores: who owns what (store products/customers with optional
  org-wide sharing; org-owned suppliers/purchases/expenses), and how money flows.
- **`auth.md`** — users, memberships, roles, permissions, and access (authn & authz).
- **`plans.md`** — plans, features, and billing.
- **`database.md`** — the database schema (tables, indexes, RLS).
- **`post-mvp.md`** — deferred features and the rationale.
