## Tables

PostgreSQL `CREATE TABLE` statements. Only keys and structurally-important fields are
included; descriptive columns (name, description, address, etc.) are added later.

Note: `user` is a reserved word in Postgres, so the table name is quoted as `"user"` in DDL.

## Auth / RBAC

```sql
-- The canonical org/store axis. ONE lookup table that membership and role point at, so the two
-- values ('ORGANIZATION', 'STORE') live in a single place instead of being repeated as independent
-- CHECK constraints that could drift. The whole authz model lines up through these FKs:
-- membership.user_type == role.user_type along one grant chain. The USER has no type — a person is
-- placed by the memberships they hold, not by a label (see auth.md).
CREATE TABLE user_type (
    user_type TEXT PRIMARY KEY CHECK (user_type IN ('ORGANIZATION', 'STORE'))
);

-- `user` is the GLOBAL identity table for the whole system (one login per person), not an
-- org-owned table. It carries NO type: whether someone is an org or store person is decided
-- entirely by which memberships they hold. Provenance fields record where the account originated,
-- so even after every membership is removed we still know the user's home org and who created them.
--
-- is_active is an ACCOUNT-LEVEL kill switch, one level ABOVE membership.is_active:
--   user.is_active       = false -> the whole account is off; ALL their memberships are
--                                   effectively suspended at once (a single switch to disable a
--                                   person everywhere, e.g. offboarding, without touching each
--                                   membership). Access resolution must check this first.
--   membership.is_active = false -> suspends access at ONE place only.
-- So effective access at a place requires BOTH user.is_active AND that membership.is_active.
CREATE TABLE "user" (
    user_id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id    BIGINT NOT NULL REFERENCES organization (organization_id),  -- home org (set once, immutable)
    is_active          BOOLEAN NOT NULL DEFAULT TRUE,   -- account kill switch; false = all memberships suspended
    created_by_user_id BIGINT REFERENCES "user" (user_id),     -- the org admin who created this account
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Roles. For MVP all roles are managed (we ship them; customers assign, don't author).
-- The columns below pre-lay the infrastructure for org-owned CUSTOM roles (post-MVP):
--   is_managed      = TRUE for the roles we ship; FALSE for a customer's custom role.
--   organization_id = the owning org for a custom role; NULL for managed (we own those).
--   user_type       = the level the role applies at (ORGANIZATION or STORE), via the shared
--                     user_type lookup. A role is only assignable to a membership of the same type
--                     (membership.user_type == role.user_type) — see auth.md.
-- Custom roles are org-owned only — there are no store-owned custom roles (no store_id).
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

-- A permission is resource:action (e.g. product:read, order:refund, role:assign), explicit
-- and never a wildcard. The REACH (one store vs all the org's stores) comes from the ROLE
-- that holds it (role.user_type) and the membership it's granted at, NOT from the permission.
--   resource    = what's acted on: product, order, role, store, user, ...
--   action      = the verb: read, create, refund, assign, ...
--   is_elevated = an eligibility guard on which role types may HOLD this permission (NOT the
--                 level it operates at):
--                   FALSE -> can go in store roles AND org roles (the default).
--                   TRUE  -> can go in ORGANIZATION roles ONLY (e.g. store:create, user:create).
--                 A one-way gate: elevated permissions are org-only; non-elevated apply to both.
--                 This stops a (future custom) store-typed role from holding an org-only power.
CREATE TABLE permission (
    permission_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    resource      TEXT NOT NULL,   -- e.g. product, order, role
    action        TEXT NOT NULL,   -- e.g. read, create, refund, assign
    is_elevated   BOOLEAN NOT NULL DEFAULT FALSE,   -- TRUE = org roles only
    UNIQUE (resource, action)
);

-- An elevated permission (is_elevated = TRUE) may only attach to an ORGANIZATION role.
-- Enforced on the write path (cross-table: permission.is_elevated vs role.user_type) when a
-- permission is added to a role. Non-elevated permissions attach to either type.
CREATE TABLE role_permission (
    role_id       BIGINT NOT NULL REFERENCES role (role_id),
    permission_id BIGINT NOT NULL REFERENCES permission (permission_id),
    PRIMARY KEY (role_id, permission_id)
);

-- ===========================================================================================
-- ABAC conditions (STRETCH GOAL — maybe MVP). Let customers put limits on a role's permission,
-- e.g. "Cashier may refund, but only up to $500." Two tables: a fixed MENU we ship
-- (permission_condition) and the customer's chosen VALUES (role_permission_condition).
-- ===========================================================================================

-- The MENU: which conditions a permission is allowed to have. WE define these (fixed reference
-- data). Each row is one tunable check: a `field` + an `operator`, plus a display label.
--   field    = the name to check on the action's DTO/entity (the SAME name; that's the mapping —
--              field 'amount' reads dto.amount at evaluation time).
--   operator = how to compare (<=, >=, ==, ...).
-- WHO edits values: ONLY org admins (permission `role_condition:edit`, held only by org roles).
-- They set a store's limits, scoped via store_id. Store admins do NOT edit limits -> no
-- self-escalation is possible. If a store wants a higher cap, the store admin asks the org admin.
-- No CHECK/min/max constraints: org admins are trusted to set any value, and WE author these
-- menu rows, so the inputs are already controlled. See auth.md.
CREATE TABLE permission_condition (
    permission_condition_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    permission_id BIGINT NOT NULL REFERENCES permission (permission_id),
    field         TEXT NOT NULL,   -- DTO/entity field to check, e.g. 'amount', 'age_days'
    operator      TEXT NOT NULL,   -- how to compare, e.g. '<='
    label         TEXT NOT NULL,   -- UI display, e.g. 'Refund cap'
    UNIQUE (permission_id, field, operator)
);

-- The customer's chosen VALUES. CUSTOMER data — empty by default (we ship none). Points at an
-- allowed menu entry (permission_condition_id), so a customer can only ever set a value for a
-- condition we permit — they can't invent a field/operator (the FK enforces the allowlist).
-- TENANT-SCOPED (required — roles are global/managed; an unscoped row would change a shared role
-- for every org):
--   organization_id NOT NULL  -- the owning org
--   store_id        NULL      -- NULL = all the org's stores; set = this store only (override)
-- Resolution is MOST-SPECIFIC-WINS for a user acting at store S in org O:
--   1. row (role, condition, org=O, store=S)        -- store-specific override
--   2. else row (role, condition, org=O, store=NULL) -- org-wide setting
--   3. else unconditional
CREATE TABLE role_permission_condition (
    role_permission_condition_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id         BIGINT NOT NULL REFERENCES organization (organization_id),
    store_id                BIGINT REFERENCES store (store_id),   -- NULL = whole org; set = one store
    role_id                 BIGINT NOT NULL REFERENCES role (role_id),
    permission_condition_id BIGINT NOT NULL REFERENCES permission_condition (permission_condition_id),
    value                   NUMERIC NOT NULL,   -- the customer's setting, e.g. 500 (within min/max)
    -- one value per (tenant-place, role, condition)
    UNIQUE (organization_id, store_id, role_id, permission_condition_id)
    -- Note: this doesn't enforce that the role actually holds the menu entry's permission
    -- (a cap on a permission the role lacks is harmless — RBAC denies first). The UI should
    -- only offer conditions for permissions the role has; value must be within the menu's min/max.
);

-- A user belongs to a place (a store OR the org), independent of any role.
-- Like an IAM user: the membership exists on its own; roles are layered on top.
-- Removing all of a user's roles at a place leaves this row intact, so the user
-- still belongs there with no access.
--
-- A user has NO type of their own — they're placed by the memberships they hold. An ORGANIZATION
-- membership reaches all the org's stores; a STORE membership reaches its one store. A user may hold
-- store memberships, an org membership, or both, UNLESS organization.allow_user_cross_memberships is
-- FALSE (the default), in which case the write path locks each user to a single kind (an org member
-- can't also be given a store membership, and vice versa). Multiple store memberships are always
-- allowed. This crossing rule is a cross-table write-path check, not a DB CHECK. See auth.md.
CREATE TABLE membership (
    membership_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id         BIGINT NOT NULL REFERENCES "user" (user_id),
    user_type       TEXT NOT NULL REFERENCES user_type (user_type),   -- ORGANIZATION | STORE: the kind of place
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    store_id        BIGINT REFERENCES store (store_id),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    deleted_at      TIMESTAMPTZ,   -- soft delete: NULL = live, set = removed (kept for history)
    -- Two distinct switches:
    --   is_active  = suspended but still belongs here (temporary; reversible toggle).
    --   deleted_at = removed from this place, but the row is retained for audit/history.
    -- A live membership has deleted_at IS NULL. Every access query must filter
    -- deleted_at IS NULL, or a removed membership would still grant access.
    --
    -- user_type FKs the shared `user_type` lookup, same as role.user_type, so the grant chain stays
    -- consistent: membership.user_type == role.user_type (the user itself has no type).
    -- organization_id is ALWAYS populated — it's the tenant boundary, carried on EVERY membership
    -- (org and store alike). store_id distinguishes the kind:
    --   ORGANIZATION membership: store_id IS NULL    (the place is the organization itself)
    --   STORE        membership: store_id IS NOT NULL (the place is one store, inside organization_id)
    -- Queries branch on user_type, not on which id is null.
    CHECK ((user_type = 'ORGANIZATION' AND store_id IS NULL)
        OR (user_type = 'STORE'        AND store_id IS NOT NULL))
);

-- Membership cardinality (the org-vs-store asymmetry):
--   STORE user  -> MANY store memberships (one per store they belong to).
--   ORG   user  -> EXACTLY ONE org membership, ever (an org member belongs to one org, and
--                  that org is their immutable home org; org reach already covers all stores,
--                  so there's never a second org membership).
-- Soft-deleted rows (deleted_at set) are excluded from both, so removing then re-adding a user
-- at the same place doesn't collide with the old tombstone.

-- at most one LIVE membership per user per store
CREATE UNIQUE INDEX membership_store_uq
    ON membership (user_id, store_id)
    WHERE store_id IS NOT NULL AND deleted_at IS NULL;

-- at most one LIVE org membership per user, PERIOD (keyed on user_id alone, not user+org) — this
-- enforces the "an org user belongs to exactly one organization" rule without splitting the table.
-- Keyed on user_type = 'ORGANIZATION' (NOT "organization_id IS NOT NULL"), because organization_id is
-- set on every membership; only org-type memberships count toward this limit.
CREATE UNIQUE INDEX membership_org_uq
    ON membership (user_id)
    WHERE user_type = 'ORGANIZATION' AND deleted_at IS NULL;

-- A role assigned to a membership. Zero or more per membership.
-- Losing a role = deleting its row here; the membership above is untouched.
-- expires_at: optional time limit on the assignment (temp/seasonal staff, contractors).
--   NULL = never expires. Once expires_at has passed the assignment grants nothing — access
--   resolution filters expires_at IS NULL OR expires_at > now() (same family as the
--   membership.deleted_at / is_active filters).
-- A role is only assignable to a membership of the SAME type as the role
-- (membership.user_type == role.user_type) — enforced on the write path (cross-table rule). So a
-- STORE membership only ever receives STORE roles, an ORGANIZATION membership only ORGANIZATION roles.
CREATE TABLE membership_assignment (
    membership_id BIGINT NOT NULL REFERENCES membership (membership_id),
    role_id       BIGINT NOT NULL REFERENCES role (role_id),
    expires_at    TIMESTAMPTZ,   -- NULL = never expires
    PRIMARY KEY (membership_id, role_id)   -- same role can't be granted twice here
);

-- Level-specific membership fields live in side tables, so the shared `membership`
-- table stays free of nulls. A membership has a detail row in exactly ONE of these,
-- matching its place: store memberships get a store_membership_detail row, org
-- memberships get an organization_membership_detail row. One row per membership, so
-- membership_id is both the PK and the FK. Screens join the one that matches the place
-- they're already querying — the two detail tables never overlap, so no UNION.
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
    -- allow_user_cross_memberships: may one user hold an org membership AND store memberships at once?
    --   FALSE (default) -> each user is locked to one kind; the write path refuses to add a store
    --                      membership to a user who has an org membership, and vice versa. Multiple
    --                      STORE memberships are always fine — the gate is only org-vs-store crossing.
    --   TRUE            -> a user may span both levels. See auth.md ("Can a user be both...").
    allow_user_cross_memberships BOOLEAN NOT NULL DEFAULT FALSE,
    -- Org-wide sharing settings (default off = each store's products/customers are its own):
    --   share_products  TRUE -> a store creating a product also writes a shared `product` row, so the
    --                           catalog identity (SKU) is recognized org-wide (each store still keeps
    --                           its own stock/price in store_product).
    --   share_customers TRUE -> a store creating a customer also writes a shared `customer` row, so
    --                           the customer is recognized at every store.
    -- When on, the store create dual-writes both tables in one transaction. See auth.md / tenancy.md.
    share_products  BOOLEAN NOT NULL DEFAULT FALSE,
    share_customers BOOLEAN NOT NULL DEFAULT FALSE
    -- The plan is bought at the org and applies to all its stores. Billing is per store:
    -- total = plan.price_per_store × number of stores in the org (derived, not stored). See plans.md.
    -- Every org gets a default ("main") store created on onboarding — the store it sells and
    -- purchases through by default. default_store_id points at it; the owner can promote a
    -- different store later. Nullable only because the org row may be inserted just before its
    -- first store in the same onboarding transaction.
);
```

## Store

```sql
CREATE TABLE store (
    store_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id  BIGINT NOT NULL REFERENCES organization (organization_id),
    region           TEXT,                               -- grouping label, e.g. 'NorthWest'
    sub_region       TEXT                                -- finer grouping, e.g. 'Seattle-Metro'
    -- region/sub_region are descriptive tags for filtering and reports, NOT places you can
    -- grant roles at. If a region ever needs to OWN access (a real district manager role)
    -- it graduates to its own entity; until then it's just a label on the store.
);

-- Free-form labels on a store (e.g. 'flagship', 'airport', 'pilot-program'). Many tags
-- per store. Kept separate from region/sub_region so a store can carry any number of them.
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
-- Store-level product: this store's own copy — its sku, stock, and price live here, always.
-- product_id links up to the shared record when sharing is on (NULL = store-only).
CREATE TABLE store_product (
    store_product_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id         BIGINT NOT NULL REFERENCES store (store_id),
    organization_id  BIGINT NOT NULL REFERENCES organization (organization_id),  -- tenant boundary / RLS
    product_id       BIGINT REFERENCES product (product_id),   -- NULL = store-only; set = shared
    sku              TEXT,
    quantity         INTEGER NOT NULL DEFAULT 0,   -- this store's stock
    price            NUMERIC(12, 2),               -- this store's price
    UNIQUE (store_id, sku)   -- sku unique within the store
);

-- Shared product: the org-wide identity (SKU). Written only when share_products is on. Holds no
-- stock/price — each store still prices and stocks independently in its store_product row.
CREATE TABLE product (
    product_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    sku             TEXT,
    UNIQUE (organization_id, sku)
);

-- Store-level customer: this store's own copy. customer_id links up to the shared record when
-- sharing is on (NULL = store-only). organization_id is the tenant boundary / RLS.
CREATE TABLE store_customer (
    store_customer_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id          BIGINT NOT NULL REFERENCES store (store_id),
    organization_id   BIGINT NOT NULL REFERENCES organization (organization_id),
    customer_id       BIGINT REFERENCES customer (customer_id)   -- NULL = store-only; set = shared
);

-- Shared customer: the org-wide customer account. Written only when share_customers is on.
CREATE TABLE customer (
    customer_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

-- Supplier is an org-level vendor record. Only org users deal with suppliers and create purchases.
CREATE TABLE supplier (
    supplier_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

-- The organization buys inventory from a supplier. Only org users create purchases. store_id is
-- optional: set it to attribute the purchase to a store (so a store admin can query their own),
-- or leave it NULL for an org-wide purchase. description records why the purchase was made.
CREATE TABLE purchase_order (
    purchase_order_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id   BIGINT NOT NULL REFERENCES organization (organization_id),
    store_id          BIGINT REFERENCES store (store_id),         -- optional: the store this is for
    supplier_id       BIGINT NOT NULL REFERENCES supplier (supplier_id),
    description       TEXT,                      -- why this purchase was made
    total             NUMERIC(12, 2) NOT NULL,   -- order total
    status            TEXT NOT NULL,             -- e.g. ordered / received / cancelled
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE purchase_order_product (
    purchase_order_id BIGINT NOT NULL REFERENCES purchase_order (purchase_order_id),
    store_product_id  BIGINT NOT NULL REFERENCES store_product (store_product_id),
    quantity          INTEGER NOT NULL,
    unit_cost         NUMERIC(12, 2) NOT NULL,   -- cost per unit at purchase time
    PRIMARY KEY (purchase_order_id, store_product_id)
);



-- Non-inventory spending (furniture, computers, utilities, SaaS, accountant fees, etc.) — money
-- OUT that is not resold and does not touch stock. Only org users record expenses. store_id is
-- optional: set it to attribute the expense to a store (so a store admin can query their own),
-- or leave it NULL for an org-wide expense. description records why.
CREATE TABLE expense (
    expense_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    store_id        BIGINT REFERENCES store (store_id),        -- optional: the store this is for
    supplier_id     BIGINT REFERENCES supplier (supplier_id),  -- the vendor (Amazon, Staples, ...); optional
    category        TEXT NOT NULL,             -- e.g. 'furniture', 'equipment', 'utilities', 'software'
    description     TEXT,                      -- why this expense was made
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
    store_customer_id BIGINT REFERENCES store_customer (store_customer_id),   -- the store's own customer row
    status            TEXT NOT NULL   -- e.g. open / paid / refunded
);

CREATE TABLE sales_order_product (
    order_id         BIGINT NOT NULL REFERENCES sales_order (order_id),
    store_product_id BIGINT NOT NULL REFERENCES store_product (store_product_id),  -- the store's product
    quantity         INTEGER NOT NULL,
    unit_price       NUMERIC(12, 2) NOT NULL,   -- snapshots the price at sale time
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
