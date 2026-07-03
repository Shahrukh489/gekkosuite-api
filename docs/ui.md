# UI Design

How the interface is laid out, how a user moves between the organization and its stores, and how
each person's role decides what they see.


## Overview

One app, one set of screens. Three things decide what a user sees:

- **The store switcher** sets the *context* — the organization, or one specific store.
- **The user's memberships** decide which contexts are even available (an org membership unlocks the
  organization context; a store membership unlocks that store).
- **The user's permissions** decide which tabs and actions show inside whatever context they're in.

There's no separate "org app" and "store app" — the same tabs are reused, and the data inside plus the
available actions change with context and role. A user with only store memberships works entirely in
store context; a user with an organization membership can work at the org level *and* drop into any
store (see `auth.md`).


## Layout

**Topbar**
- Notifications
- Help
- User profile

**Sidebar**
- Logo
- **Store switcher** (sets the context — see below)
- Tabs (flat for now; shown based on context + permissions — see below)


## Store switcher (the context)

The switcher at the top of the sidebar chooses *where* you're working — the organization, or one
store. The organization sits at the top, with each store the user can reach below it:

```
[ Acme Inc ▾ ]
  ├─ 🏢 Acme Inc            ← Organization context (org-level + cross-store rollup)
  ├─ 🏪 Store A
  ├─ 🏪 Store B
  └─ 🏪 Store C
```

This maps directly to the API (`auth.md`): selecting a **store** routes its tabs under
`/stores/{storeId}/…` (products, customers, and sales are worked on at the store, so they're all store
paths — even when products or customers are shared org-wide, they're still created and managed from a
store). Selecting the **organization** routes its tabs under the org-level paths (suppliers, purchases,
expenses, stores, users). The organization itself always comes from the user's token, never the URL.
Switching context re-scopes every tab's data to that place.

What the switcher shows depends on the user's **memberships**:

- **A store-only user** (e.g. a cashier) — no Organization entry, only the store(s) they belong to. If
  it's a single store, the switcher can be hidden and they land straight in it. Someone who belongs to
  several stores (e.g. Cashier at one, Manager at another) sees each store they're a member of.
- **A user with an organization membership** (owner/admin) — the Organization entry plus every store,
  since an org membership reaches all of them.


## Tabs

Flat lists for now — we'll group them once each tab is described. The same tab components are reused
across contexts; the context scopes the data inside.

**Organization context** — central work: procurement, spending, administration.

| Tab | |
|---|---|
| Dashboard | Company-wide overview (cross-store rollup) |
| Suppliers | Manage the org's suppliers |
| Purchases | Purchase orders (buying inventory) |
| Expenses | Non-inventory spending |
| Billing | The org's plan and billing |
| Reports | Aggregate reports across all stores |
| Stores | Create and manage stores |
| Users | Create and manage users |
| Settings | Org settings (incl. sharing settings) |

**Store context** — selling: the store's products, customers, and sales.

| Tab | |
|---|---|
| Dashboard | This store's overview |
| Sell | Ring up a sale |
| Orders | The store's sales orders |
| Returns | Returns and refunds |
| Products | The store's products (SKU, stock, price) |
| Customers | The store's customers |
| Reports | This store's reports |
| Users | Users at this store |
| Settings | Store settings |


## When does a tab show? (the one rule)

A tab is visible only when **both** are true:

1. **It's relevant to the current context** (org vs store, per the tables above), **and**
2. **The user holds at least one permission it needs** (e.g. Customers needs `customer:read`).

That single rule produces the whole sidebar. Tabs a user can't use are **hidden** — the bar shows only
what they can do, which is cleanest for non-technical users.

Inside a tab, **permissions also decide the actions**: the tab may show, but buttons like *Delete*,
*Refund*, or *Edit price* render only if the user holds that permission. So the same tab is read-only
for one role and fully editable for another — no separate screen.


## FAQ

**A cashier logs in — what do they see?**
Just their store (no switcher if it's a single store), and only the tabs their role allows — e.g. Dashboard, Orders, Customers. No Users or Settings, since they lack the permission.

**A store manager vs that cashier — same store, different view?**
Yes. Same store, but the manager's role has more permissions, so they see more tabs (e.g. Products, Users) and more actions inside them (refund, edit price). One UI; the role decides the rest.

**An org owner wants to work in one store — how?**
They pick that store in the switcher. The app routes calls under `/stores/{storeId}/…` and shows the store tabs. Their org membership reaches every store, so they have full access there.

**How does the owner see the whole business?**
They pick **Organization** in the switcher → the org tabs (Dashboard rollup, Suppliers, Purchases, Expenses, Billing, Stores, Users). The org Dashboard is the cross-store overview.

**Why is there no Products or Customers tab in the org context?**
Products and customers are created and managed at a store, so they live in the store context. To work on them, the owner switches to that store. When sharing is on they're recognized across every store, but they're still managed from the store context — there's no separate org-level Products or Customers screen.

**Why can one user see a tab and another can't?**
Tabs are hidden unless the user has a permission for them, so the sidebar always shows only what that person can actually do.

**A user works at two stores — how do they switch?**
Both stores appear in the switcher; picking one re-scopes every tab to that store. Their roles can differ per store (Cashier at one, Manager at another).
