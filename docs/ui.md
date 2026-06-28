# UI Design

One app, one set of screens. The **store switcher** sets the *context* (organization vs a store),
and the user's **permissions** decide which tabs and actions show. There is no separate "org app"
and "store app" — the same tabs are reused; the data inside and the available actions change with
context and role.

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

This maps directly to tenancy (`auth.md`): selecting the **organization** sends
`X-Target-Organization-Id`; selecting a **store** sends `X-Target-Store-Id`. Switching context
re-scopes every tab's data to that place.

What the switcher shows depends on the user:
- **Store-only user** (e.g. a cashier) — no Organization entry, only the store(s) they belong to.
  If it's a single store, the switcher can be hidden — they land straight in it.
- **Org user** (owner/admin) — the Organization entry plus every store (an org role reaches all).
- **Multi-store user** — the stores they're a member of.


# Tabs (flat)

Flat lists for now — we'll group them later when each tab is described. The same tab components are
reused across contexts; the context scopes the data inside.

**Organization context:**
- Dashboard
- Products
- Orders
- Suppliers
- Customers
- Expenses
- Billing
- Reports
- Stores
- Users
- Settings

**Store context:**
- Dashboard
- Sell
- Orders
- Returns
- Products
- Customers
- Expenses
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
Yes. Same store, but the manager's role has more permissions, so they see more tabs (e.g. Products, Expenses, Users) and more actions inside them (refund, edit price). One UI, role decides the rest.

**An org owner wants to work in one store — how?**
They pick that store in the switcher. The context flips to the store (the app sends `X-Target-Store-Id`) and shows the store tabs. Their org role reaches every store, so they have full access there.

**How does the owner see the whole business?**
They pick **Organization** in the switcher → the org tabs (Dashboard rollup, Products catalog, Customers, Suppliers, Billing, Stores, Users). The org Dashboard is the cross-store overview.

**Same tab in org vs store — is it the same screen?**
Yes — e.g. Customers is one screen. In the org it lists all customers; in a store it lists the customers used there. Context scopes the data; the screen is identical.

**Why can one user see a tab and another can't?**
Tabs are hidden unless the user has a permission for them. So the sidebar always shows only what that person can actually do.

**A user works at two stores — how do they switch?**
Both stores appear in the switcher; picking one re-scopes every tab to that store. Their roles can differ per store (Cashier at one, Manager at another).

