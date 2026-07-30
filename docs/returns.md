# Returns / RMA plan

A plan for an **in-store returns system**: a customer brings back something they bought at a
GekkoSuite store, a cashier or manager looks up the original sale, picks what's coming back and why,
and the system refunds them. This is the retail-counter version of an RMA (return the item, get money
back) — not the mail-order/warehouse version (ship it back, get an RMA number, wait for inspection).
That distinction matters for scope; see **Terminology** below.

Nothing here is built yet. `docs/database.md` sketches `sales_order_return` /
`sales_order_return_product` as design, but they were never added to the real migration
(`tools/GekkoSuite.Database/Migrations/1_0_0.sql`), and there's no API layer for either **orders** or
**returns** — no `OrderController`, no `ReturnController`, no repositories. The UI's Sell tab currently
doesn't persist a sale anywhere either. **Returns depend on orders existing first** — you can't return
against a sale that was never saved. This doc plans both, in order.


## Terminology: why "in-store return," not "RMA"

"RMA" (Return Merchandise Authorization) usually implies: customer requests a return remotely → gets an
authorization number → ships the item back → warehouse inspects it → refund issued days later. That's
the right model for e-commerce/mail order, where the item and the money never meet in person.

GekkoSuite is a **multi-store POS** — most returns happen at a register, in person, with the item in
hand and the original sale one lookup away (`docs/overview.md`, `docs/ui.md`'s Returns tab). Forcing an
authorization-number workflow onto that would fight the "simple over flexible" principle in `CLAUDE.md`
for no benefit — there's no shipping step to authorize. So this plan is a same-visit counter return:
look up the sale, pick the lines, refund. The `ONLINE` store type (`store.type`) is the one case that
might genuinely want a request → ship-back → inspect flow later; it's called out as a **Phase 4 / later**
extension, not blocking the core design.


## What already exists to build on

| Piece | Where | Status |
|---|---|---|
| `order_status` enum (`OPEN`, `COMPLETED`, `VOIDED`) | `docs/database.md` | Designed, not migrated |
| `return_status` enum (`PENDING`, `COMPLETED`, `VOIDED`) | `docs/database.md` | Designed, not migrated |
| `sales_order` / `sales_order_product` | `docs/database.md` | Designed, not migrated, no API |
| `sales_order_return` / `sales_order_return_product` | `docs/database.md` | Designed, not migrated, no API |
| `returns` feature (STORE-scoped) | `docs/overview.md`, seeded in `local/mock_data.sql` as a feature code | Gate exists conceptually; not wired to any endpoint |
| `order:refund` permission | seeded in `local/mock_data.sql`, granted to the **Manager** role only (not Cashier) | Exists, unused — nothing checks it yet |
| Returns tab | `docs/ui.md` (store context, needs `order:refund`-equivalent to show) | Documented, not built |
| ABAC refund caps (`permission_condition` / `role_permission_condition`, e.g. "Cashier refund ≤ $500") | `docs/post-mvp.md` | Stretch goal — designed, not built, not required for v1 |

So the ground floor — the enums, the permission, the feature flag, the two return tables — is already
drawn on paper. Nothing has been poured yet, and the floor below it (orders) doesn't exist either.


## Scope decisions I need from you before building

These are product calls, not implementation details — flagging them now so the schema doesn't have to
be redesigned mid-build.

1. **Return window.** Is there a time limit (e.g. 30/90 days from sale)? Fixed org-wide, or configurable
   per store/org? If a receipt is past the window, does the UI block the return, warn-and-allow, or route
   to a manager override?
2. **Refund method.** Must a refund go back to the original tender (card refunded to the same card, cash
   returned as cash), or can staff choose a different method (e.g. cash sale refunded as store credit)?
   This decides whether `sales_order.payment_method` (needs adding — see below) is just informational or
   load-bearing for what the return screen allows.
3. **Store credit — ✅ RESOLVED: in scope for v1.** A refund with `refund_method = STORE_CREDIT` credits
   the amount to the customer's account rather than handing back cash/card. See **Phase 1a** below for
   the balance design.
4. **Restocking — ✅ RESOLVED: a per-line option, not a default behavior.** Each returned line carries its
   own `restock BOOLEAN` (defaulting to `TRUE`) that the cashier can flip off — resellable items go back
   into `store_product.stock`, damaged/write-off ones don't. Already reflected in the schema below.
5. **Cross-store returns.** Can a customer return to a *different* store than they bought from (common in
   retail chains), or only the store of purchase? This has real teeth: `store_product` stock and RLS are
   both store-scoped (`docs/database.md`), so "return anywhere" means the return can target a
   `store_product` row that isn't the one sold, and stock adjustments happen at a different store than
   the sale. Simple-over-flexible says start with **same-store only**; flag if that's wrong for you.
6. **Approval / refund caps.** Does every return need a Manager, or can a Cashier refund unsupervised up
   to some cap? `docs/post-mvp.md` already designs the ABAC mechanism for this (`permission_condition` /
   `role_permission_condition`, e.g. "Cashier refund ≤ $500"). For v1, the simplest thing is: only
   `order:refund` gates the whole action (today, only Manager holds it — mock data already reflects
   this), and the ABAC cap is a Phase 4 add-on once the flat gate is live.
7. **Exchanges.** Is a same-visit exchange (return one item, immediately buy another) in scope, or is
   that just "a return, then a separate new sale" from the system's point of view? I'd recommend the
   latter for v1 — an exchange is two orders (one COMPLETED return + one new sale) rather than a third
   transaction type, which keeps `sales_order` and `sales_order_return` from needing to know about each
   other beyond the return already pointing at the original order.

I've noted my lean on each so you can just flag disagreements rather than answering from scratch.


## Phase 0 (prerequisite): orders have to actually persist

The Sell tab doesn't write anything today — checkout is local component state (see recent Sell-page
work). A return needs a real `sales_order` row to look up, so this has to land first regardless of how
the return system itself is scoped:

- Add `sales_order` + `sales_order_product` to `1_0_0.sql` (already designed in `docs/database.md`,
  lines defining `order_status`, `sales_order`, `sales_order_product`).
- Add `payment_method` to `sales_order` (`CASH` | `CARD` — new enum) — the checkout dialog already asks
  cash-or-card; today that answer is thrown away. Needed either way for the receipt/lookup screen, and
  load-bearing if scope decision #2 says refunds must match original tender.
- `OrderEntity` / `OrderDto` / `OrderRepository` / `OrderController`, following the existing
  `ProductRepository` pattern (`BaseRepository`, raw SQL, tenant-stamped) — a `POST
  /stores/{storeId}/orders` that writes the order + lines, decrements `store_product.stock`, and returns
  the created order with a human-readable order number for lookup.
- Wire the Sell page's checkout to call it instead of just toasting a success message.


## Phase 1: return schema

Extends `docs/database.md`'s existing sketch rather than replacing it. Additions in **bold**.

```sql
-- Lifecycle of a return. PENDING = raised, nothing refunded/restocked yet; COMPLETED = refunded;
-- VOIDED = cancelled before completion.
CREATE TYPE return_status AS ENUM ('PENDING', 'COMPLETED', 'VOIDED');

-- How the refund was paid out. Independent of the original sale's tender (see scope decision #2) —
-- if refunds must match original tender, the app enforces that; the column doesn't assume it.
-- STORE_CREDIT doesn't hand back money — it credits customer.store_credit_balance instead (Phase 1a).
CREATE TYPE refund_method AS ENUM ('CASH', 'CARD', 'STORE_CREDIT');

CREATE TABLE sales_order_return (
    return_id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id              UUID NOT NULL REFERENCES store (store_id),
    order_id              UUID NOT NULL REFERENCES sales_order (order_id),
    -- who processed it — the authorizer if order:refund is required, the cashier otherwise
    processed_by_user_id  UUID REFERENCES user_account (user_id),
    status                return_status NOT NULL DEFAULT 'PENDING',
    reason                TEXT,                              -- free-text or a fixed reason code — see below
    refund_method         refund_method,                      -- NULL until COMPLETED
    refund_total          NUMERIC(12, 2) NOT NULL DEFAULT 0,   -- sum of line refund_amounts
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at          TIMESTAMPTZ                         -- stamped with status = COMPLETED, like subscription.ended_at pairs with status
);
CREATE INDEX ON sales_order_return (store_id);
CREATE INDEX ON sales_order_return (order_id);   -- "has this order already been returned against?"

CREATE TABLE sales_order_return_product (
    return_id         UUID NOT NULL REFERENCES sales_order_return (return_id),
    store_product_id  UUID NOT NULL REFERENCES store_product (store_product_id),
    quantity          INTEGER NOT NULL,
    sold_price        NUMERIC(12, 2) NOT NULL,   -- snapshot of the original line's unit_price
    refund_amount     NUMERIC(12, 2) NOT NULL DEFAULT 0,
    restock           BOOLEAN NOT NULL DEFAULT TRUE,   -- see scope decision #4
    condition_note     TEXT,                            -- optional, e.g. "box opened", "damaged"
    PRIMARY KEY (return_id, store_product_id)
);
```

**Invariants to guard** (mirrors the existing "Optional Triggers" section in `docs/database.md` — app
service is the primary guard, these are backstops):

- A return line's `quantity` can't exceed `(quantity sold on the original order line) − (quantity
  already returned across earlier non-voided returns against that order)`. Spans `sales_order_product`
  and prior `sales_order_return_product` rows, so it's a trigger, not a `CHECK`.
- `sales_order_return.order_id` must belong to the same `store_id` as the return (same-store rule from
  scope decision #5, if that's the answer) — enforceable as a `CHECK`-adjacent join, likely app-layer.
- The original order must be `status = 'COMPLETED'` — you can't return an `OPEN` or already-`VOIDED` sale.
- Restocking and the refund only take effect when `status` moves to `COMPLETED` — a `PENDING` return is a
  draft, same as `sales_order.status = 'OPEN'` not yet touching stock. This is why `restock` decrements
  happen in the same service method that flips the status, not on line insert.


## Phase 1a: store credit

A `STORE_CREDIT` refund adds to the customer's balance instead of paying out cash/card. Customers are
shared **org-wide** (`docs/tenancy.md` — one `customer` row recognized across every store in the org, via
`store_customer`), so the balance lives on the shared `customer` record, not `store_customer` — credit
earned at a return in one store can be spent at any of the org's other stores, same as the customer
identity itself.

```sql
ALTER TABLE customer
    -- running store-credit balance, org-wide (see docs/tenancy.md's shared-customer model). Only moved
    -- by a STORE_CREDIT return (credit) or redeeming credit at checkout (debit) — never by app writes
    -- directly touching the number, so both paths go through one service method that can't take it negative.
    ADD COLUMN store_credit_balance NUMERIC(12, 2) NOT NULL DEFAULT 0;
```

A single running balance (not a full ledger table) is the v1 shape — proportionate to what's actually
needed: the return flow *issues* credit, and a future Sell-page enhancement *redeems* it at checkout.
Both are one `UPDATE customer SET store_credit_balance = store_credit_balance + / - @amount` inside the
same transaction that completes the return or the sale, so the balance can't drift from what actually
happened. If a full audit trail (who issued it, which order redeemed it, when) turns out to be needed
beyond "what's the balance right now," that's the Phase 4 ledger upgrade — not needed to ship v1.

**Invariant:** completing a return with `refund_method = STORE_CREDIT` credits
`customer.store_credit_balance` by `refund_total` in the same transaction that flips
`sales_order_return.status` to `COMPLETED` — the balance updates at the same moment the refund "happens,"
exactly like restocking does for `restock = true` lines.


## Phase 2: API

Following the existing repo pattern (`docs/api.md`'s section style, `BaseRepository` +
`ProductRepository` as the template):

| Endpoint | Scope | Permission | Notes |
|---|---|---|---|
| `GET /stores/{storeId}/orders?query=` | STORE | `order:read` | Receipt lookup — by order number, customer name/phone, or date range. Needed before a return can even start. |
| `GET /stores/{storeId}/orders/{orderId}` | STORE | `order:read` | The order + its lines, to build the "what can still be returned" screen (sold qty − already-returned qty per line). |
| `POST /stores/{storeId}/orders/{orderId}/returns` | STORE | `order:refund` | Creates a `PENDING` return with lines + reason. Validates quantities against what's left to return. |
| `POST /stores/{storeId}/returns/{returnId}/complete` | STORE | `order:refund` | Flips to `COMPLETED`, stamps `refund_method` + `completed_at`, restocks lines where `restock = true`, computes `refund_total`. The actual money-back moment — for `refund_method = STORE_CREDIT` this is also where `customer.store_credit_balance` gets credited (Phase 1a), in the same transaction. |
| `POST /stores/{storeId}/returns/{returnId}/void` | STORE | `order:refund` | Cancels a `PENDING` return (mis-scanned item, customer changed their mind) — never touches stock or refund total since those only apply at `COMPLETED`. |
| `GET /stores/{storeId}/returns` | STORE | `order:read` | The Returns tab's list view (`docs/ui.md`). |

`order:refund` gates every write here — same permission already seeded for Manager, not Cashier. If
scope decision #6 lands on "Cashier can refund up to a cap," that's the ABAC layer from
`docs/post-mvp.md` sitting on top of this same gate, not a different permission.


## Phase 3: UI (Returns tab)

Per `docs/ui.md`, this is a store-context tab, shown when the user holds `order:refund` (or `order:read`
for a view-only Cashier, if that's wanted) **and** the store's effective features include `returns`
(`docs/overview.md`'s features table).

Flow:
1. **Find the sale** — search by order number, customer, or date (uses the `GET /orders?query=` lookup).
   No barcode/receipt-scanning hardware assumed for v1 — text search only.
2. **Pick lines + quantities** — shows each original line with (sold − already returned) as the max
   selectable quantity; can't select more than what's left.
3. **Reason** — a fixed dropdown (defective, wrong item, changed mind, ...) rather than pure free text,
   so returns are reportable later (`docs/post-mvp.md`-style reporting). `reason` on the table stays
   `TEXT` for now — either a small fixed code set enforced in the app, or upgrade to a lookup table if the
   list needs to be customer-editable (unlikely to be worth it at this size — keep it simple).
4. **Restock toggle per line** — defaults on; staff flips it off for damaged/unsellable returns.
5. **Refund method** — cash / card / **store credit** picker, same shape as the Sell checkout dialog for
   consistency; pre-filled with the original tender if scope decision #2 says match-only. Choosing store
   credit doesn't ask for a card/cash amount — it just shows what the customer's new balance will be
   after the credit lands (current `store_credit_balance` + `refund_total`).
6. **Confirm** → `POST .../returns` then immediately `.../complete` for the common one-step case (or
   split into "start return" / "manager approves" as two screens if scope decision #6 wants a manager
   gate distinct from the cashier initiating it).

The Returns **list/history** view also needs to support finding a specific return quickly once there's
any real volume: a **search by customer** (name/email, same substring match the Sell page's customer
picker already does) and a **date range filter** on `created_at`, alongside the existing store/permission
scoping. Both are client-side filters over the fetched list at this size — no need for server-side query
params until a store has enough return history for that to matter.

Reuses patterns already in the codebase: `TableLayout` for the Returns list, the same cash/card
`RadioGroup` pattern the Sell checkout dialog uses, `usePermission`/`PermissionGuard` for the route gate.


## Phase 4: explicitly deferred

- **A full store-credit transaction ledger** — the running `customer.store_credit_balance` (Phase 1a) is
  the v1 shape; a `customer_credit_transaction` table (issued/redeemed history, who/when/from-which-return)
  is only worth building once someone actually needs to audit *how* a balance got to what it is, not just
  what it currently is.
- **Redeeming store credit at checkout** — Phase 1a issues it from a return; spending it back down on a
  future Sell-page sale is its own small feature (a payment method beyond cash/card) that depends on
  Phase 0's real order persistence anyway.
- **Refund caps / ABAC** (`docs/post-mvp.md`'s `permission_condition` mechanism) — layers on top of the
  flat `order:refund` gate once that's live.
- **Online/mail-in RMA flow** for `store.type = 'ONLINE'` — request → authorization number → ship back →
  inspect → refund. Genuinely different shape (async, no item-in-hand moment), don't force it into the
  counter-return schema above.
- **Exchanges as a first-class transaction type** (vs. return + new sale as two orders) — only worth it
  if the two-order approach proves clunky in practice.


## Summary: build order

1. `sales_order` / `sales_order_product` migrated for real + `OrderController` — nothing else works
   without this.
2. Answer the 7 scope questions above (or confirm my leans).
3. Migrate `sales_order_return` / `sales_order_return_product` (Phase 1 schema).
4. `OrderRepository`/`ReturnRepository` + `ReturnController` (Phase 2).
5. Returns tab in the UI (Phase 3), gated by `order:refund` + the `returns` feature.
6. Everything in Phase 4 only after the above is live and being used.
