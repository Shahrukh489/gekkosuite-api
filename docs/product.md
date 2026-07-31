# Product schema

A product lives in two tables:

| Table | Written | Scope |
|---|---|---|
| `store_product` | Always, on every product create | One store, its own stock, price, and cost |
| `product` | Only when `organization.allow_share_products` is on (off by default) | Org-wide, one shared identity, no stock/price/cost |

`store_product` never needs a `product` row to function. Every field a till or a storefront needs
(name, category, brand, variant options) lives directly on `store_product`. The shared `product` table
only exists to let an org recognize "the same item" across multiple stores; stock, price, and cost
always stay per-store regardless of sharing (`CLAUDE.md`).

Migrated in `tools/GekkoSuite.Database/Migrations/1_0_0.sql`.


## `store_product` — the store's own copy (always written)

| Field | Type | Required | Notes |
|---|---|---|---|
| `store_product_id` | UUID | PK | |
| `store_id` | UUID | Yes | FK → `store` |
| `organization_id` | UUID | Yes | FK → `organization` (tenant stamp, for RLS) |
| `product_id` | UUID | No | FK → `product`; set only when sharing is on |
| `name` | VARCHAR(256) | Yes | Display name |
| `description` | VARCHAR(1024) | No | |
| `sku` | VARCHAR(64) | No | This store's own product code. Unique per store |
| `barcode` | VARCHAR(64) | No | Manufacturer UPC/EAN — what a barcode scan looks up, not `sku`. Unique per store |
| `category` | VARCHAR(128) | No | Free text, e.g. `Beverages` |
| `brand` | VARCHAR(128) | No | Free text, e.g. `Lavazza` |
| `variant_option_{one,two,three}_name` / `_value` | VARCHAR | No | Up to 3 variant dimensions, e.g. `Flavor` / `Vanilla` — see [Variants](#variants) |
| `price` | NUMERIC | Yes | This store's selling price |
| `cost` | NUMERIC | No | What this store paid per unit — margin reporting, never shown at checkout |
| `stock` | INTEGER | Yes | Units on hand at this store. Can go negative (oversold/backorder) — never add a `>= 0` check |
| `track_inventory` | BOOLEAN | Yes, default `TRUE` | `FALSE` = stock never decrements (e.g. a service, not a good) |
| `is_taxable` | BOOLEAN | Yes, default `TRUE` | `FALSE` = always tax-exempt, `tax_rate` ignored |
| `tax_rate` | NUMERIC(5,2) | Yes, default `0` | Percentage, e.g. `8.25` |
| `reorder_point` | INTEGER | No | Low-stock alert threshold |
| `is_active` | BOOLEAN | Yes | Sellable toggle, independent of soft-delete |
| `created_at` / `updated_at` | TIMESTAMPTZ | Yes | |
| `is_deleted` / `deleted_at` | BOOLEAN / TIMESTAMPTZ | Yes / No | Soft-delete |

**Indexes:**
- `store_id`
- Unique on `(store_id, sku)`, where SKU is set and the row is not deleted
- Unique on `(store_id, barcode)`, where barcode is set and the row is not deleted
- `product_id`, where set


## `product` — the shared, org-wide identity (written only when sharing is on)

| Field | Type | Required | Notes |
|---|---|---|---|
| `product_id` | UUID | PK | |
| `organization_id` | UUID | Yes | FK → `organization` |
| `name` | VARCHAR(256) | Yes | |
| `description` | VARCHAR(1024) | No | |
| `category` | VARCHAR(128) | No | |
| `brand` | VARCHAR(128) | No | |
| `variant_option_{one,two,three}_name` / `_value` | VARCHAR | No | Mirrors `store_product`'s variant fields |
| `created_at` / `updated_at` | TIMESTAMPTZ | Yes | |
| `is_deleted` / `deleted_at` | BOOLEAN / TIMESTAMPTZ | Yes / No | Soft-delete |

**Indexes:** `organization_id`.

There are no `price`, `cost`, `stock`, or tax fields on this table, by design. Those stay on
`store_product` only, since every store sets its own.


## How the two tables connect

The only link is `store_product.product_id` → `product.product_id`, nullable, set only when sharing is
on. Multiple `store_product` rows (one per store) can point at the same `product_id` — that's what lets
the org ask "how many of *this* item do we sell, across every store" instead of fuzzy-matching by name.

```mermaid
flowchart LR
    P[("product<br/>(shared, org-wide)")]
    SP1["store_product<br/>Store A"]
    SP2["store_product<br/>Store B"]
    SP3["store_product<br/>Store C — sharing off,<br/>no product_id"]

    SP1 -->|product_id| P
    SP2 -->|product_id| P
    SP3 -.no link.-x P
```

Who writes what, and when (`auth.md`):

| `allow_share_products` | `POST /stores/{storeId}/products` does |
|---|---|
| Off (default) | `INSERT store_product` only |
| On | `INSERT product`, then `INSERT store_product` linked to it, in the same transaction |

`product:create` is an ordinary, non-elevated store permission. A store Manager can only create at
their own store, while an org-level user can create into any store in the org the same way, just with a
different `{storeId}`. That last case, one org-level user seeding the same item into several stores, is
the main real-world reason the shared table exists.

Known gap: creating with sharing on always inserts a new `product` row; it never looks up an existing
shared product to link to instead. Two stores independently creating "Bic Lighter Blue" get two separate
shared rows, not one. This needs a matching step (SKU or barcode, or an explicit "link to existing" UI
action) before sharing ships. See `auth.md`'s open risk on editing and deleting a shared record, which
has the same root cause.


## Variants

There's no separate variant table. Each variant is its own complete `store_product` row, grouped only by
matching `name` — no foreign key, no parent row. Example (`local/mock_data.sql`):

| `name` | `sku` | `variant_option_one_name` | `variant_option_one_value` | `stock` |
|---|---|---|---|---|
| Flavored Syrup 750ml | SYRUP-VAN | Flavor | Vanilla | 24 |
| Flavored Syrup 750ml | SYRUP-CAR | Flavor | Caramel | 18 |
| Flavored Syrup 750ml | SYRUP-HAZ | Flavor | Hazelnut | 0 |

- The UI clusters `store_product` rows sharing a `name` into one card with an option picker. Three slots
  (`_one`, `_two`, `_three`) cover products that vary along more than one dimension, such as flavor and
  size together.
- Renaming a product means updating every variant row's `name` individually — no single row owns "the
  product name."
- Each variant sells out, goes inactive, or changes price independently, since they're full rows and not
  child lines. Hazelnut hitting `stock = 0` above doesn't touch Vanilla or Caramel.
- If sharing is on, each variant row links to its own `product_id` independently. Grouping stays a
  `store_product`-level, name-matching operation regardless of whether sharing is on.


## What else `store_product` connects to

| Connects to | Relationship |
|---|---|
| `organization` | `store_product.organization_id` / `product.organization_id` — tenant boundary |
| `store` | `store_product.store_id` — the one store that owns this row |
| `product` | `store_product.product_id` — optional shared identity, see above |
| `supplier` | Not wired up yet — `supplier_id` is planned on both tables but deferred until `supplier` itself is migrated. See Known limitations |
| Sales, purchases, returns | Not referenced directly from `store_product` — they reference it, and drive `stock` up or down. See below |

### How `store_product.stock` changes

Four flows touch `stock`, each documented elsewhere in this repo. This is the one place they're pulled
into a single picture:

```mermaid
flowchart LR
    QTY(("store_product<br/>.stock"))

    PO["Purchase order<br/>status → RECEIVED"] -->|"+ purchase_order_product.quantity"| QTY
    RET["Return line<br/>return COMPLETED, restock = true"] -->|"+ sales_order_return_product.quantity"| QTY
    MANUAL["Manual edit<br/>(stock count, initial import)"] -->|"set directly"| QTY
    QTY -->|"− sales_order_product.quantity"| SALE["Sale<br/>order status → COMPLETED"]

    style QTY fill:#dbeafe,stroke:#93c5fd
```

| Flow | Fires on | Effect | Source |
|---|---|---|---|
| Receiving inventory | `purchase_order.status` → `RECEIVED` | `+= purchase_order_product.quantity` per line | `database.md` |
| Selling | `sales_order.status` → `COMPLETED` | `-= sales_order_product.quantity` per line | `returns.md` Phase 0 |
| Returning | `sales_order_return.status` → `COMPLETED`, lines with `restock = true` | `+= sales_order_return_product.quantity` | `returns.md` Phase 1 |
| Manual correction | Staff edits the product directly | Set to whatever they enter | Initial import, recounts, shrinkage |

The change always happens in the same transaction as the status flip, not when the line is first
inserted. A `DRAFT` purchase order or `OPEN` sale has its line items on record but hasn't touched stock
yet. A `CANCELLED` order or `VOIDED` sale/return never crosses that line, so there's nothing to undo.
This is also why negative `stock` is expected rather than a bug: a sale can complete even when the count
is already wrong, and the negative number is the signal that a recount is overdue.


## Known limitations

- SKU casing isn't normalized. `HIDRIPS`, `HiDrips`, and `Hidrips` can coexist as three different SKUs
  under today's case-sensitive unique index. Normalize casing on write, or switch to `citext`.
- No dedupe on import. Literal duplicate rows (same barcode-as-SKU, different id) need an explicit
  merge decision, not a raw insert.
- `category` and `brand` are free text, not controlled vocabularies. Fine for MVP, but don't roll
  them up for reporting without trimming and normalizing first.
- `supplier_id` isn't wired up. Planned as a real FK on both tables once `supplier` is migrated;
  today there's no supplier link at all.
- Sharing can't match an existing shared product — see "Known gap" above.
