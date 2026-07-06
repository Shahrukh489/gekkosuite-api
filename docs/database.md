```sql
-- How a store sells: a storefront kind. 
CREATE TYPE store_type AS ENUM ('ONLINE', 'PHYSICAL');

-- The scope a membership/role operates at: the two kinds of place a person can act at.
CREATE TYPE scope AS ENUM ('ORGANIZATION', 'STORE');

-- The organization: the business and the tenant (unit of isolation). Owns stores, users, and settings.
CREATE TABLE organization (
    -- tenant id; 
    organization_id  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- the business's display name (required)
    name             TEXT NOT NULL,
    -- optional free-text note about the business
    description      TEXT,
    -- the owner; full access that can't be stripped (transfer only)
    owner_user_id    UUID REFERENCES "user" (user_id),
    -- the org's one plan; every store inherits its features
    plan_id          UUID REFERENCES plan (plan_id),
    -- store to land in by default (e.g. single-store orgs)
    default_store_id UUID REFERENCES store (store_id),
    -- can one user hold both org and store memberships? (off = locked to one kind)
    allow_user_cross_memberships BOOLEAN NOT NULL DEFAULT FALSE,
    -- org-wide product sharing (off = each store's catalog is its own)
    allow_share_products  BOOLEAN NOT NULL DEFAULT FALSE,
    -- org-wide customer sharing (off = each store's customers are its own)
    allow_share_customers BOOLEAN NOT NULL DEFAULT FALSE,
    -- when the org was onboarded (stored UTC)
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- soft-delete flag; TRUE = org removed but kept for history
    is_deleted       BOOLEAN NOT NULL DEFAULT FALSE,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ
);

-- A store: the business unit where selling happens. Owned by one org;
CREATE TABLE store (
    -- store id; 
    store_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- owning org (the tenant)
    organization_id  UUID NOT NULL REFERENCES organization (organization_id),
    -- store display name 
    name             TEXT NOT NULL,
    -- ONLINE | PHYSICAL: how the store sells
    type             store_type NOT NULL DEFAULT 'PHYSICAL',
    -- the org's default store; must agree with organization.default_store_id
    is_default       BOOLEAN NOT NULL DEFAULT FALSE,
    -- description / notes about the store
    description      TEXT,
    -- physical location (all NULL for an ONLINE store) --
    -- street address
    address    TEXT,
    -- geographic city
    city             TEXT,
    -- geographic state/province
    state            TEXT,
    -- postal/zip code — TEXT to preserve leading zeros and non-numeric formats
    postal_code      TEXT,
    -- ISO country code, e.g. 'US'
    country          TEXT,
    -- ISO 4217 currency the store sells in, e.g. 'USD'
    currency         TEXT,
    -- store contact phone (TEXT: '+', spaces, extensions)
    phone            TEXT,
    -- store contact email
    email            TEXT,
    -- when the store was created (stored UTC)
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- soft-delete flag; TRUE = store removed but kept for history
    is_deleted       BOOLEAN NOT NULL DEFAULT FALSE,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ,
    -- can't delete the default store; reassign the default to another store first
    CHECK (NOT (is_default AND is_deleted))
);

-- At most one default store per org. 
CREATE UNIQUE INDEX store_one_default_per_org
    ON store (organization_id)
    WHERE is_default;


-- A user: one login per person. Where they can act comes from their memberships (see auth.md).
CREATE TABLE "user" (
    -- user id
    user_id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- home org (set once, immutable) — the tenant boundary
    organization_id    UUID NOT NULL REFERENCES organization (organization_id),
    -- login identifier; globally unique — one email = one account (see UNIQUE below)
    email              TEXT NOT NULL,
    -- password hash — slow salted KDF (argon2id/bcrypt); NEVER store plaintext (see auth.md)
    password      TEXT NOT NULL,
    -- display name for the UI (e.g. the access-list views)
    name               TEXT NOT NULL,
    -- contact phone (TEXT: '+', spaces, extensions)
    phone              TEXT,
    -- account kill switch; false = all memberships suspended (the way to revoke, not delete)
    is_active          BOOLEAN NOT NULL DEFAULT TRUE,
    -- the org admin who created this account
    created_by_user_id UUID REFERENCES "user" (user_id),
    -- when the account was created (stored UTC)
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted         BOOLEAN NOT NULL DEFAULT FALSE,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at         TIMESTAMPTZ,
    -- one email = one account across the whole system
    UNIQUE (email)
);


-- A role: a named bundle of permissions. Either a managed role we ship, or an org's own custom role.
CREATE TABLE role (
    -- role id
    role_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- display name shown in the role picker, e.g. 'Cashier', 'Org Admin'
    name            TEXT NOT NULL,
    -- optional human description of what the role is for
    description     TEXT,
    -- TRUE = system role we ship (org-wide); FALSE = an org's own custom role
    is_managed      BOOLEAN NOT NULL DEFAULT TRUE,
    -- owning org for a custom role; NULL for managed roles (see CHECK)
    organization_id UUID REFERENCES organization (organization_id),
    -- ORGANIZATION | STORE: the level this role attaches at (must match the membership's scope)
    scope scope NOT NULL,
    -- managed roles have no org; custom roles must have one
    CHECK (
        (is_managed = TRUE  AND organization_id IS NULL)
        OR (is_managed = FALSE AND organization_id IS NOT NULL)
    )
);

-- no two custom roles in the same org share a name
CREATE UNIQUE INDEX role_org_name_uq
    ON role (organization_id, name)
    WHERE organization_id IS NOT NULL;

-- managed (system) role names are unique among themselves
CREATE UNIQUE INDEX role_managed_name_uq
    ON role (name)
    WHERE is_managed;

-- A permission: one allowed action, like product:read. The atomic unit roles are built from.
CREATE TABLE permission (
    -- permission id
    permission_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- the thing acted on, e.g. product, order, role
    resource      TEXT NOT NULL,
    -- what may be done to it, e.g. read, create, refund, assign
    action        TEXT NOT NULL,
    -- optional human description for the role-builder UI
    description   TEXT,
    -- TRUE = for org roles only, FALSE = Can be added to both Store and Org Roles;
    is_elevated   BOOLEAN NOT NULL DEFAULT FALSE,
    -- resource + action is the permission's natural key, e.g. (product, read)
    UNIQUE (resource, action)
);

-- role_permission: which permissions a role grants (many-to-many). Guarded on write (see auth.md).
CREATE TABLE role_permission (
    -- the role
    role_id       UUID NOT NULL REFERENCES role (role_id),
    -- the permission it grants
    permission_id UUID NOT NULL REFERENCES permission (permission_id),
    -- a role can't list the same permission twice
    PRIMARY KEY (role_id, permission_id)
);


```

# Auth / RBAC

```sql




CREATE TABLE membership (
    membership_id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES "user" (user_id),
    scope scope NOT NULL,     -- ORGANIZATION | STORE: the kind of place
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    store_id        UUID REFERENCES store (store_id),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    deleted_at      TIMESTAMPTZ,
    CHECK ((scope = 'ORGANIZATION' AND store_id IS NULL)
        OR (scope = 'STORE'        AND store_id IS NOT NULL))
);

CREATE UNIQUE INDEX membership_store_uq
    ON membership (user_id, store_id)
    WHERE store_id IS NOT NULL AND deleted_at IS NULL;

CREATE UNIQUE INDEX membership_org_uq
    ON membership (user_id)
    WHERE scope = 'ORGANIZATION' AND deleted_at IS NULL;

CREATE TABLE membership_assignment (
    membership_id UUID NOT NULL REFERENCES membership (membership_id),
    role_id       UUID NOT NULL REFERENCES role (role_id),
    expires_at    TIMESTAMPTZ,   -- NULL = never expires
    PRIMARY KEY (membership_id, role_id)   -- same role can't be granted twice here
);

CREATE TABLE store_membership_detail (
    membership_id  UUID PRIMARY KEY REFERENCES membership (membership_id),
    store_pin_hash TEXT   -- register PIN, stored as a salted KDF hash (never plaintext) — see auth.md
    -- ...other store-only member fields go here
);

CREATE TABLE organization_membership_detail (
    membership_id UUID PRIMARY KEY REFERENCES membership (membership_id)
    -- ...org-only member fields go here
);

```

## Plans / Features

```sql
CREATE TABLE plan (
    plan_id        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    price_per_store NUMERIC(12, 2) NOT NULL   -- billed per store: total = price_per_store × store count
);

CREATE TABLE feature (
    feature_id  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code        TEXT NOT NULL UNIQUE,   -- stable machine identifier, e.g. 'multi_store' (code checks this; never rename)
    label       TEXT NOT NULL,          -- human display text, e.g. 'Multi-store' (safe to change)
    description TEXT
);

CREATE TABLE plan_feature (
    plan_id    UUID NOT NULL REFERENCES plan (plan_id),
    feature_id UUID NOT NULL REFERENCES feature (feature_id),
    PRIMARY KEY (plan_id, feature_id)
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
    store_product_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id         UUID NOT NULL REFERENCES store (store_id),
    organization_id  UUID NOT NULL REFERENCES organization (organization_id),
    product_id       UUID REFERENCES product (product_id),
    sku              TEXT,
    quantity         INTEGER NOT NULL DEFAULT 0,
    price            NUMERIC(12, 2),
    UNIQUE (store_id, sku)
);

CREATE TABLE product (
    product_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    sku             TEXT,
    UNIQUE (organization_id, sku)
);

CREATE TABLE store_customer (
    store_customer_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id          UUID NOT NULL REFERENCES store (store_id),
    organization_id   UUID NOT NULL REFERENCES organization (organization_id),
    customer_id       UUID REFERENCES customer (customer_id)   -- NULL = store-only; set = shared
);

CREATE TABLE customer (
    customer_id     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organization (organization_id)
);

CREATE TABLE supplier (
    supplier_id     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organization (organization_id)
);

CREATE TABLE purchase_order (
    purchase_order_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   UUID NOT NULL REFERENCES organization (organization_id),
    store_id          UUID REFERENCES store (store_id),
    supplier_id       UUID NOT NULL REFERENCES supplier (supplier_id),
    description       TEXT,
    total             NUMERIC(12, 2) NOT NULL,
    status            TEXT NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE purchase_order_product (
    purchase_order_id UUID NOT NULL REFERENCES purchase_order (purchase_order_id),
    store_product_id  UUID NOT NULL REFERENCES store_product (store_product_id),
    quantity          INTEGER NOT NULL,
    unit_cost         NUMERIC(12, 2) NOT NULL,
    PRIMARY KEY (purchase_order_id, store_product_id)
);


CREATE TABLE expense (
    expense_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    store_id        UUID REFERENCES store (store_id),
    supplier_id     UUID REFERENCES supplier (supplier_id),
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
    order_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id          UUID NOT NULL REFERENCES store (store_id),
    store_customer_id UUID REFERENCES store_customer (store_customer_id),
    status            TEXT NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()   -- newest-first sorting (UUID PKs aren't time-ordered)
);

CREATE TABLE sales_order_product (
    order_id         UUID NOT NULL REFERENCES sales_order (order_id),
    store_product_id UUID NOT NULL REFERENCES store_product (store_product_id),
    quantity         INTEGER NOT NULL,
    unit_price       NUMERIC(12, 2) NOT NULL,
    PRIMARY KEY (order_id, store_product_id)
);

CREATE TABLE sales_order_return (
    return_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id  UUID NOT NULL REFERENCES store (store_id),
    order_id  UUID NOT NULL REFERENCES sales_order (order_id)
);

CREATE TABLE sales_order_return_product (
    return_id        UUID NOT NULL REFERENCES sales_order_return (return_id),
    store_product_id UUID NOT NULL REFERENCES store_product (store_product_id),
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

-- Composite indexes for the common sorted lists (list newest-first / alphabetical).
-- Note: UUID primary keys are random, NOT time-ordered, so "newest first" must sort on
-- created_at, never on the id column (the old BIGINT `... _id DESC` trick no longer works).
CREATE INDEX ON store_product   (store_id, sku);                       -- a store's products, by SKU
CREATE INDEX ON sales_order     (store_id, created_at DESC);           -- a store's orders, newest first
CREATE INDEX ON purchase_order  (organization_id, created_at DESC);    -- an org's purchases, newest first
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
    USING (organization_id = current_setting('app.current_org')::uuid);

ALTER TABLE sales_order ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_order FORCE  ROW LEVEL SECURITY;
CREATE POLICY sales_order_tenant ON sales_order
    USING (store_id = current_setting('app.current_store')::uuid);

-- The store-level copies are always store-scoped; the shared records are org-scoped (so every store
-- in the org can resolve them). Both carry organization_id, so both are safe under RLS either way.
ALTER TABLE store_customer ENABLE ROW LEVEL SECURITY;
ALTER TABLE store_customer FORCE  ROW LEVEL SECURITY;
CREATE POLICY store_customer_tenant ON store_customer
    USING (store_id = current_setting('app.current_store')::uuid);

ALTER TABLE customer ENABLE ROW LEVEL SECURITY;    -- the SHARED record
ALTER TABLE customer FORCE  ROW LEVEL SECURITY;
CREATE POLICY customer_tenant ON customer
    USING (organization_id = current_setting('app.current_org')::uuid);

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
