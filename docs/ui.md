# UI Design

One app, one set of screens. The user's **type** (organization or store) sets which world they're in,
the **store switcher** sets the *context* (the organization, or a specific store), and the user's
**permissions** decide which tabs and actions show. There is no separate "org app" and "store app" —
the same tabs are reused; the data inside and the available actions change with context and role.

A **store user** only ever works in store context (they have no organization entry). An **organization
user** can work in the organization context *and* drop into any store. So the switcher's shape follows
the user's type — see below.

# Topbar
- Notifications
- Help Icon
- User Profile

# Sidebar
- Logo
- **Store switcher** (sets the context — see below)
- Tabs (flat for now; shown based on context + permissions — see below)


# Store switcher (the context)

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
paths — even when products/customers are shared org-wide, they're still created and managed from a
store); selecting the **organization** routes its tabs under the org-level paths (suppliers, purchases,
expenses, stores, users). The organization itself always comes from the user's token, never the URL.
Switching context re-scopes every tab's data to that place.

What the switcher shows depends on the user's **type**:
- **Store user** (e.g. a cashier) — no Organization entry, only the store(s) they belong to. If it's
  a single store, the switcher can be hidden — they land straight in it. A multi-store store user
  (e.g. Cashier at one, Manager at another) sees each store they're a member of.
- **Organization user** (owner/admin) — the Organization entry plus every store (an org role reaches all).


# Tabs (flat)

Flat lists for now — we'll group them later when each tab is described. The same tab components are
reused across contexts; the context scopes the data inside.

**Organization context** (central: procurement, spending, administration):
- Dashboard
- Suppliers
- Purchases
- Expenses
- Billing
- Reports
- Stores
- Users
- Settings

**Store context** (selling: the store's own products, customers, sales):
- Dashboard
- Sell
- Orders
- Returns
- Products
- Customers
- Reports
- Users
- Settings



# When does a tab show? (the one rule)

A tab is visible if **both** are true:

1. **It's relevant to the current context** (org vs store, per the table above), **and**
2. **The user has at least one permission it needs** (e.g. Customers needs `customer:read`).

That single rule produces the whole sidebar. Tabs the user can't use are **hidden** — the bar shows
only what you can do (cleanest for non-technical users).

Inside a tab, **permissions also decide the actions** — the tab may show, but buttons like *Delete*,
*Refund*, or *Edit price* render only if the user has that permission. So one tab is read-only for
one role and fully editable for another, with no separate screen.


# FAQ

**A cashier logs in — what do they see?**
Just their store (no switcher if it's one store), and only the tabs their role allows — e.g. Dashboard, Orders, Customers. No Users or Settings (no permission).

**A store manager vs that cashier — same store, different view?**
Yes. Same store, but the manager's role has more permissions, so they see more tabs (e.g. Products, Users) and more actions inside them (refund, edit price). One UI, role decides the rest.

**An org owner wants to work in one store — how?**
They pick that store in the switcher. The app routes calls under `/stores/{storeId}/…` and shows the store tabs. Their org role reaches every store, so they have full access there.

**How does the owner see the whole business?**
They pick **Organization** in the switcher → the org tabs (Dashboard rollup, Suppliers, Purchases, Expenses, Billing, Stores, Users). The org Dashboard is the cross-store overview.

**Why is there no Products or Customers tab in the org context?**
Products and customers are created and managed at a store, so they live in the store context. To work on a store's products or customers, the owner switches to that store. When the org has sharing on, those items are recognized across every store, but they're still managed from the store context — there's no separate org-level Products/Customers screen.

**Why can one user see a tab and another can't?**
Tabs are hidden unless the user has a permission for them. So the sidebar always shows only what that person can actually do.

**A user works at two stores — how do they switch?**
Both stores appear in the switcher; picking one re-scopes every tab to that store. Their roles can differ per store (Cashier at one, Manager at another).

