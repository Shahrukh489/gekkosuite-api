```sql
-- How a store sells: a storefront kind. 
CREATE TYPE store_type AS ENUM ('ONLINE', 'PHYSICAL');

-- The scope a membership/role operates at: the two kinds of place a person can act at.
CREATE TYPE scope AS ENUM ('ORGANIZATION', 'STORE');

-- A plan: what an org subscribes to. Priced per store; every store inherits the plan's features.
CREATE TABLE plan (
    -- plan id
    plan_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- display name, e.g. 'Basic', 'Pro' (unique across plans)
    name            TEXT NOT NULL UNIQUE,
    -- optional blurb about the plan
    description     TEXT
);

-- A feature: one capability a plan can include (reports, returns, multi-store, ...).
CREATE TABLE feature (
    -- feature id
    feature_id  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- fixed key the app looks up in code, e.g. 'store_reporting, ai_chatbot' — never rename it or feature checks break
    code        TEXT NOT NULL UNIQUE,
    -- what users see, e.g. 'Multi-store' — safe to rename anytime
    label       TEXT NOT NULL,
    -- optional longer description
    description TEXT
);

-- plan_feature: which features a plan includes (many-to-many). Adding a row gives every org on that
-- plan the feature instantly (see plans.md).
CREATE TABLE plan_feature (
    -- the plan
    plan_id    UUID NOT NULL REFERENCES plan (plan_id),
    -- the feature it includes
    feature_id UUID NOT NULL REFERENCES feature (feature_id),
    -- a plan can't list the same feature twice
    PRIMARY KEY (plan_id, feature_id)
);

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
    -- org-wide product sharing (off = each store's catalog is its own)
    allow_share_products  BOOLEAN NOT NULL DEFAULT FALSE,
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
    deleted_at       TIMESTAMPTZ
);
CREATE INDEX ON store (organization_id);   -- an org's stores


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

CREATE INDEX ON "user" (organization_id);   -- users in their home org


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
    scope           scope NOT NULL,
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


-- A membership: a place a user belongs — the whole ORGANIZATION, or one STORE. Sets their reach there.
CREATE TABLE membership (
    -- membership id
    membership_id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- the user this membership belongs to
    user_id         UUID NOT NULL REFERENCES "user" (user_id),
    -- ORGANIZATION | STORE: the kind of place (see CHECK for the store_id rule)
    scope           scope NOT NULL,
    -- the tenant this membership is in (the boundary every request is checked against)
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    -- the store, for a STORE membership; NULL for an ORGANIZATION membership
    store_id        UUID REFERENCES store (store_id),
    -- suspend switch for this one place; false = access off here but kept
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    -- when the membership was granted (stored UTC)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- soft-delete flag; TRUE = removed from this place but kept for history
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at      TIMESTAMPTZ,
    -- org membership has no store; store membership must have one
    CHECK ((scope = 'ORGANIZATION' AND store_id IS NULL)
        OR (scope = 'STORE'        AND store_id IS NOT NULL))
);

-- a user can hold only one live membership at a given store
CREATE UNIQUE INDEX membership_store_uq
    ON membership (user_id, store_id)
    WHERE store_id IS NOT NULL AND NOT is_deleted;

-- a user can hold only one live organization membership
CREATE UNIQUE INDEX membership_org_uq
    ON membership (user_id)
    WHERE scope = 'ORGANIZATION' AND NOT is_deleted;

-- all of a user's memberships (the authz query walks these every request)
CREATE INDEX ON membership (user_id);

-- membership_assignment: a role granted to a membership (many-to-many). Role scope must match the
-- membership scope, enforced on write (see auth.md).
CREATE TABLE membership_assignment (
    -- the membership the role is granted to
    membership_id UUID NOT NULL REFERENCES membership (membership_id),
    -- the role granted
    role_id       UUID NOT NULL REFERENCES role (role_id),
    -- when the role was granted (stored UTC)
    assigned_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- who granted it (the admin) — for audit; NULL if system-seeded
    assigned_by_user_id UUID REFERENCES "user" (user_id),
    -- optional expiry; NULL = never expires
    expires_at    TIMESTAMPTZ,
    -- same role can't be granted to the same membership twice
    PRIMARY KEY (membership_id, role_id)
);

-- Fields that only apply to a STORE membership (1:1 with membership). Keeps store-only columns off
-- the shared table; org memberships simply have no row here.
CREATE TABLE store_membership_detail (
    -- the store membership this detail belongs to
    membership_id  UUID PRIMARY KEY REFERENCES membership (membership_id),
    -- register PIN, stored as a salted KDF hash (never plaintext) — see auth.md
    store_pin TEXT
    -- ...other store-only member fields go here
);

-- Fields that only apply to an ORGANIZATION membership (1:1 with membership). Empty for now.
CREATE TABLE organization_membership_detail (
    -- the org membership this detail belongs to
    membership_id UUID PRIMARY KEY REFERENCES membership (membership_id)
    -- ...org-only member fields go here
);

-- Lifecycle of a sale. OPEN = in progress; COMPLETED = paid/finalized; VOIDED = cancelled.
CREATE TYPE order_status AS ENUM ('OPEN', 'COMPLETED', 'VOIDED');

-- Lifecycle of a return. PENDING = raised; COMPLETED = refunded; VOIDED = cancelled.
CREATE TYPE return_status AS ENUM ('PENDING', 'COMPLETED', 'VOIDED');

-- A sale rung up at a store.
CREATE TABLE sales_order (
    -- order id
    order_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- the store the sale belongs to 
    store_id          UUID NOT NULL REFERENCES store (store_id),
    -- the customer, if attached; NULL for a walk-in / anonymous sale
    store_customer_id UUID REFERENCES store_customer (store_customer_id),
    -- the user (cashier) who rang the sale
    sold_by_user_id   UUID REFERENCES "user" (user_id),
    -- OPEN | COMPLETED | VOIDED
    status            order_status NOT NULL DEFAULT 'OPEN',
    -- money breakdown (all snapshotted; total = subtotal - discount_total + tax_total)
    subtotal          NUMERIC(12, 2) NOT NULL DEFAULT 0,   -- sum of line (unit_price × qty)
    discount_total    NUMERIC(12, 2) NOT NULL DEFAULT 0,   -- order-level + line discounts applied
    tax_total         NUMERIC(12, 2) NOT NULL DEFAULT 0,   -- tax charged
    total             NUMERIC(12, 2) NOT NULL DEFAULT 0,   -- what the customer pays
    -- when the sale was created (stored UTC; newest-first sorting — UUID PKs aren't time-ordered)
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ON sales_order (store_id);   -- a store's orders

-- A line on a sale: one product, its quantity, and the price/tax/discount at time of sale.
CREATE TABLE sales_order_product (
    -- the order this line belongs to
    order_id         UUID NOT NULL REFERENCES sales_order (order_id),
    -- the store product sold
    store_product_id UUID NOT NULL REFERENCES store_product (store_product_id),
    -- how many units
    quantity         INTEGER NOT NULL,
    -- price per unit, snapshotted at sale time (not read live from store_product)
    unit_price       NUMERIC(12, 2) NOT NULL,
    -- discount applied to this line
    discount         NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- tax charged on this line
    tax              NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- one row per product per order
    PRIMARY KEY (order_id, store_product_id)
);

-- A return against a prior sale (refunds some or all of its lines).
CREATE TABLE sales_order_return (
    -- return id
    return_id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- the store the return belongs to 
    store_id            UUID NOT NULL REFERENCES store (store_id),
    -- the sale being returned against
    order_id            UUID NOT NULL REFERENCES sales_order (order_id),
    -- the user who processed the return
    processed_by_user_id UUID REFERENCES "user" (user_id),
    -- PENDING | COMPLETED | VOIDED
    status              return_status NOT NULL DEFAULT 'PENDING',
    -- free-text reason for the return
    reason              TEXT,
    -- total amount refunded to the customer
    refund_total        NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- when the return was created (stored UTC)
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ON sales_order_return (store_id);   -- a store's returns

-- A line on a return: which product and how many units came back.
CREATE TABLE sales_order_return_product (
    -- the return this line belongs to
    return_id        UUID NOT NULL REFERENCES sales_order_return (return_id),
    -- the store product being returned (must be one that was on the original order)
    store_product_id UUID NOT NULL REFERENCES store_product (store_product_id),
    -- how many units returned (app/trigger enforces: total returned ≤ quantity originally sold)
    quantity         INTEGER NOT NULL,
    -- the price the unit was sold for (snapshot of the order line's unit_price)
    sold_price   NUMERIC(12, 2) NOT NULL,
    -- amount refunded for this line
    refund_amount    NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- one row per product per return
    PRIMARY KEY (return_id, store_product_id)
);




-- Lifecycle of a purchase order. DRAFT = being prepared; ORDERED = placed with supplier;
-- RECEIVED = goods arrived; CANCELLED = voided.
CREATE TYPE purchase_order_status AS ENUM ('DRAFT', 'ORDERED', 'RECEIVED', 'CANCELLED');

-- A supplier (vendor). Org-level — the org holds the vendor relationships and does all buying.
CREATE TABLE supplier (
    -- supplier id
    supplier_id     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- owning org (the tenant)
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    -- vendor display name (required)
    name            TEXT NOT NULL,
    -- optional notes about the supplier
    description     TEXT,
    -- vendor contact email
    email           TEXT,
    -- vendor contact phone (TEXT: '+', spaces, extensions)
    phone           TEXT,
    -- when the supplier was added (stored UTC)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at      TIMESTAMPTZ
);
CREATE INDEX ON supplier (organization_id);

-- A purchase order: the org buys inventory from a supplier, optionally attributed to a store.
CREATE TABLE purchase_order (
    -- purchase order id
    purchase_order_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- owning org (the tenant); the money always belongs to the org
    organization_id   UUID NOT NULL REFERENCES organization (organization_id),
    -- store this purchase is attributed to, if any (NULL = org-wide)
    store_id          UUID REFERENCES store (store_id),
    -- the supplier bought from
    supplier_id       UUID NOT NULL REFERENCES supplier (supplier_id),
    -- the org user who created it
    created_by_user_id UUID REFERENCES "user" (user_id),
    -- why/what (shown to store admins when attributed)
    description       TEXT,
    -- DRAFT | ORDERED | RECEIVED | CANCELLED
    status            purchase_order_status NOT NULL DEFAULT 'DRAFT',
    -- order total (sum of line unit_cost × qty)
    total             NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- when the purchase was created (stored UTC)
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ON purchase_order (organization_id);   -- an org's purchases
CREATE INDEX ON purchase_order (store_id);          -- a store's purchases (when attributed)

-- Line items on a purchase order: which store product, how many, at what cost.
CREATE TABLE purchase_order_product (
    -- the purchase order this line belongs to
    purchase_order_id UUID NOT NULL REFERENCES purchase_order (purchase_order_id),
    -- the store product being bought (stock lands at that store)
    store_product_id  UUID NOT NULL REFERENCES store_product (store_product_id),
    -- how many units
    quantity          INTEGER NOT NULL,
    -- cost per unit (what the org paid the supplier)
    unit_cost         NUMERIC(12, 2) NOT NULL,
    -- one row per product per purchase order
    PRIMARY KEY (purchase_order_id, store_product_id)
);

-- Non-inventory spending (furniture, utilities, ...), optionally attributed to a store.
CREATE TABLE expense (
    -- expense id
    expense_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- owning org (the tenant); the money always belongs to the org
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    -- store this expense is attributed to, if any (NULL = org-wide)
    store_id        UUID REFERENCES store (store_id),
    -- supplier/vendor, if the spend was to one (optional)
    supplier_id     UUID REFERENCES supplier (supplier_id),
    -- the org user who recorded it
    created_by_user_id UUID REFERENCES "user" (user_id),
    -- free-text category (e.g. 'utilities', 'equipment')
    category        TEXT NOT NULL,
    -- why/what
    description     TEXT,
    -- amount spent
    amount          NUMERIC(12, 2) NOT NULL,
    -- when the expense was recorded (stored UTC)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ON expense (organization_id);   -- an org's expenses
CREATE INDEX ON expense (store_id);          -- a store's expenses (when attributed)



-- The shared, org-level customer — one identity recognized across every store (cross-store loyalty).
CREATE TABLE customer (
    -- customer id
    customer_id     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- owning org (the tenant)
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    -- the identity key: same email in an org = the same customer (required, unique per org)
    email           TEXT NOT NULL,
    -- display name
    name            TEXT NOT NULL,
    -- contact phone (TEXT: '+', spaces, extensions)
    phone           TEXT,
    -- when the customer was created (stored UTC)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at      TIMESTAMPTZ,
    -- one customer per email within an org (the org-wide identity)
    UNIQUE (organization_id, email)
);
CREATE INDEX ON customer (organization_id);

-- A store's link to a shared customer — one row per store the customer has shopped at.
CREATE TABLE store_customer (
    -- store-customer link id
    store_customer_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- the store
    store_id          UUID NOT NULL REFERENCES store (store_id),
    -- owning org (the tenant)
    organization_id   UUID NOT NULL REFERENCES organization (organization_id),
    -- the shared org-wide customer this links to (always set)
    customer_id       UUID NOT NULL REFERENCES customer (customer_id),
    -- when first linked at this store (stored UTC)
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- a customer links to a given store at most once
    UNIQUE (store_id, customer_id)
);
CREATE INDEX ON store_customer (store_id);
CREATE INDEX ON store_customer (customer_id);   -- loyalty: every store a customer shops at



```


## Products and customers (two tables per entity: store-level + shared)

Products and customers each use **two tables** — a store-level one and an org-level shared one:

- **Store-level** (`store_product`, `store_customer`) — always written; a row owned by one store.
- **Shared** (`product`, `customer`) — org-level records recognized across every store in the org.

**Customers** are shared org-wide: every `store_customer` links to a shared `customer`, so a person is
recognized across all the org's stores. **Products** share only when the org turns on
`organization.allow_share_products` (off by default → each store's catalog is its own). See
`tenancy.md` for the model and `auth.md` for the write flow.

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



```
@TODO:
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


# Indexes

Reference for every index in the schema and the query it exists for. The declarations live **inline
under each table** above; this section is the map. Primary keys and `UNIQUE` constraints are indexed
automatically by Postgres; the plain `CREATE INDEX`es are the FK/lookup columns we chose to index.

## Primary keys (auto-indexed)

| Table | Primary key | Query it serves |
|---|---|---|
| `plan` | `plan_id` | get a plan by id |
| `feature` | `feature_id` | get a feature by id |
| `plan_feature` | `(plan_id, feature_id)` | list the features in a given plan |
| `organization` | `organization_id` | get an org by id (resolve the tenant) |
| `store` | `store_id` | get a store by id (`/stores/{storeId}/...`) |
| `user` | `user_id` | get a user by id (token resolution) |
| `role` | `role_id` | get a role by id |
| `permission` | `permission_id` | get a permission by id |
| `role_permission` | `(role_id, permission_id)` | list the permissions granted to a given role |
| `membership` | `membership_id` | get a membership by id |
| `membership_assignment` | `(membership_id, role_id)` | list the roles on a given membership |
| `store_membership_detail` | `membership_id` | get the store-only detail (PIN) for a membership |
| `organization_membership_detail` | `membership_id` | get the org-only detail for a membership |
| `sales_order` | `order_id` | get an order by id |
| `sales_order_product` | `(order_id, store_product_id)` | list the line items on a given order |
| `sales_order_return` | `return_id` | get a return by id |
| `sales_order_return_product` | `(return_id, store_product_id)` | list the line items on a given return |
| `supplier` | `supplier_id` | get a supplier by id |
| `purchase_order` | `purchase_order_id` | get a purchase order by id |
| `purchase_order_product` | `(purchase_order_id, store_product_id)` | list the line items on a given purchase order |
| `expense` | `expense_id` | get an expense by id |
| `customer` | `customer_id` | get a customer by id |
| `store_customer` | `store_customer_id` | get a store-customer link by id |
| `store_product` | `store_product_id` | get a store product by id |
| `product` | `product_id` | get a shared product by id |

## Unique constraints (auto-indexed + enforce a rule)

| Constraint | Query / rule it serves |
|---|---|
| `plan.name` | look up a plan by name / no two plans share a name |
| `feature.code` | look up a feature by its code / codes don't collide |
| `user.email` | **login: find the account for an email** / one account per email |
| `permission (resource, action)` | look up a permission like `product:read` / no duplicates |
| `customer (organization_id, email)` | find a customer by email in an org / one identity per email |
| `store_customer (store_id, customer_id)` | is this customer already linked to this store? / no duplicate link |
| `store_product (store_id, sku)` | find a store's product by SKU (barcode scan) / no duplicate SKU per store |
| `product (organization_id, sku)` | find the shared product by SKU in an org / no duplicate |

## Unique partial indexes (rule + query)

| Index | Query / rule it serves |
|---|---|
| `role_org_name_uq` — `role(organization_id, name) WHERE organization_id IS NOT NULL` | find an org's custom role by name / no duplicate name per org |
| `role_managed_name_uq` — `role(name) WHERE is_managed` | find a system role by name / managed names unique |
| `membership_store_uq` — `membership(user_id, store_id) WHERE store_id IS NOT NULL AND NOT is_deleted` | is this user a live member of this store? / one live membership per store |
| `membership_org_uq` — `membership(user_id) WHERE scope='ORGANIZATION' AND NOT is_deleted` | does this user have a live org membership? / at most one |

## Plain indexes

| Index | Query it serves |
|---|---|
| `store (organization_id)` | all stores in an org |
| `"user" (organization_id)` | all users in an org (admin user list) |
| `membership (user_id)` | **all of a user's memberships (authz, every request)** |
| `sales_order (store_id)` | a store's orders |
| `sales_order_return (store_id)` | a store's returns |
| `supplier (organization_id)` | an org's suppliers |
| `purchase_order (organization_id)` | an org's purchase orders |
| `purchase_order (store_id)` | the POs attributed to a store |
| `expense (organization_id)` | an org's expenses |
| `expense (store_id)` | the expenses attributed to a store |
| `customer (organization_id)` | an org's customers |
| `store_customer (store_id)` | a store's customers |
| `store_customer (customer_id)` | **every store a customer shops at (loyalty)** |


## CHECK constraints 

| Table | The CHECK | Rule in plain words | What it prevents |
|---|---|---|---|
| `role` | `CHECK ((is_managed = TRUE AND organization_id IS NULL) OR (is_managed = FALSE AND organization_id IS NOT NULL))` | A role is either **managed** (a system role we ship — belongs to no org, so `organization_id` must be NULL) or **custom** (an org built it — so `organization_id` must be set). One or the other, never mixed. | a managed role wrongly tied to one org, or a custom role floating with no owner |
| `membership` | `CHECK ((scope = 'ORGANIZATION' AND store_id IS NULL) OR (scope = 'STORE' AND store_id IS NOT NULL))` | A membership is either at the **org** (so `store_id` must be NULL) or at a **store** (so `store_id` must be set). The `store_id` has to match the `scope`. | an org membership with a store set, or a store membership with no store |



