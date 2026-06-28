# To Be Decided

Open design questions captured but not yet resolved.

---

## RESOLVED — Store users writing org-level (shared) resources

**Decision:** made products and customers **store-owned** (each store its own), and centralized
procurement + expenses at the **org** (only org users buy/spend). So a store user only ever writes
**store-level** data, and the "store user writes a shared org row" mismatch **no longer exists**.

Customers/products are isolated per store for MVP; **org-wide sharing** (one catalog / one customer
base) is a planned post-MVP **setting** with org-approval (or auto-approve) — see `post-mvp.md`.

This also cleaned up the auth model: a store user = pure store-level (sell-side) actor; an org user =
org-level actor. No cross-level authorization case remains.

---

## OPEN — Inventory distribution (how purchased stock reaches a store)

Procurement is now an **org** action: an org user creates a `purchase_order` with **no store_id**
(stock is bought for the org). But products are **store-owned** (`store_product`). So there is
currently **no modeled path** for purchased inventory to become a specific store's `store_product`
quantity.

To decide when inventory/receiving is designed:

- Does an org purchase name a **destination store** (adds straight to that store's `store_product`)?
- Or does stock land at an org level first and get **distributed** to stores in a separate step
  (a transfer/allocation flow)?
- Related: **what does `purchase_order_product` reference?** Products are store-owned now, so a
  purchase line can't simply point at a `store_product`. Options: an org-level item/SKU list, a
  destination store's product, or free-text for MVP. Tied to the same distribution decision.

For MVP, purchases can be recorded as org-level money-out without auto-touching store stock; wire the
stock path when receiving is designed.

---

## OPEN — Org-wide sharing setting (post-MVP, noted here for tracking)

The post-MVP plan: an **org-level setting** to share customers and/or the catalog org-wide instead of
per-store. When on, a store-created customer/product becomes visible to all stores — with **org
approval** of the change (notification → approve), or **auto-approve** if the org configures it. See
`post-mvp.md` for where this lands. Open sub-questions for then: dedup of the same customer/product
across stores at share time; whether sharing is all-or-nothing or per-entity (customers vs catalog).
