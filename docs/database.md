## Tables

PostgreSQL `CREATE TABLE` statements. Only keys and structurally-important fields are
included; descriptive columns (name, description, address, etc.) are added later.

Note: `user` is a reserved word in Postgres, so the table name is quoted as `"user"` in DDL.

## Auth / RBAC

```sql
CREATE TABLE user_type (
    user_type TEXT PRIMARY KEY CHECK (user_type IN ('ORGANIZATION', 'STORE'))
);


CREATE TABLE "user" (
    user_id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id    BIGINT NOT NULL REFERENCES organization (organization_id),  -- home org (set once, immutable)
    is_active          BOOLEAN NOT NULL DEFAULT TRUE,   -- account kill switch; false = all memberships suspended
    created_by_user_id BIGINT REFERENCES "user" (user_id),     -- the org admin who created this account
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE role (
    role_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    is_managed      BOOLEAN NOT NULL DEFAULT TRUE,
    organization_id BIGINT REFERENCES organization (organization_id),   -- NULL for managed roles
    user_type       TEXT NOT NULL REFERENCES user_type (user_type),     -- ORGANIZATION | STORE
    -- managed roles are ours (no owning org); custom roles belong to one org
    CHECK (
        (is_managed = TRUE  AND organization_id IS NULL)
        OR (is_managed = FALSE AND organization_id IS NOT NULL)
    )
);

CREATE TABLE permission (
    permission_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    resource      TEXT NOT NULL,   -- e.g. product, order, role
    action        TEXT NOT NULL,   -- e.g. read, create, refund, assign
    is_elevated   BOOLEAN NOT NULL DEFAULT FALSE,   -- TRUE = org roles only
    UNIQUE (resource, action)
);

CREATE TABLE role_permission (
    role_id       BIGINT NOT NULL REFERENCES role (role_id),
    permission_id BIGINT NOT NULL REFERENCES permission (permission_id),
    PRIMARY KEY (role_id, permission_id)
);


CREATE TABLE membership (
    membership_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id         BIGINT NOT NULL REFERENCES "user" (user_id),
    user_type       TEXT NOT NULL REFERENCES user_type (user_type),   -- ORGANIZATION | STORE: the kind of place
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    store_id        BIGINT REFERENCES store (store_id),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    deleted_at      TIMESTAMPTZ,   
    CHECK ((user_type = 'ORGANIZATION' AND store_id IS NULL)
        OR (user_type = 'STORE'        AND store_id IS NOT NULL))
);

CREATE UNIQUE INDEX membership_store_uq
    ON membership (user_id, store_id)
    WHERE store_id IS NOT NULL AND deleted_at IS NULL;

CREATE UNIQUE INDEX membership_org_uq
    ON membership (user_id)
    WHERE user_type = 'ORGANIZATION' AND deleted_at IS NULL;

CREATE TABLE membership_assignment (
    membership_id BIGINT NOT NULL REFERENCES membership (membership_id),
    role_id       BIGINT NOT NULL REFERENCES role (role_id),
    expires_at    TIMESTAMPTZ,   -- NULL = never expires
    PRIMARY KEY (membership_id, role_id)   -- same role can't be granted twice here
);

CREATE TABLE store_membership_detail (
    membership_id BIGINT PRIMARY KEY REFERENCES membership (membership_id),
    store_pin     TEXT
    -- ...other store-only member fields go here
);

CREATE TABLE organization_membership_detail (
    membership_id BIGINT PRIMARY KEY REFERENCES membership (membership_id)
    -- ...org-only member fields go here
);

```

## Plans / Features

```sql
CREATE TABLE plan (
    plan_id        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    price_per_store NUMERIC(12, 2) NOT NULL   -- billed per store: total = price_per_store × store count
);

CREATE TABLE feature (
    feature_id  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code        TEXT NOT NULL UNIQUE,   -- stable machine identifier, e.g. 'multi_store' (code checks this; never rename)
    label       TEXT NOT NULL,          -- human display text, e.g. 'Multi-store' (safe to change)
    description TEXT
);

CREATE TABLE plan_feature (
    plan_id    BIGINT NOT NULL REFERENCES plan (plan_id),
    feature_id BIGINT NOT NULL REFERENCES feature (feature_id),
    PRIMARY KEY (plan_id, feature_id)
);
```

## Organization

```sql
CREATE TABLE organization (
    organization_id  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    owner_user_id    BIGINT REFERENCES "user" (user_id),
    plan_id          BIGINT REFERENCES plan (plan_id),     -- the org's one plan; every store inherits its features
    default_store_id BIGINT REFERENCES store (store_id),
    allow_user_cross_memberships BOOLEAN NOT NULL DEFAULT FALSE,
    share_products  BOOLEAN NOT NULL DEFAULT FALSE,
    share_customers BOOLEAN NOT NULL DEFAULT FALSE
);
```

## Store

```sql
CREATE TABLE store (
    store_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id  BIGINT NOT NULL REFERENCES organization (organization_id),
    region           TEXT,     
    sub_region       TEXT      
);

CREATE TABLE store_tag (
    store_id BIGINT NOT NULL REFERENCES store (store_id),
    tag      TEXT   NOT NULL,
    PRIMARY KEY (store_id, tag)   -- a store can't have the same tag twice
);
```

## Products and customers (two tables per entity: store-level + shared)

Products and customers use **two tables each** — a store-level one and an org-level shared one:

- **Store-level** (`store_product`, `store_customer`) — always written; a row owned by one store.
- **Shared** (`product`, `customer`) — org-level records visible to every store in the org, written
  only when the org has turned sharing on (`organization.share_products` / `share_customers`).

By default sharing is off and each store keeps its own products and customers, isolated from the
others. When an org turns sharing on, a store's create dual-writes: the store-level row **and** a
shared org-level row it links up to, so the item is recognized at every store. See `tenancy.md` for
the model and `auth.md` for the write flow.

```sql
CREATE TABLE store_product (
    store_product_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id         BIGINT NOT NULL REFERENCES store (store_id),
    organization_id  BIGINT NOT NULL REFERENCES organization (organization_id), 
    product_id       BIGINT REFERENCES product (product_id),  
    sku              TEXT,
    quantity         INTEGER NOT NULL DEFAULT 0,   
    price            NUMERIC(12, 2),             
    UNIQUE (store_id, sku) 
);

CREATE TABLE product (
    product_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    sku             TEXT,
    UNIQUE (organization_id, sku)
);

CREATE TABLE store_customer (
    store_customer_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id          BIGINT NOT NULL REFERENCES store (store_id),
    organization_id   BIGINT NOT NULL REFERENCES organization (organization_id),
    customer_id       BIGINT REFERENCES customer (customer_id)   -- NULL = store-only; set = shared
);

CREATE TABLE customer (
    customer_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

CREATE TABLE supplier (
    supplier_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

CREATE TABLE purchase_order (
    purchase_order_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id   BIGINT NOT NULL REFERENCES organization (organization_id),
    store_id          BIGINT REFERENCES store (store_id),        
    supplier_id       BIGINT NOT NULL REFERENCES supplier (supplier_id),
    description       TEXT,                    
    total             NUMERIC(12, 2) NOT NULL, 
    status            TEXT NOT NULL,          
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE purchase_order_product (
    purchase_order_id BIGINT NOT NULL REFERENCES purchase_order (purchase_order_id),
    store_product_id  BIGINT NOT NULL REFERENCES store_product (store_product_id),
    quantity          INTEGER NOT NULL,
    unit_cost         NUMERIC(12, 2) NOT NULL,
    PRIMARY KEY (purchase_order_id, store_product_id)
);


CREATE TABLE expense (
    expense_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    store_id        BIGINT REFERENCES store (store_id),  
    supplier_id     BIGINT REFERENCES supplier (supplier_id),  
    category        TEXT NOT NULL,          
    description     TEXT,                   
    amount          NUMERIC(12, 2) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

```

## Store transactions (sales, returns)

Selling happens at a store. Sales reference the store's own products (`store_product`) and its own
customers, and the transaction belongs to the store.

```sql
CREATE TABLE sales_order (
    order_id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id          BIGINT NOT NULL REFERENCES store (store_id),
    store_customer_id BIGINT REFERENCES store_customer (store_customer_id),  
    status            TEXT NOT NULL   
);

CREATE TABLE sales_order_product (
    order_id         BIGINT NOT NULL REFERENCES sales_order (order_id),
    store_product_id BIGINT NOT NULL REFERENCES store_product (store_product_id), 
    quantity         INTEGER NOT NULL,
    unit_price       NUMERIC(12, 2) NOT NULL,  
    PRIMARY KEY (order_id, store_product_id)
);

CREATE TABLE sales_order_return (
    return_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id  BIGINT NOT NULL REFERENCES store (store_id),
    order_id  BIGINT NOT NULL REFERENCES sales_order (order_id)
);

CREATE TABLE sales_order_return_product (
    return_id        BIGINT NOT NULL REFERENCES sales_order_return (return_id),
    store_product_id BIGINT NOT NULL REFERENCES store_product (store_product_id),
    quantity         INTEGER NOT NULL,
    PRIMARY KEY (return_id, store_product_id)
);
```

## Indexes

Postgres indexes primary keys and unique constraints automatically, but **not** foreign-key
columns. Every FK we filter or join on needs an explicit index, or queries on the big tables
become full-table scans. (See the Performance section for why each is needed.)

A composite primary key already indexes its **leftmost** column, so `sales_order_product`
and `sales_order_return_product` don't need an extra index on `order_id` / `return_id`, only on
the other column.

```sql
-- RBAC
CREATE INDEX ON store           (organization_id);
CREATE INDEX ON "user"          (organization_id);   -- users in their home org
CREATE INDEX ON "user"          (created_by_user_id);
CREATE INDEX ON membership      (user_id);
CREATE INDEX ON membership_assignment (role_id);    -- membership_id covered by PK
CREATE INDEX ON role            (organization_id);  -- an org's custom roles (NULL for managed)
-- store_membership_detail / organization_membership_detail: membership_id is the PK,
-- already indexed; no extra index needed
CREATE INDEX ON role_permission (permission_id);   -- role_id covered by PK
CREATE INDEX ON permission_condition       (permission_id);   -- the menu for a permission (UI)
CREATE INDEX ON role_permission_condition  (organization_id, role_id);   -- a tenant's conditions for a role (enforcement lookup)
CREATE INDEX ON role_permission_condition  (permission_condition_id);
CREATE INDEX ON role_permission_condition  (store_id);   -- store-specific overrides

-- Organization
CREATE INDEX ON store        (organization_id, region);   -- group an org's stores by region
CREATE INDEX ON store_tag    (tag);                       -- find stores by tag (store_id covered by PK)
CREATE INDEX ON organization (owner_user_id);
CREATE INDEX ON organization (plan_id);
CREATE INDEX ON organization (default_store_id);
CREATE INDEX ON plan_feature (feature_id);         -- plan_id covered by PK

-- Org-level entities (suppliers; org-level money: purchases, expenses)
CREATE INDEX ON supplier (organization_id);
CREATE INDEX ON purchase_order (organization_id);
CREATE INDEX ON purchase_order (store_id);     -- a store's purchases (when attributed)
CREATE INDEX ON purchase_order (supplier_id);
CREATE INDEX ON expense        (organization_id);
CREATE INDEX ON expense        (store_id);     -- a store's expenses (when attributed)
CREATE INDEX ON expense        (supplier_id);

-- Products & customers (store-level + shared) + store sales (the big tables — these matter most)
CREATE INDEX ON store_product          (store_id);           -- a store's own products
CREATE INDEX ON store_product          (organization_id);
CREATE INDEX ON store_product          (product_id);         -- link to the shared record (when shared)
CREATE INDEX ON product                (organization_id);    -- the org's shared catalog
CREATE INDEX ON store_customer         (store_id);           -- a store's own customers
CREATE INDEX ON store_customer         (organization_id);
CREATE INDEX ON store_customer         (customer_id);        -- link to the shared record (when shared)
CREATE INDEX ON customer               (organization_id);    -- the org's shared customers
CREATE INDEX ON sales_order            (store_id);
CREATE INDEX ON sales_order            (store_customer_id);
CREATE INDEX ON sales_order_product    (store_product_id);   -- order_id covered by PK
CREATE INDEX ON sales_order_return         (store_id);
CREATE INDEX ON sales_order_return         (order_id);
CREATE INDEX ON sales_order_return_product (store_product_id);   -- return_id covered by PK

-- Composite indexes for the common sorted lists (list newest-first / alphabetical)
CREATE INDEX ON store_product   (store_id, sku);         -- a store's products, by SKU
CREATE INDEX ON sales_order     (store_id, order_id DESC);
CREATE INDEX ON purchase_order  (organization_id, purchase_order_id DESC);   -- an org's purchases, newest first
```

## Row-Level Security (tenant isolation floor)

The application already scopes every query by `organization_id` / `store_id` (see `auth.md`). RLS is
the **floor below that**: even if a hand-written query forgets its tenant filter, the database itself
refuses to return rows from another tenant. App scoping is the first line; RLS is the can't-be-wrong
backstop, so a single sloppy query can't cause a cross-tenant leak.

How it works: each request, after auth, sets the verified tenant from the request context onto the DB
session, and a policy on every tenant-scoped table filters to it automatically.

```sql
-- 1. The app connects as a NON-superuser, NON-owner role. Superusers and table owners BYPASS RLS,
--    so the app role must be neither, or the whole mechanism is silently skipped.
--    (Run migrations/admin as a separate privileged role; the request path uses this one.)
CREATE ROLE app_user LOGIN;

-- 2. Per request (inside the request's transaction), set the verified tenant from the context.
--    SET LOCAL = transaction-scoped, so it resets at commit/rollback and CANNOT leak across a
--    pooled connection to the next request (the classic RLS footgun). Never set from client input.
--    SET LOCAL app.current_org   = <context.organizationId>;
--    SET LOCAL app.current_store = <context.storeId or NULL>;

-- 3. Enable + FORCE RLS on every tenant-scoped table, then a policy filtering to the session tenant.
--    FORCE so even the table owner is subject to it. Example for an org-scoped and a store-scoped table:

ALTER TABLE supplier ENABLE ROW LEVEL SECURITY;
ALTER TABLE supplier FORCE  ROW LEVEL SECURITY;
CREATE POLICY supplier_tenant ON supplier
    USING (organization_id = current_setting('app.current_org')::bigint);

ALTER TABLE sales_order ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_order FORCE  ROW LEVEL SECURITY;
CREATE POLICY sales_order_tenant ON sales_order
    USING (store_id = current_setting('app.current_store')::bigint);

-- The store-level copies are always store-scoped; the shared records are org-scoped (so every store
-- in the org can resolve them). Both carry organization_id, so both are safe under RLS either way.
ALTER TABLE store_customer ENABLE ROW LEVEL SECURITY;
ALTER TABLE store_customer FORCE  ROW LEVEL SECURITY;
CREATE POLICY store_customer_tenant ON store_customer
    USING (store_id = current_setting('app.current_store')::bigint);

ALTER TABLE customer ENABLE ROW LEVEL SECURITY;    -- the SHARED record
ALTER TABLE customer FORCE  ROW LEVEL SECURITY;
CREATE POLICY customer_tenant ON customer
    USING (organization_id = current_setting('app.current_org')::bigint);

-- Apply the same pattern to every tenant-scoped table:
--   store-scoped (filter on store_id):        store_product, store_customer, sales_order,
--                                             sales_order_return, ...
--   org-scoped   (filter on organization_id): product (shared), customer (shared), supplier,
--                                             purchase_order, expense, role(custom), ...
-- Child/line tables (sales_order_product, ...) inherit isolation through their parent's FK, but add
-- a policy too if they can ever be queried directly.
```

Notes:
- **Defense in depth, not a replacement** — keep the app-level scoping; RLS catches the query that slips.
- **org vs store context** — an org user acts with `app.current_org` set (and reaches store rows because
  those stores are in their org — store policies can also allow `store.organization_id = app.current_org`);
  a store action sets `app.current_store`. Pick one consistent policy shape per table and document it.
- **Migrations bypass RLS** by design (run as the owner/privileged role), so schema changes aren't blocked.
