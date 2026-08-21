-- ============================================================================
-- V1.0.3 — sales receipts. Backs GET /stores/{storeId}/orders and
-- GET /stores/{storeId}/orders/{orderId} — a completed sale is the record a
-- receipt is printed/looked up from. Extends docs/database.md's `sales_order`
-- / `sales_order_product` sketch (docs/returns.md's Phase 0 prerequisite for
-- returns) with the `payment_method` column returns.md already flags as
-- needed, plus `organization_id` on `sales_order` for the tenant-stamped
-- WHERE-clause scoping every other store-scoped table uses (BaseRepository /
-- product, customer, ...). Per-line discount/tax are left out of
-- sales_order_product — not needed until a return/refund flow reads them.
-- ============================================================================

-- Lifecycle of a sale. OPEN = in progress; COMPLETED = paid/finalized; VOIDED = cancelled.
CREATE TYPE order_status AS ENUM ('OPEN', 'COMPLETED', 'VOIDED');

-- How a sale was paid.
CREATE TYPE payment_method AS ENUM ('CASH', 'CARD');

-- A sale rung up at a store — the record a receipt is printed/looked up from.
CREATE TABLE sales_order (
    -- order id
    order_id          UUID PRIMARY KEY,
    -- the store the sale belongs to
    store_id          UUID NOT NULL REFERENCES store (store_id),
    -- the owning org (the tenant boundary — stamped for tenant-scoped queries, like every other store-scoped table)
    organization_id   UUID NOT NULL REFERENCES organization (organization_id),
    -- human-readable receipt number shown to the customer/cashier; unique per store (index below)
    order_number      VARCHAR(32) NOT NULL,
    -- the customer, if attached; NULL for a walk-in / anonymous sale
    store_customer_id UUID REFERENCES store_customer (store_customer_id),
    -- the user (cashier) who rang the sale; NULL if unknown
    sold_by_user_id   UUID REFERENCES user_account (user_id),
    -- OPEN | COMPLETED | VOIDED
    status            order_status NOT NULL,
    -- CASH | CARD — how the sale was paid
    payment_method    payment_method NOT NULL,
    -- money breakdown (all snapshotted; total = subtotal - discount_total + tax_total)
    subtotal          NUMERIC(12, 2) NOT NULL,
    discount_total    NUMERIC(12, 2) NOT NULL,
    tax_total         NUMERIC(12, 2) NOT NULL,
    total             NUMERIC(12, 2) NOT NULL,
    -- when the sale was made (stored UTC)
    created_at        TIMESTAMPTZ NOT NULL
);
CREATE INDEX ON sales_order (store_id);   -- a store's receipts
-- a receipt number is unique within a store
CREATE UNIQUE INDEX sales_order_number_per_store ON sales_order (store_id, order_number);

-- A line on a sale: one product, its quantity, and the price at time of sale.
CREATE TABLE sales_order_product (
    -- the order this line belongs to
    order_id         UUID NOT NULL REFERENCES sales_order (order_id),
    -- the store product sold
    store_product_id UUID NOT NULL REFERENCES store_product (store_product_id),
    -- how many units
    quantity         INTEGER NOT NULL,
    -- price per unit, snapshotted at sale time (not read live from store_product)
    unit_price       NUMERIC(12, 2) NOT NULL,
    -- one row per product per order
    PRIMARY KEY (order_id, store_product_id)
);
