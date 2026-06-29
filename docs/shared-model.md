@TODO: Review


# Org-wide sharing of products and customers

In MVP, products and customers are **store-owned** — each store keeps its own, isolated from other
stores. This is the simplest, safest default. Some organizations will want the opposite: **one shared
catalog and/or one shared customer base** across all their stores (a customer recognized at any store,
a product carried by several).

The plan: an **org-level setting** to turn on sharing — per entity (customers, products, or both).
When on, an item created at one store can be promoted to org-wide visibility. Promotion is **gated by
org approval**: the change raises a notification an org admin approves (or the org configures
**auto-approve** to skip it). Open sub-questions for then:

- **Dedup at share time** — matching the same customer (phone/email) or product (SKU) across stores so
  sharing links rather than duplicates.
- **Granularity** — all-or-nothing vs. per-entity (share customers but not catalog).
- **Direction** — does sharing make a single org-owned record, or keep store records linked into a
  shared view.

This is deliberately deferred: build it when an organization actually needs cross-store sharing, not
before.

## How shared customers work (the safe pattern)

When an org turns sharing on for customers, a store user can create/edit a customer that **all** the
org's stores see. This does **not** need any new actor-type branching — it's the same flat permission
check plus the org's setting. The rules:

1. **The org admin enables it.** A `customer:create` request from a store user is only allowed to write
   a *shared* (org-level) customer if the org's **share-customers setting is on**. Off (the default) →
   the customer stays store-owned. The setting is the switch; the user's type doesn't change.

2. **The permission must NOT be elevated.** `customer:create` stays a normal (non-elevated) permission
   so it can live in a **store role**. (If it were elevated, store users couldn't hold it at all — which
   is the opposite of what shared-customers wants.) So the only thing gating store users from customers
   is whether their role includes the permission — not the permission's level.

3. **The check at the endpoint** is just two things — no branch on `user_type`:
   ```
   allow create/edit customer if:
     a. the user holds customer:create (or customer:edit) via a LIVE role, AND
     b. customer.organization_id == context.organizationId      (tenant boundary)
   ```
   That's it. A store user with the permission qualifies; the customer must be in their own org.

4. **`organization_id` comes from the token, never the request body.** A new customer's
   `organization_id` is set server-side from `context.organizationId`. A store user can therefore only
   ever create a customer in *their own* org — they can't pass a different org id to write into another
   tenant. This single rule is what makes the flat check safe.

5. **RLS on `customer` follows the mode:**
   - **Shared mode (setting on):** `customer` is org-scoped — the RLS policy filters on
     `app.current_org` (`organization_id = current_setting('app.current_org')`). Any store user in the
     org sees the shared customers.
   - **Isolated mode (setting off, the MVP default):** `customer` is store-scoped — the policy filters on
     `app.current_store` (`store_id = current_setting('app.current_store')`), so a store user sees only
     their store's customers.

   So the table carries both `organization_id` (always — the tenant boundary) and `store_id` (the owning
   store in isolated mode), and the RLS policy shape is chosen by the org's sharing setting.

The same pattern applies to shared products (an org-wide catalog) — flat permission + the org's setting
+ org-from-token + org-scoped RLS when sharing is on.


## The two tables per entity

Sharing is modeled with **two tables** per entity — a store-level one and an org-level one:

- **Store-level (always written):** `customer_store`, `product_store` — a row owned by one store.
- **Org-level (written only when sharing is on):** `customer`, `product` — a shared record visible to
  the whole org.

```sql
-- Store-level: the store's own copy. customer_id links up to the shared record when sharing is on.
CREATE TABLE customer_store (
    customer_store_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id          BIGINT NOT NULL REFERENCES store (store_id),
    organization_id   BIGINT NOT NULL REFERENCES organization (organization_id),  -- boundary / RLS
    customer_id       BIGINT REFERENCES customer (customer_id)   -- NULL = store-only; set = shared
    -- ...customer fields...
);

-- Org-level: the org-wide shared record.
CREATE TABLE customer (
    customer_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
    -- ...customer fields...
);

-- Products mirror this exactly:
CREATE TABLE product_store (
    product_store_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id         BIGINT NOT NULL REFERENCES store (store_id),
    organization_id  BIGINT NOT NULL REFERENCES organization (organization_id),
    product_id       BIGINT REFERENCES product (product_id),   -- NULL = store-only; set = shared
    sku              TEXT,
    quantity         INTEGER NOT NULL DEFAULT 0,   -- this store's stock
    price            NUMERIC(12, 2)                -- this store's price
);

CREATE TABLE product (
    product_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    sku             TEXT,
    UNIQUE (organization_id, sku)
);
```


## How we insert (the dual write)

The **store-level row is always written**; the org-level row is added only when sharing is on, in the
**same transaction**, and the store row links to it.

**Sharing OFF (default — isolated):**
- Insert into `customer_store` (or `product_store`) only.
- `customer_id` / `product_id` stays NULL — there's no shared record; other stores don't see it.


**Sharing ON (shared):** one transaction —
1. Insert the org-level row (`customer` / `product`) with `organization_id = context.organizationId`.
2. Insert the store-level row (`customer_store` / `product_store`) with its `customer_id` /
   `product_id` set to the org row from step 1.

```
create customer (store user):
  if org.share_customers = OFF:
  TODO null is wrong for customer_id here
      INSERT customer_store (store_id, organization_id = context.org, customer_id = NULL, ...)

  if org.share_customers = ON:                                     -- one transaction
      INSERT customer (organization_id = context.org, ...)              -> customer_id
      INSERT customer_store (store_id, organization_id = context.org, customer_id, ...)

create product (store user):
  if org.share_products = OFF:
      INSERT product_store (store_id, organization_id = context.org, product_id = NULL, sku, qty, price)

  if org.share_products = ON:                                      -- one transaction
      INSERT product (organization_id = context.org, sku, ...)         -> product_id
      INSERT product_store (store_id, organization_id = context.org, product_id, sku, qty, price)
```

Notes:
- **`organization_id` on both rows comes from the token** (`context.organizationId`), never the request
  body — so a store user can only ever write into their own org.
- For **products**, the store-level row always keeps the store's own **stock and price** (`quantity`,
  `price`); the shared `product` row holds only the org-wide **identity** (SKU). So even when shared,
  each store still prices and stocks independently.
- For **customers**, the shared `customer` row is the org-wide account; the `customer_store` row is the
  store's link to it (and any store-local customer fields).


## RLS — both tables, policy by level

The store-level tables are always **store-scoped**; the org-level shared tables are **org-scoped**:

```sql
-- store-level copy: always store-scoped
CREATE POLICY customer_store_tenant ON customer_store
    USING (store_id = current_setting('app.current_store')::bigint);
CREATE POLICY product_store_tenant ON product_store
    USING (store_id = current_setting('app.current_store')::bigint);

-- org-level shared record: org-scoped (any store user in the org can resolve it)
CREATE POLICY customer_tenant ON customer
    USING (organization_id = current_setting('app.current_org')::bigint);
CREATE POLICY product_tenant ON product
    USING (organization_id = current_setting('app.current_org')::bigint);
```

- **Sharing OFF:** a store reads from the **store-level** table only (`app.current_store`) — it never
  sees another store's rows, and the org-level table has no row to share.
- **Sharing ON:** the shared record resolves through the **org-level** table (`app.current_org`), so
  every store user in the org sees it; the store-level row still scopes that store's own copy
  (its stock/price for products, its link for customers).

`app.current_org` is always set per request (from the token); `app.current_store` is set on store
actions. Migrations run as the privileged role and bypass RLS, as elsewhere.

