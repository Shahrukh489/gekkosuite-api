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


# FAQ

## A cashier logs in — what do they see?

A single store (with no switcher, since they belong to just one), and only the tabs their role's
permissions unlock. Everything they lack a permission for is simply hidden.

**Example**
Sara is a Cashier at Store A. Her role holds `order:read`, `sale:create`, and `customer:read`, so she
sees **Dashboard, Sell, Orders, Customers**. She has no `user:*` or settings permissions, so **Users**
and **Settings** never appear. She doesn't have to know they exist — the sidebar shows only her job.


## Same store, different roles — do two people see different things?

Yes. The store is the same; the **role decides the view**. More permissions means more tabs, and more
actions inside each tab. This is the UI reading the exact same permissions that authorize the API
(see *Authorization* in `auth.md`) — the tab and the button both check a permission.

**Example**
At Store A, Sara (Cashier) and Marcus (Manager) share one store context. Marcus's role adds
`product:edit` and `order:refund`, so he also sees the **Products** tab and — inside **Orders** — a
**Refund** button and an **Edit price** control. Sara sees neither. One screen, two experiences, no
separate "manager app."


## An org owner wants to work inside one store — how?

They pick that store in the switcher. The app then routes under `/stores/{storeId}/…` and shows the
store tabs, scoped to that store. Because an **organization membership reaches every store in the org**
(`auth.md`), the owner has full access there without needing a separate store membership.

**Example**
Diego holds an organization membership with the Org Admin role. He picks **Store B** in the switcher →
the store tabs appear, showing Store B's data. He can sell, edit products, and refund there, just as if
he were a Store B manager — his org membership already grants store reach across the org.


## How does the owner see the whole business at once?

They pick **Organization** in the switcher. That swaps the sidebar to the org tabs — Dashboard rollup,
Suppliers, Purchases, Expenses, Billing, Stores, Users, Settings — and every tab's data is the
company-wide view. The org **Dashboard** is the cross-store overview.

**Example**
From the **Organization** context Diego opens **Reports** and sees sales rolled up across Stores A, B,
and C together, then opens **Purchases** to record a supplier order for the company. None of this is
reachable from a store context — org work lives only at the org level (`auth.md`).


## Why is there no Products or Customers tab in the org context?

Because products and customers are **created and managed at a store**, so those tabs live in the store
context. To work on them, switch into the relevant store. Turning on sharing doesn't add an org-level
screen — a shared product or customer is still managed from a store; sharing only changes whether the
other stores also recognize it (see `tenancy.md` and the dual-write in `auth.md`).

**Example**
Sara adds a customer at Store A. Because customers are shared org-wide, that customer is now recognized
at Stores B and C too — but everyone still edits it from a **store's** Customers tab. There's no "org
customers" screen; the owner who wants to see it just switches into any store.


## Why can one user see a tab when another can't?

Every tab is gated by a permission, and tabs a user lacks are hidden rather than greyed out — so the
sidebar always reflects exactly what that person can do. This is the same deny-by-default idea the API
uses (`auth.md`): no permission, no access — and here, no permission, no tab.

**Example**
**Users** requires a `user:*` permission. Marcus (Manager) doesn't hold one, so the tab is absent from
his sidebar entirely — he never sees a button he can't use.


## A user works at two stores — how do they switch, and can their access differ?

Both stores appear in the switcher; picking one re-scopes every tab to that store. Their **role can
differ per store**, because access is per-membership (`auth.md`), so the tabs and actions change as
they switch.

**Example**
Maria is a Cashier at Seattle and a Manager at Portland (one login, two store memberships — see the
create-user example in `auth.md`). In **Seattle** she sees the cashier view (no Products, no Refund);
she switches to **Portland** and the Manager tabs and actions appear. Same login, different reach at
each place.
