# Sales Receipt schema (immediate purchases)

Scope, for now: only the immediate-purchase path from
`docs/PurchaseFlow/order-processing-workflow.md`, cart, checkout, paid, done. Dealer Portal, credit
terms, quotes, invoices, and multi-step fulfillment are deferred. Nothing here blocks adding them later;
that architecture doc already treats them as separate tables, not baked into this one.


## Why "receipt," not "order"

A receipt records a completed transaction: a customer paid, right now, at a store, for specific items.
There's no draft state, no approval step, no partial fulfillment to track. That matches the workflow
doc's Immediate POS Sale profile exactly: if there's no in-between state to persist, don't model one.


## Prerequisite: points at `store_inventory`, not `store_product`

Same prerequisite as before this rewrite: `products.md` already replaced `store_product` with `product`
to `product_variant` to `store_inventory`, and this schema assumes that migration has landed. Every line
item below points at `store_inventory_id`.


## Schema

```sql
-- How the customer paid. CASH = physical bills/coins; CARD = any card-present or card-not-present
-- transaction (credit, debit, tap/contactless, chip, swipe, all settle the same way here); STORE_CREDIT =
-- applied from the customer's store_credit_balance (customers.md); OTHER = anything that doesn't fit the
-- above, e.g. a check, a gift card, an in-house account, a mobile wallet not worth its own value yet.
CREATE TYPE receipt_tender_type AS ENUM ('CASH', 'CARD', 'STORE_CREDIT', 'OTHER');

-- A completed, immediate sale at a store. No draft/pending state: a receipt only exists once payment has
-- been taken, matching docs/PurchaseFlow/order-processing-workflow.md's Immediate POS Sale profile.
CREATE TABLE sales_receipt (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id     UUID NOT NULL REFERENCES store (store_id),
    -- the customer, if attached; NULL for a walk-in / anonymous sale
    customer_id  UUID REFERENCES customer (customer_id),
    -- human-facing receipt number, shown to the customer (unique per store, see index below)
    number       TEXT NOT NULL,
    total        NUMERIC(12, 2) NOT NULL,
    -- total tax charged across every line on this receipt (one figure, not tracked per line)
    tax_amount   NUMERIC(12, 2) NOT NULL DEFAULT 0,
    tender_type  receipt_tender_type NOT NULL,
    paid_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ON sales_receipt (store_id);
CREATE UNIQUE INDEX sales_receipt_number_per_store ON sales_receipt (store_id, number);

-- A line on a receipt: one variant, its quantity, and price snapshotted at sale time. product_name /
-- variant_name / sku are copied from product / product_variant at the moment of sale, not read live, so
-- a receipt still shows what was actually sold if the product is later renamed, re-categorized, or
-- deleted. store_inventory_id stays for the live link (returns, reporting); the text columns are the
-- historical record.
CREATE TABLE sales_receipt_line (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    receipt_id          UUID NOT NULL REFERENCES sales_receipt (id),
    store_inventory_id  UUID NOT NULL REFERENCES store_inventory (store_inventory_id),
    -- snapshot of product.name at sale time
    product_name        TEXT NOT NULL,
    -- snapshot of product_variant.name at sale time, e.g. 'Large / Black'
    variant_name        TEXT NOT NULL,
    -- snapshot of product_variant.sku at sale time
    sku                 TEXT NOT NULL,
    qty                 INTEGER NOT NULL,
    unit_price          NUMERIC(12, 2) NOT NULL
);
CREATE INDEX ON sales_receipt_line (receipt_id);
```


## Field notes

| Field | Why it's shaped that way |
|---|---|
| `sales_receipt.number` | The human-facing number printed on the receipt, distinct from the internal `id`. Unique per store, not per org, since two stores' receipt sequences don't need to relate to each other. |
| `sales_receipt.tax_amount` | Total tax charged on the receipt, alongside `total`. Tracked at the receipt level, not per line, since nothing here needs a per-line tax breakdown. |
| `sales_receipt.tender_type` | How the customer paid: `CASH` (bills/coins), `CARD` (credit, debit, tap, chip, swipe, all one value), `STORE_CREDIT` (drawn from `customer.store_credit_balance`), or `OTHER` (check, gift card, in-house account). A fixed enum rather than a `payment` table, since an immediate purchase has exactly one payment event, the receipt itself. |
| `sales_receipt.paid_at` | Defaults to `now()`. There's no separate "submitted" or "completed" timestamp because the receipt doesn't exist until the sale is done. |
| `sales_receipt_line.product_name` / `variant_name` / `sku` | Copied from `product` / `product_variant` at the moment of sale, not read live through `store_inventory_id`. A receipt is a historical record; if the product is renamed, recategorized, or deleted afterward, the receipt still shows exactly what the customer bought and paid for. |


## Stock

Creating a `sales_receipt_line` deducts `store_inventory.stock` directly, in the same transaction as the
receipt. There's no reservation step and no separate inventory ledger table for this MVP scope, matching
CLAUDE.md's simple-over-flexible default: an immediate sale has nothing to reserve ahead of, it happens
and is deducted in one step.


## Out of scope for now

These stay part of the long-term architecture in `docs/PurchaseFlow/order-processing-workflow.md`, but
nothing in this schema needs to anticipate them structurally. They're additive later, not a rework of
what's here:

- Dealer Portal / B2B credit orders, PO references, credit approval
- Quotes
- Multi-step or partial fulfillment
- Invoices as a separate financial document (a receipt already is the record of a completed, paid sale)
- Payment as a separate multi-event table (`tender_type` + `paid_at` cover a single, immediate payment)


## Open questions

1. **`organization_id`.** Every other tenant-scoped table in `database.md` stamps `organization_id`
   directly, for RLS, even when a `store_id` is also present. This schema as given doesn't. It's
   resolvable through `store_id` to `store.organization_id`, but worth deciding whether to add the column
   directly before this is migrated, for RLS consistency with the rest of the schema.
2. **Returns.** `database.md`'s `sales_order_return` / `sales_order_return_product` reference
   `sales_order` and the retired `store_product_id`. Once returns are built against this model, they'd
   need to reference `sales_receipt` and `store_inventory_id` instead. Not designed here since it wasn't
   asked for.


## Summary: build order

1. Land `products.md`'s `product` / `product_variant` / `store_inventory` migration; this schema depends
   on `store_inventory_id` existing.
2. Add `sales_receipt` and `sales_receipt_line`.
3. Wire checkout to deduct `store_inventory.stock` and insert the receipt and its lines in one
   transaction.
