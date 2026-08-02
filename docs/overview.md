| | |
|---|---|
| **Project** | GekkoSuite |
| **Author** | Salman Hoosein |
| **Version** | 1.0 |
| **Description** | Easy to use and simply CRM/PoS that provides features to serve the daily operations of small organizations that can have up to multiple stores. |


# Overview

```mermaid
flowchart TB
    ORG["Organization<br/>(suppliers, purchases, expenses, users)"]
    ST1["Store A<br/>(its products, customers, sales)"]
    ST2["Store B<br/>(its products, customers, sales)"]

    ORG --> ST1
    ORG --> ST2

    style ORG fill:#dbeafe,stroke:#93c5fd
```

---

## Organization

An **organization** is the business. It owns many **stores** and **handles adminstration**: owns the suppliers, does all purchasing, expense spending, manages stores, creates users, etc...

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
| Org-level scope | Make org-level changes |

---

## Store

A **store** is where selling happens, you can't sell at the organization level. Each **store owns its own selling data**: its products (each with their own stock and price), its customers, and its sales and returns.

| Feature | What it allows |
|---|---|
| Sell | Ring up sales at the store. |
| Orders | Manage sales orders for the stores. |
| Invoices | Manage invoices for the stores. |
| Receipts | Manage receipts for the stores. |
| Returns | Process returns and refunds for the store's orders. |
| Inventory | Manage the store's own inventory (SKUs, stock, price), linked to the Organization's products |
| Customers | Manage the store's own customers. With sharing on, a new customer is also recognized org-wide. |
| Store-only scope | A store user only ever acts on their own store's data |

---

# Documentation 

The detail lives in focused docs:

- **`auth.md`** — users, memberships, roles, permissions, and access (authn & authz).
- **`plans.md`** — plans, add-ons, features, and billing.
- **`database.md`** — the database schema (tables, indexes, RLS).
- **`costs.md`** — estimate of development and maintenance costs.
- **`api.md`** — high level api schema.
- **`post-mvp.md`** — deferred features and the rationale.
