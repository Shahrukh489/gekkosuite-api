-- ============================================================================
-- Combined schema — merges tools/GekkoSuite.Database/Migrations/1_0_0.sql,
-- 1_0_3.sql, 1_0_4.sql, and triggers_todo into one file for pasting into
-- Adminer/pgAdmin in a single run, instead of running each migration file
-- separately.
--
-- Deliberately placed in local/, NOT in the Migrations/ folder: that folder's
-- *.sql files are embedded into GekkoSuite.Database and picked up by `dotnet
-- run migrate`, so a copy here would get re-applied (and fail on "already
-- exists") the next time someone runs the real migration tool.
--
-- 1_0_2.sql is intentionally NOT included: every statement in it (CREATE TYPE
-- offering_type, CREATE TABLE offering, and the seed INSERT INTO offering) is
-- either an exact duplicate of what 1_0_0.sql already creates below, or seed
-- data — and seeding is left entirely to local/mock_data.sql, run separately
-- after this file.
-- ============================================================================


-- ============================================================================
-- From 1_0_0.sql
-- ============================================================================

-- The scope a membership/role/feature operates at.
CREATE TYPE scope AS ENUM ('ORGANIZATION', 'STORE');

-- How a store sells: a storefront kind.
CREATE TYPE store_type AS ENUM ('ONLINE', 'PHYSICAL');

-- The org's overall billing status
CREATE TYPE billing_status AS ENUM ('ACTIVE', 'PAST_DUE', 'UNPAID', 'CANCELED');

-- A subscription's LIFECYCLE
CREATE TYPE offering_status AS ENUM ('TRIALING', 'ACTIVE', 'CANCELED');

-- What an org can subscribe to: a base PLAN or a optional ADDON.
CREATE TYPE offering_type AS ENUM ('PLAN', 'ADDON');

-- An offering: a priced bundle of features an org subscribes to. Either a base PLAN or a stackable ADDON.
CREATE TABLE offering (
    -- offering id
    offering_id     UUID PRIMARY KEY,
    -- PLAN | ADDON: PLAN = baseline plan; ADDON = stackable extra bought on top
    type            offering_type NOT NULL,
    -- display name shown in the plan/add-on catalog, e.g. 'Basic', 'Pro', 'Marketing'
    name            VARCHAR(256) NOT NULL UNIQUE,
    -- optional human description of what the offering includes
    description     VARCHAR(1024),
    -- per-store price; the bill adds this × store count for each of the org's live offerings (0 = free)
    price_per_store NUMERIC NOT NULL,
    -- still offered in the catalog? (FALSE = retired; existing subscriptions keep it)
    is_active       BOOLEAN NOT NULL
);

-- A feature: one capability an offering can include (reports, returns, multi-store, ...).
CREATE TABLE feature (
    -- feature id
    feature_id  UUID PRIMARY KEY,
    -- fixed key the app looks up in code, e.g. 'store_reporting', 'ai_chatbot' — never rename it (breaks checks)
    code        VARCHAR(64) NOT NULL UNIQUE,
    -- display name shown to users, e.g. 'Multi-store' — safe to rename anytime
    label       VARCHAR(256) NOT NULL,
    -- ORGANIZATION | STORE: where the feature applies (org-level like billing, or shown in a store)
    scope       scope NOT NULL,
    -- optional human description of what the feature does
    description VARCHAR(1024)
);

-- offering_feature: which features an offering includes (many-to-many).
CREATE TABLE offering_feature (
    -- the offering
    offering_id UUID NOT NULL REFERENCES offering (offering_id),
    -- the feature it includes
    feature_id  UUID NOT NULL REFERENCES feature (feature_id),
    -- an offering can't list the same feature twice
    PRIMARY KEY (offering_id, feature_id)
);



-- The organization: the business and the tenant (unit of isolation). Owns stores, users, and settings.
CREATE TABLE organization (
    -- tenant id
    organization_id  UUID PRIMARY KEY,
    -- the business's display name (required)
    name             VARCHAR(256) NOT NULL,
    -- optional free-text note about the business
    description      VARCHAR(1024),
    -- the org's overall payment state for its ONE itemized bill (all subscriptions)
    billing_status   billing_status NOT NULL,
    -- when the org was onboarded (stored UTC)
    created_at       TIMESTAMPTZ NOT NULL,
    -- when the org was last modified (stored UTC)
    updated_at       TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = org removed but kept for history
    is_deleted       BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ
);

-- A subscription: ties an org to an offering (a plan or an add-on)
CREATE TABLE subscription (
    -- subscription id
    subscription_id     UUID PRIMARY KEY,
    -- the org this subscription belongs to
    organization_id     UUID NOT NULL REFERENCES organization (organization_id),
    -- the offering this subscription is for — its features contribute to the org's effective feature set
    offering_id         UUID NOT NULL REFERENCES offering (offering_id),
    -- TRIALING (free trial, features on) | ACTIVE (on & billed) | CANCELED (off)
    status              offering_status NOT NULL,
    -- when a free trial ends (NULL if not trialing)
    trial_ends_at       TIMESTAMPTZ,
    -- end of the current paid period (renewal/billing boundary)
    current_period_end  TIMESTAMPTZ,
    -- when this subscription row started (stored UTC)
    created_at          TIMESTAMPTZ NOT NULL,
    -- when it ended (NULL = still live). A live row counts toward the org's features & bill.
    ended_at            TIMESTAMPTZ
);
CREATE INDEX ON subscription (organization_id);

-- A user: one login per person. Where they can act comes from their memberships (see auth.md).
CREATE TABLE user_account (
    -- user id
    user_id            UUID PRIMARY KEY,
    -- home org (set once, immutable) — the tenant boundary
    organization_id    UUID NOT NULL REFERENCES organization (organization_id),
    -- login identifier; globally unique (see UNIQUE below). 254 = RFC 5321 max email length
    email              VARCHAR(254) NOT NULL,
    -- password hash — slow salted KDF (argon2id); NEVER plaintext (see auth.md). Sized for the encoded
    -- self-describing hash (algorithm + params + salt + hash), not just the raw digest
    password           VARCHAR(512) NOT NULL,
    -- first name
    first_name         VARCHAR(128) NOT NULL,
    -- last name
    last_name          VARCHAR(128) NOT NULL,
    -- contact phone (TEXT-like: '+', spaces, extensions); optional
    phone              VARCHAR(32),
    -- account kill switch; false = all memberships suspended (revoke, not delete)
    is_active          BOOLEAN NOT NULL,
    -- the org owner — full access that can't be stripped (transfer only). Exactly one per org (index below)
    is_org_owner       BOOLEAN NOT NULL,
    -- the admin who created this account (audit); NULL only for the bootstrap owner
    created_by_user_id UUID REFERENCES user_account (user_id),
    -- when the account was created (stored UTC)
    created_at         TIMESTAMPTZ NOT NULL,
    -- when the account was last modified (stored UTC)
    updated_at         TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted         BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at         TIMESTAMPTZ,
    -- one email = one account across the whole system
    UNIQUE (email),
    -- audit trail is mandatory for everyone EXCEPT the bootstrap owner (who has no creator). This also
    -- keeps org → user from being a circular FK — the owner is flagged, not pointed at.
    CHECK ((is_org_owner AND created_by_user_id IS NULL)
        OR (NOT is_org_owner AND created_by_user_id IS NOT NULL)),
    -- emails are stored lowercased so the case-sensitive UNIQUE(email) can't allow Foo@x / foo@x dupes
    CHECK (email = lower(email))
);

CREATE INDEX ON user_account (organization_id);
-- exactly one owner per org (only live users count)
CREATE UNIQUE INDEX user_one_owner_per_org ON user_account (organization_id) WHERE is_org_owner AND NOT is_deleted;

-- A store: the business unit where selling happens. Owned by one org.
CREATE TABLE store (
    -- store id
    store_id         UUID PRIMARY KEY,
    -- owning org (the tenant)
    organization_id  UUID NOT NULL REFERENCES organization (organization_id),
    -- store display name
    name             VARCHAR(256) NOT NULL,
    -- ONLINE | PHYSICAL: how the store sells
    type             store_type NOT NULL,
    -- description / notes about the store
    description      VARCHAR(1024),
    -- physical location (all NULL for an ONLINE store) --
    -- street address
    address          VARCHAR(256),
    -- geographic city
    city             VARCHAR(128),
    -- geographic state/province
    state            VARCHAR(128),
    -- postal/zip code — text to preserve leading zeros and non-numeric formats
    postal_code      VARCHAR(32),
    -- ISO 3166-1 alpha-2 country code, e.g. 'US'
    country          VARCHAR(2),
    -- ISO 4217 currency the store sells in, e.g. 'USD'
    currency         VARCHAR(3),
    -- store contact phone ('+', spaces, extensions)
    phone            VARCHAR(32),
    -- store contact email
    email            VARCHAR(254),
    -- the org's default store — where single-store orgs (and org users) land. Exactly one per org (index below)
    is_default       BOOLEAN NOT NULL,
    -- when the store was created (stored UTC)
    created_at       TIMESTAMPTZ NOT NULL,
    -- when the store was last modified (stored UTC)
    updated_at       TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = store removed but kept for history
    is_deleted       BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ
);
CREATE INDEX ON store (organization_id);
-- at most one default store per org (only live stores count)
CREATE UNIQUE INDEX store_one_default_per_org ON store (organization_id) WHERE is_default AND NOT is_deleted;

-- A product: the shared, org-level identity for an item — written only when the org turns on
-- share_products (product.md). store_product is always written regardless; this is its optional
-- org-wide mirror, used for cross-store search/reporting and seeding a new store's catalog. Never
-- carries price, cost, or stock — those stay per-store even when the identity is shared (CLAUDE.md).
-- supplier_id is deferred until the `supplier` table itself is migrated (product.md).
CREATE TABLE product (
    -- product id
    product_id                 UUID PRIMARY KEY,
    -- owning org (the tenant)
    organization_id            UUID NOT NULL REFERENCES organization (organization_id),
    -- product display name, e.g. 'Espresso Beans 1kg'
    name                       VARCHAR(256) NOT NULL,
    -- optional human description
    description                VARCHAR(1024),
    -- free-text grouping, e.g. 'Bakery', 'Beverages'
    category                   VARCHAR(128),
    -- free-text brand/manufacturer, e.g. 'Lavazza'
    brand                      VARCHAR(128),
    -- up to 3 variant dimension VALUES (Vanilla/Large/Red/...) mirroring the linked store_product's
    -- per-variant values when sharing is on. Dimension NAMES live on store_product_group, not here
    -- (product.md "Variants") — from 1_0_4.sql
    variant_option_one_value   VARCHAR(256),
    variant_option_two_value   VARCHAR(256),
    variant_option_three_value VARCHAR(256),
    -- when the shared record was created (stored UTC)
    created_at                 TIMESTAMPTZ NOT NULL,
    -- when it was last modified (stored UTC)
    updated_at                 TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted                 BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at                 TIMESTAMPTZ
);
CREATE INDEX ON product (organization_id);

-- A store_product_group: the variant parent, owning the shared identity (name, description, category,
-- brand, variant dimension NAMES) once per product family. Never itself sellable — no price/sku/stock/
-- tax fields. Store-scoped, matching the rest of the catalog's default-isolated model. From 1_0_4.sql
-- (product.md "Variants").
CREATE TABLE store_product_group (
    -- group id
    group_id                   UUID PRIMARY KEY,
    -- the store this product family belongs to
    store_id                   UUID NOT NULL REFERENCES store (store_id),
    -- the owning org (the tenant boundary — stamped for RLS)
    organization_id            UUID NOT NULL REFERENCES organization (organization_id),
    -- product display name, owned here once instead of on every variant row
    name                       VARCHAR(256) NOT NULL,
    -- optional human description
    description                VARCHAR(1024),
    -- free-text grouping for browsing/filtering, e.g. 'Beverages'
    category                   VARCHAR(128),
    -- free-text brand/manufacturer, e.g. 'Lavazza'
    brand                      VARCHAR(128),
    -- up to 3 variant dimension NAMES this product varies along, e.g. 'Flavor'. Defined once per group;
    -- each store_product variant supplies the matching VALUE.
    variant_option_one_name    VARCHAR(64),
    variant_option_two_name    VARCHAR(64),
    variant_option_three_name  VARCHAR(64),
    -- when the group was created (stored UTC)
    created_at                 TIMESTAMPTZ NOT NULL,
    -- when it was last modified (stored UTC)
    updated_at                 TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted                 BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at                 TIMESTAMPTZ
);
CREATE INDEX ON store_product_group (store_id);

-- A store_product: a product as it exists AT ONE STORE — its own stock and price. Always written when a
-- product is created (the shared org-level `product` is written only when share_products is on). Stock and
-- price are ALWAYS per-store, never shared (see tenancy.md / CLAUDE.md). Must be able to stand alone with
-- no shared `product` row, since sharing is off by default (product.md).
CREATE TABLE store_product (
    -- store_product id
    store_product_id UUID PRIMARY KEY,
    -- the store this product belongs to (its business unit)
    store_id         UUID NOT NULL REFERENCES store (store_id),
    -- the owning org (the tenant boundary — stamped for RLS and cross-store isolation)
    organization_id  UUID NOT NULL REFERENCES organization (organization_id),
    -- the shared org-wide identity this product is recognized as; set only when share_products is on
    -- (product.md — "How product and store_product connect")
    product_id       UUID REFERENCES product (product_id),
    -- the variant family this row belongs to; NULL = standalone product (the common case). From
    -- 1_0_4.sql (product.md "Variants")
    group_id         UUID REFERENCES store_product_group (group_id),
    -- product display name, e.g. 'Espresso Beans 1kg'. Required when group_id is NULL; must be NULL
    -- when group_id is set — the group owns naming for grouped variants (CHECK below)
    name             VARCHAR(256),
    -- optional human description
    description      VARCHAR(1024),
    -- stock-keeping unit; the store's own product code (optional, unique per store — index below)
    sku              VARCHAR(64),
    -- manufacturer barcode (UPC/EAN); optional, unique per store — index below (distinct from `sku`,
    -- the store's own code: a barcode scan at checkout looks this up, not the SKU)
    barcode          VARCHAR(64),
    -- free-text grouping for browsing/filtering at checkout, e.g. 'Bakery', 'Beverages'
    category         VARCHAR(128),
    -- free-text brand/manufacturer, e.g. 'Lavazza'
    brand            VARCHAR(128),
    -- this variant's own value along each dimension, e.g. 'Vanilla'. The dimension NAMES (e.g.
    -- 'Flavor') live once on store_product_group, not repeated per row — see product.md "Variants"
    variant_option_one_value   VARCHAR(256),
    variant_option_two_value   VARCHAR(256),
    variant_option_three_value VARCHAR(256),
    -- this store's selling price (never shared across stores)
    price            NUMERIC NOT NULL,
    -- what this store paid per unit (never shared across stores); for margin reporting, not shown at checkout
    cost             NUMERIC,
    -- units on hand at this store (never shared across stores); allowed to go negative — overselling/
    -- backorder is real, not a bug (product.md's "Data quality" — do not add a `stock >= 0` CHECK)
    stock            INTEGER NOT NULL,
    -- whether stock is decremented on sale at all; FALSE = never tracked (e.g. a service, not a good)
    track_inventory  BOOLEAN NOT NULL DEFAULT TRUE,
    -- whether a sale of this product is taxed; FALSE = always tax-exempt regardless of tax_rate
    is_taxable       BOOLEAN NOT NULL DEFAULT TRUE,
    -- tax percentage applied at checkout when is_taxable (e.g. 8.25 = 8.25%); ignored otherwise
    tax_rate         NUMERIC(5, 2) NOT NULL DEFAULT 0,
    -- optional low-stock threshold for reorder alerts; NULL = no threshold set
    reorder_point    INTEGER,
    -- listing toggle; FALSE = hidden from selling but kept
    is_active        BOOLEAN NOT NULL,
    -- when the product was created at this store (stored UTC)
    created_at       TIMESTAMPTZ NOT NULL,
    -- when it was last modified (stored UTC)
    updated_at       TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed from this store but kept for history
    is_deleted       BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ,
    -- a row either stands alone and owns its own name, or belongs to a group and defers naming to it.
    -- Never both, never neither. From 1_0_4.sql
    CONSTRAINT store_product_group_name_xor CHECK ((group_id IS NULL) = (name IS NOT NULL))
);
CREATE INDEX ON store_product (store_id);
-- a SKU is unique within a store (only live products count); NULL SKUs are exempt
CREATE UNIQUE INDEX store_product_sku_per_store ON store_product (store_id, sku) WHERE sku IS NOT NULL AND NOT is_deleted;
-- a barcode is unique within a store (only live products count); NULL barcodes are exempt
CREATE UNIQUE INDEX store_product_barcode_per_store ON store_product (store_id, barcode) WHERE barcode IS NOT NULL AND NOT is_deleted;
-- products linked to a shared org-wide identity (only rows where share_products created one)
CREATE INDEX ON store_product (product_id) WHERE product_id IS NOT NULL;
-- variants belonging to the same product family (only rows in a group)
CREATE INDEX ON store_product (group_id) WHERE group_id IS NOT NULL;

-- A store_customer: a customer as known AT ONE STORE. Always written when a customer is created (the
-- shared org-level `customer` — recognized org-wide — is written in the same transaction; see
-- tenancy.md / CLAUDE.md). Each store owns its own row.
CREATE TABLE store_customer (
    -- store_customer id
    store_customer_id UUID PRIMARY KEY,
    -- the store this customer belongs to (its business unit)
    store_id          UUID NOT NULL REFERENCES store (store_id),
    -- the owning org (the tenant boundary — stamped for RLS and cross-store isolation)
    organization_id   UUID NOT NULL REFERENCES organization (organization_id),
    -- customer's full name, e.g. 'Jane Doe'
    name              VARCHAR(256) NOT NULL,
    -- contact email; optional (unique per store — index below)
    email             VARCHAR(254),
    -- contact phone ('+', spaces, extensions); optional
    phone             VARCHAR(32),
    -- opt-in flag for a store loyalty/marketing list
    is_active         BOOLEAN NOT NULL,
    -- when the customer was first added at this store (stored UTC)
    created_at        TIMESTAMPTZ NOT NULL,
    -- when it was last modified (stored UTC)
    updated_at        TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed from this store but kept for history
    is_deleted        BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at        TIMESTAMPTZ
);
CREATE INDEX ON store_customer (store_id);
-- an email is unique within a store (only live customers count); NULL emails are exempt
CREATE UNIQUE INDEX store_customer_email_per_store ON store_customer (store_id, email) WHERE email IS NOT NULL AND NOT is_deleted;


-- A role: a named bundle of permissions. Either a managed role we ship, or an org's own custom role.
CREATE TABLE role (
    -- role id
    role_id         UUID PRIMARY KEY,
    -- display name shown in the role picker, e.g. 'Cashier', 'Org Admin'
    name            VARCHAR(256) NOT NULL,
    -- optional human description of what the role is for
    description     VARCHAR(1024),
    -- TRUE = system role we ship (org-wide); FALSE = an org's own custom role
    is_managed      BOOLEAN NOT NULL,
    -- owning org for a custom role; NULL for managed roles (see CHECK)
    organization_id UUID REFERENCES organization (organization_id),
    -- ORGANIZATION | STORE: the level this role attaches at (must match the membership's scope)
    scope           scope NOT NULL,
    -- when the role was created (stored UTC)
    created_at      TIMESTAMPTZ NOT NULL,
    -- when the role was last modified (stored UTC)
    updated_at      TIMESTAMPTZ NOT NULL,
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

-- A permission: one allowed action, like product:read.
CREATE TABLE permission (
    -- permission id
    permission_id UUID PRIMARY KEY,
    -- the thing acted on, e.g. product, order, role
    resource      VARCHAR(64) NOT NULL,
    -- what may be done to it, e.g. read, create, refund, assign
    action        VARCHAR(64) NOT NULL,
    -- optional description
    description   VARCHAR(1024),
    -- TRUE = for org roles only, FALSE = can be added to both STORE and ORGANIZATION roles
    is_elevated   BOOLEAN NOT NULL,
    -- resource + action is the permission's natural key, e.g. (product, read)
    UNIQUE (resource, action)
);

-- role_permission: which permissions a role grants (many-to-many).
CREATE TABLE role_permission (
    -- the role
    role_id       UUID NOT NULL REFERENCES role (role_id),
    -- the permission it grants
    permission_id UUID NOT NULL REFERENCES permission (permission_id),
    -- a role can't list the same permission twice
    PRIMARY KEY (role_id, permission_id)
);

-- An ELEVATED (company-level) permission may only sit in an ORGANIZATION-scoped role, so it can never
-- reach a store seat (e.g. user:create must not land in a store role → a cashier creating users). Spans
-- permission + role, so it's a trigger, not a CHECK. App service is the primary guard; this is the backstop.
-- CREATE OR REPLACE FUNCTION enforce_role_permission_elevated() RETURNS trigger AS $$
-- BEGIN
--     IF (SELECT p.is_elevated FROM permission p WHERE p.permission_id = NEW.permission_id)
--        AND (SELECT r.scope FROM role r WHERE r.role_id = NEW.role_id) <> 'ORGANIZATION' THEN
--         RAISE EXCEPTION 'elevated permission % may only be granted to an ORGANIZATION role', NEW.permission_id;
--     END IF;
--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER role_permission_elevated_guard
--     BEFORE INSERT OR UPDATE ON role_permission
--     FOR EACH ROW EXECUTE FUNCTION enforce_role_permission_elevated();



-- A membership: a place a user belongs — the whole ORGANIZATION, or one STORE. Sets their reach there.
CREATE TABLE membership (
    -- membership id
    membership_id   UUID PRIMARY KEY,
    -- the user this membership belongs to
    user_id         UUID NOT NULL REFERENCES user_account (user_id),
    -- ORGANIZATION | STORE: the kind of place (see CHECK for the store_id rule)
    scope           scope NOT NULL,
    -- the tenant this membership is in (the boundary every request is checked against)
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    -- the store, for a STORE membership; NULL for an ORGANIZATION membership
    store_id        UUID REFERENCES store (store_id),
    -- suspend switch for this one place; false = access off here but kept
    is_active       BOOLEAN NOT NULL,
    -- when the membership was granted (stored UTC)
    created_at      TIMESTAMPTZ NOT NULL,
    -- when the membership was last modified (stored UTC)
    updated_at      TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed from this place but kept for history
    is_deleted      BOOLEAN NOT NULL,
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

-- all of a user's memberships
CREATE INDEX ON membership (user_id);

-- The one-kind rule: a user's live memberships must be ALL organization or ALL store, never both — so a
-- STORE user can never reach ORGANIZATION data (see auth.md, CLAUDE.md). This spans multiple rows (all of
-- a user's memberships), which a CHECK can't express, so it's a trigger. The app service is the primary
-- guard on the write path; this is the database backstop.
-- CREATE OR REPLACE FUNCTION enforce_one_membership_kind() RETURNS trigger AS $$
-- BEGIN
--     IF NEW.is_deleted THEN
--         RETURN NEW;   -- soft-deleting a membership can never create a conflict
--     END IF;
--     IF EXISTS (
--         SELECT 1 FROM membership m
--         WHERE m.user_id = NEW.user_id
--           AND m.membership_id <> NEW.membership_id
--           AND m.scope <> NEW.scope
--           AND NOT m.is_deleted
--     ) THEN
--         RAISE EXCEPTION 'user % already holds a live membership of the other scope (one-kind rule)', NEW.user_id;
--     END IF;
--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER membership_one_kind
--     BEFORE INSERT OR UPDATE ON membership
--     FOR EACH ROW EXECUTE FUNCTION enforce_one_membership_kind();

-- -- A membership must live in the user's OWN organization — its organization_id has to match the user's
-- -- home org. Prevents giving a user a membership in another org (cross-tenant access). Joins membership →
-- -- user, so it's a trigger, not a CHECK.
-- CREATE OR REPLACE FUNCTION enforce_membership_in_user_org() RETURNS trigger AS $$
-- BEGIN
--     IF NEW.organization_id <> (SELECT u.organization_id FROM user_account u WHERE u.user_id = NEW.user_id) THEN
--         RAISE EXCEPTION 'membership org % does not match user %''s home organization', NEW.organization_id, NEW.user_id;
--     END IF;
--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER membership_in_user_org
--     BEFORE INSERT OR UPDATE ON membership
--     FOR EACH ROW EXECUTE FUNCTION enforce_membership_in_user_org();

-- membership_assignment: a role granted to a membership (many-to-many).
CREATE TABLE membership_assignment (
    -- assignment id
    assignment_id UUID PRIMARY KEY,
    -- the membership the role is granted to
    membership_id UUID NOT NULL REFERENCES membership (membership_id),
    -- the role granted
    role_id       UUID NOT NULL REFERENCES role (role_id),
    -- when the role was granted (stored UTC)
    assigned_at   TIMESTAMPTZ NOT NULL,
    -- who granted it
    assigned_by_user_id UUID NOT NULL REFERENCES user_account (user_id),
    -- optional expiry; NULL = never expires
    expires_at    TIMESTAMPTZ,
    -- same role can't be granted to the same membership twice
    UNIQUE (membership_id, role_id)
);

-- A role's scope must match the membership's scope: a STORE role only on a STORE membership, an
-- ORGANIZATION role only on an ORGANIZATION membership. Prevents an org role on a store seat (store
-- employee gets org-wide reach). Joins role + membership, so it's a trigger, not a CHECK.
-- CREATE OR REPLACE FUNCTION enforce_assignment_scope_match() RETURNS trigger AS $$
-- BEGIN
--     IF (SELECT r.scope FROM role r WHERE r.role_id = NEW.role_id)
--        <> (SELECT m.scope FROM membership m WHERE m.membership_id = NEW.membership_id) THEN
--         RAISE EXCEPTION 'role % scope does not match membership % scope', NEW.role_id, NEW.membership_id;
--     END IF;
--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER membership_assignment_type_guard
--     BEFORE INSERT OR UPDATE ON membership_assignment
--     FOR EACH ROW EXECUTE FUNCTION enforce_assignment_scope_match();


-- ============================================================================
-- 1_0_2.sql skipped here — see the note at the top of this file.
-- ============================================================================


-- ============================================================================
-- From 1_0_3.sql
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


-- ============================================================================
-- From triggers_todo — draft V1.0.1 trigger definitions, never enabled
-- (every statement below was already commented out in the source file, kept
-- disabled here too). Reference only; the app-level checks are the real
-- guard for these invariants today (see auth.md).
-- ============================================================================

-- -- ============================================================================
-- -- V1.0.1 — RBAC schema (roles, permissions, memberships) + the managed role
-- -- catalog. Mirrors docs/database.md's role/permission/membership tables and
-- -- the four cross-table trigger invariants documented there but not given as
-- -- SQL. Seeds Cashier, Manager, and Org Admin (the three roles every worked
-- -- example across the docs settles on) plus every permission those docs name
-- -- by resource:action.
-- --
-- -- Same convention as 1_0_0.sql: NO column defaults, NO server-side value
-- -- generation. Every value — ids, timestamps, flags — is supplied explicitly,
-- -- including in the seed INSERTs below (this is reference/system data every
-- -- environment needs, unlike local/mock_data.sql's throwaway tenant fixtures).
-- -- ============================================================================

-- -- ============================================================================
-- -- Triggers — cross-table invariants a CHECK can't express because they read
-- -- sibling rows or other tables (see database.md's "Optional Triggers" table).
-- -- ============================================================================

-- -- An elevated permission (is_elevated = TRUE) may only sit on an ORGANIZATION-scope role, so a store
-- -- role can never carry a company-level power like user:create.
-- CREATE FUNCTION role_permission_elevated_guard_fn() RETURNS TRIGGER AS $$
-- DECLARE
--     v_is_elevated BOOLEAN;
--     v_role_scope  scope;
-- BEGIN
--     SELECT is_elevated INTO v_is_elevated FROM permission WHERE permission_id = NEW.permission_id;
--     SELECT scope INTO v_role_scope FROM role WHERE role_id = NEW.role_id;

--     IF v_is_elevated AND v_role_scope <> 'ORGANIZATION' THEN
--         RAISE EXCEPTION 'Elevated permission (%) cannot be granted to a % role', NEW.permission_id, v_role_scope;
--     END IF;

--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER role_permission_elevated_guard
--     BEFORE INSERT OR UPDATE ON role_permission
--     FOR EACH ROW EXECUTE FUNCTION role_permission_elevated_guard_fn();

-- -- A role's scope must match the membership it's granted to, so a STORE seat can never end up with an
-- -- ORGANIZATION role's wider reach.
-- CREATE FUNCTION membership_assignment_type_guard_fn() RETURNS TRIGGER AS $$
-- DECLARE
--     v_role_scope       scope;
--     v_membership_scope scope;
-- BEGIN
--     SELECT scope INTO v_role_scope FROM role WHERE role_id = NEW.role_id;
--     SELECT scope INTO v_membership_scope FROM membership WHERE membership_id = NEW.membership_id;

--     IF v_role_scope <> v_membership_scope THEN
--         RAISE EXCEPTION 'Role scope (%) does not match membership scope (%)', v_role_scope, v_membership_scope;
--     END IF;

--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER membership_assignment_type_guard
--     BEFORE INSERT OR UPDATE ON membership_assignment
--     FOR EACH ROW EXECUTE FUNCTION membership_assignment_type_guard_fn();

-- -- A user can only be given a membership in their own home organization.
-- CREATE FUNCTION user_organization_membership_guard_fn() RETURNS TRIGGER AS $$
-- DECLARE
--     v_user_org UUID;
-- BEGIN
--     SELECT organization_id INTO v_user_org FROM user_account WHERE user_id = NEW.user_id;

--     IF v_user_org <> NEW.organization_id THEN
--         RAISE EXCEPTION 'User (%) belongs to organization (%), cannot be given a membership in (%)', NEW.user_id, v_user_org, NEW.organization_id;
--     END IF;

--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER user_organization_membership
--     BEFORE INSERT OR UPDATE ON membership
--     FOR EACH ROW EXECUTE FUNCTION user_organization_membership_guard_fn();

-- -- A user is either an ORGANIZATION member or a STORE member, never both (mixing the two would let a
-- -- store employee also reach org-wide data). Only checks against other LIVE memberships.
-- CREATE FUNCTION membership_single_kind_guard_fn() RETURNS TRIGGER AS $$
-- DECLARE
--     v_conflicting_count INTEGER;
-- BEGIN
--     IF NEW.is_deleted THEN
--         RETURN NEW;
--     END IF;

--     SELECT COUNT(*) INTO v_conflicting_count
--     FROM membership
--     WHERE user_id = NEW.user_id
--       AND membership_id <> NEW.membership_id
--       AND NOT is_deleted
--       AND scope <> NEW.scope;

--     IF v_conflicting_count > 0 THEN
--         RAISE EXCEPTION 'User (%) already holds a membership of the other scope; a user is either an ORGANIZATION or a STORE member, never both', NEW.user_id;
--     END IF;

--     RETURN NEW;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE TRIGGER membership_single_kind_guard
--     BEFORE INSERT OR UPDATE ON membership
--     FOR EACH ROW EXECUTE FUNCTION membership_single_kind_guard_fn();
