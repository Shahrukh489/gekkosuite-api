## Tables

PostgreSQL `CREATE TABLE` statements. Only keys and structurally-important fields are
included; descriptive columns (name, description, address, etc.) are added later.

Note: `user` is a reserved word in Postgres, so the table name is quoted as `"user"` in DDL.

## Auth / RBAC

```sql
-- The canonical org/store axis. ONE lookup table that user, role, and permission all point at,
-- so the two values ('ORGANIZATION', 'STORE') live in a single place instead of being repeated
-- as independent CHECK constraints that could drift. The whole authz model lines up through
-- these FKs: a user's type == the role's type == the permission's type along one grant chain.
CREATE TABLE user_type (
    user_type TEXT PRIMARY KEY CHECK (user_type IN ('ORGANIZATION', 'STORE'))
);

-- `user` is the GLOBAL identity table for the whole system (one login per person), not an
-- org-owned table. Provenance fields record where the account originated, so even after
-- every membership is removed we still know the user's home org and who created them.
--
-- user_type is the STRUCTURAL org-vs-store distinction, set at creation:
--   ORGANIZATION -> owns/runs the company; one org membership, reaches all the org's stores.
--   STORE        -> an employee; one or more store memberships, can never reach the org.
-- It gates which memberships and roles a user may hold (user_type must equal role.user_type).
-- IMMUTABLE in MVP: there is no path to change it. Promote/demote (changing user_type) is a
-- deliberate post-MVP feature (see post-mvp.md).
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
    user_type          TEXT NOT NULL REFERENCES user_type (user_type),  -- ORGANIZATION | STORE, set at creation, immutable
    is_active          BOOLEAN NOT NULL DEFAULT TRUE,   -- account kill switch; false = all memberships suspended
    created_by_user_id BIGINT REFERENCES "user" (user_id),     -- the org admin who created this account
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Roles. For MVP all roles are managed (we ship them; customers assign, don't author).
-- The columns below pre-lay the infrastructure for org-owned CUSTOM roles (post-MVP):
--   is_managed      = TRUE for the roles we ship; FALSE for a customer's custom role.
--   organization_id = the owning org for a custom role; NULL for managed (we own those).
--   user_type       = the level the role applies at (ORGANIZATION or STORE), via the shared
--                     user_type lookup. A role is only assignable to a user of the same type
--                     (user.user_type == role.user_type) — see auth.md.
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
-- The place must MATCH the user's type (enforced on the write path, since it's a cross-table
-- rule the DB CHECK can't span): an ORGANIZATION user only gets organization_id memberships;
-- a STORE user only gets store_id memberships. So an org user has one org membership (reaching
-- all the org's stores), a store user has one or more store memberships.
CREATE TABLE membership (
    membership_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id         BIGINT NOT NULL REFERENCES "user" (user_id),
    organization_id BIGINT REFERENCES organization (organization_id),
    store_id        BIGINT REFERENCES store (store_id),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    deleted_at      TIMESTAMPTZ,   -- soft delete: NULL = live, set = removed (kept for history)
    -- Two distinct switches:
    --   is_active  = suspended but still belongs here (temporary; reversible toggle).
    --   deleted_at = removed from this place, but the row is retained for audit/history.
    -- A live membership has deleted_at IS NULL. Every access query must filter
    -- deleted_at IS NULL, or a removed membership would still grant access.
    -- the place is organization_id OR store_id: EXACTLY ONE set, never both, never neither.
    -- The `<>` (XOR) means exactly one of the two is NOT NULL.
    CHECK ((organization_id IS NOT NULL) <> (store_id IS NOT NULL))
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
CREATE UNIQUE INDEX membership_org_uq
    ON membership (user_id)
    WHERE organization_id IS NOT NULL AND deleted_at IS NULL;

-- A role assigned to a membership. Zero or more per membership.
-- Losing a role = deleting its row here; the membership above is untouched.
-- expires_at: optional time limit on the assignment (temp/seasonal staff, contractors).
--   NULL = never expires. Once expires_at has passed the assignment grants nothing — access
--   resolution filters expires_at IS NULL OR expires_at > now() (same family as the
--   membership.deleted_at / is_active filters).
-- A role is only assignable to a membership whose user has the SAME type as the role
-- (user.user_type == role.user_type) — enforced on the write path (cross-table rule). So a
-- STORE user can only ever receive STORE roles, an ORGANIZATION user only ORGANIZATION roles.
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
    default_store_id BIGINT REFERENCES store (store_id)
    -- The plan is bought at the org and applies to all its stores. Billing is per store:
    -- total = plan.price_per_store × number of stores in the org (derived, not stored). See plans.md.
    -- Every org gets a default ("main") store created on onboarding — the store it sells and
    -- purchases through by default. default_store_id points at it; the owner can promote a
    -- different store later. Nullable only because the org row may be inserted just before its
    -- first store in the same onboarding transaction.
    -- customers, suppliers, and the product catalog are all org-level and visible to every
    -- store (tightly-coupled single company), so there's no per-store sharing flag.
);
```

## Store

```sql
CREATE TABLE store (
    store_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id  BIGINT NOT NULL REFERENCES organization (organization_id),
    region           TEXT,                               -- grouping label, e.g. 'NorthWest'
    sub_region       TEXT,                               -- finer grouping, e.g. 'Seattle-Metro'
    purchase_balance NUMERIC(12, 2),                     -- delegated inventory-buying allowance; NULL = unlimited
    expense_balance  NUMERIC(12, 2)                      -- delegated expense-spending allowance; NULL = unlimited
    -- The org is the single source of funds; a store has no account of its own. These two are
    -- how much spending power the org admin delegates to this store, each DEPLETING independently:
    --   purchase_balance -> inventory buys (purchase_order); each PO decrements it.
    --   expense_balance  -> non-inventory spend (expense, e.g. furniture); each expense decrements it.
    -- A purchase/expense is rejected if its total exceeds the matching balance (when not NULL).
    -- On success, the record is inserted AND the matching balance decremented in one transaction.
    -- The owner "tops up" by raising the number. NULL = no limit.
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

## Org-level entities (catalog, customers, suppliers)

These belong to the organization and are visible to every store — not per-store. Each store
sets its own stock/price for catalog products via `product_store`.

```sql
-- Product is an ORG-level identity (one catalog for the whole org), deduped on SKU — one
-- 'Coke SKU-123' shared across all stores. Each store sets its own quantity and price via
-- product_store. This fits a tightly-coupled single company: one catalog, per-store stock.
CREATE TABLE product (
    product_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),
    sku             TEXT,
    UNIQUE (organization_id, sku)   -- sku is unique within the org (the dedup key)
);

-- A store's stock and price for a product. One row per (product, store) the store carries.
CREATE TABLE product_store (
    product_id BIGINT NOT NULL REFERENCES product (product_id),
    store_id   BIGINT NOT NULL REFERENCES store (store_id),
    quantity   INTEGER NOT NULL DEFAULT 0,   -- this store's own stock (independent per store)
    price      NUMERIC(12, 2),               -- this store's own price
    PRIMARY KEY (product_id, store_id)
);

-- Customer is an ORG-level identity: one account per person for the whole org, visible to
-- every store. Walk into any store, use the same account. No per-store customer data.
CREATE TABLE customer (
    customer_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

-- @TODO: decide on this if needed because it will limit showing only customers in a store in UI 
CREATE TABLE customer_store (
    customer_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

-- Supplier is an ORG-level vendor record, visible to every store. No per-store terms.
CREATE TABLE supplier (
    supplier_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

-- A store buying inventory from an org supplier (inventory-IN, the counterpart to sales_order).
-- Spends the org's funds against the store's delegated purchase_balance: on creation we insert
-- this + its lines AND decrement store.purchase_balance in one transaction, rejecting the
-- purchase if its total exceeds the balance (when the balance isn't NULL = unlimited).
CREATE TABLE purchase_order (
    purchase_order_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id          BIGINT NOT NULL REFERENCES store (store_id),       -- the store that ordered
    supplier_id       BIGINT NOT NULL REFERENCES supplier (supplier_id), -- the org supplier bought from
    total             NUMERIC(12, 2) NOT NULL,   -- order total (what's deducted from purchase_balance)
    status            TEXT NOT NULL,             -- e.g. ordered / received / cancelled
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE purchase_order_product (
    purchase_order_id BIGINT NOT NULL REFERENCES purchase_order (purchase_order_id),
    product_id        BIGINT NOT NULL REFERENCES product (product_id),
    quantity          INTEGER NOT NULL,
    unit_cost         NUMERIC(12, 2) NOT NULL,   -- cost per unit at purchase time
    PRIMARY KEY (purchase_order_id, product_id)
);



-- Non-inventory spending (furniture, computers, utilities, SaaS, accountant fees, etc.) —
-- money OUT that is NOT resold and does NOT touch stock, so it's separate from purchase_order.
-- No product lines: just a category and amount. An expense belongs to EITHER a store OR the
-- org directly:
--   store_id set   -> a store expense (location accounting; depletes that store's expense_balance)
--   store_id NULL  -> an ORG-level expense (HQ overhead: the POS subscription, accountant,
--                     company-wide software) — no store, so no per-store balance is depleted.
-- organization_id is always set so org-level expenses (store_id NULL) still have an owner.
CREATE TABLE expense (
    expense_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id),  -- always the owning org
    store_id        BIGINT REFERENCES store (store_id),       -- set = store expense; NULL = org-level expense
    supplier_id     BIGINT REFERENCES supplier (supplier_id), -- the vendor (Amazon, Staples, ...); optional
    category        TEXT NOT NULL,             -- e.g. 'furniture', 'equipment', 'utilities', 'software'
    amount          NUMERIC(12, 2) NOT NULL,   -- store expense: deducted from the store's expense_balance
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
    -- when store_id is set, its store must belong to organization_id (enforced on the write path)
);

```

## Store transactions (sales, returns, purchases, expenses)

These happen at a store (the business unit). They reference the org-level catalog/customers/
suppliers above, but the transaction itself belongs to a store.

```sql
CREATE TABLE sales_order (
    order_id    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id    BIGINT NOT NULL REFERENCES store (store_id),
    customer_id BIGINT REFERENCES customer (customer_id),
    status      TEXT NOT NULL   -- e.g. open / paid / refunded
);

CREATE TABLE sales_order_product (
    order_id   BIGINT NOT NULL REFERENCES sales_order (order_id),
    product_id BIGINT NOT NULL REFERENCES product (product_id),
    quantity   INTEGER NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL,   -- snapshots the price at sale time
    PRIMARY KEY (order_id, product_id)
);

CREATE TABLE sales_order_return (
    return_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id  BIGINT NOT NULL REFERENCES store (store_id),
    order_id  BIGINT NOT NULL REFERENCES sales_order (order_id)
);

CREATE TABLE sales_order_return_product (
    return_id  BIGINT NOT NULL REFERENCES sales_order_return (return_id),
    product_id BIGINT NOT NULL REFERENCES product (product_id),
    quantity   INTEGER NOT NULL,
    PRIMARY KEY (return_id, product_id)
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

-- Org-level identities (customers, suppliers, product catalog)
CREATE INDEX ON customer (organization_id);
CREATE INDEX ON supplier (organization_id);
CREATE INDEX ON product  (organization_id);

-- Store data (the big tables — these matter most)
CREATE INDEX ON product_store          (store_id);     -- a store's catalog (product_id covered by PK)
CREATE INDEX ON sales_order            (store_id);
CREATE INDEX ON sales_order            (customer_id);
CREATE INDEX ON sales_order_product    (product_id);   -- order_id covered by PK
CREATE INDEX ON sales_order_return         (store_id);
CREATE INDEX ON sales_order_return         (order_id);
CREATE INDEX ON sales_order_return_product (product_id);   -- return_id covered by PK
CREATE INDEX ON purchase_order             (store_id);
CREATE INDEX ON purchase_order             (supplier_id);
CREATE INDEX ON purchase_order_product     (product_id);   -- purchase_order_id covered by PK
CREATE INDEX ON expense                    (organization_id);   -- org-level expenses (store_id NULL)
CREATE INDEX ON expense                    (store_id);
CREATE INDEX ON expense                    (supplier_id);

-- Composite indexes for the common sorted lists (list newest-first / alphabetical)
CREATE INDEX ON product        (organization_id, sku);    -- the org catalog, by SKU
CREATE INDEX ON sales_order     (store_id, order_id DESC);
CREATE INDEX ON purchase_order  (store_id, purchase_order_id DESC);   -- a store's purchases, newest first
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

ALTER TABLE customer ENABLE ROW LEVEL SECURITY;
ALTER TABLE customer FORCE  ROW LEVEL SECURITY;
CREATE POLICY customer_tenant ON customer
    USING (organization_id = current_setting('app.current_org')::bigint);

ALTER TABLE sales_order ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales_order FORCE  ROW LEVEL SECURITY;
CREATE POLICY sales_order_tenant ON sales_order
    USING (store_id = current_setting('app.current_store')::bigint);

-- Apply the same pattern to every tenant-scoped table:
--   org-scoped   (filter on organization_id): customer, supplier, product, role(custom), expense, ...
--   store-scoped (filter on store_id):         product_store, sales_order, sales_order_return,
--                                              purchase_order, store expenses, ...
-- Child/line tables (sales_order_product, ...) inherit isolation through their parent's FK, but add
-- a policy too if they can ever be queried directly.
```

Notes:
- **Defense in depth, not a replacement** — keep the app-level scoping; RLS catches the query that slips.
- **org vs store context** — an org user acts with `app.current_org` set (and reaches store rows because
  those stores are in their org — store policies can also allow `store.organization_id = app.current_org`);
  a store action sets `app.current_store`. Pick one consistent policy shape per table and document it.
- **Migrations bypass RLS** by design (run as the owner/privileged role), so schema changes aren't blocked.
