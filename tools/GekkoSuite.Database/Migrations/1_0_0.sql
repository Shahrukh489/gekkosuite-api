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
    -- type based on theie memberships
    user_type          scope,
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

-- A store_product: a product as it exists AT ONE STORE — its own stock and price. Always written when a
-- product is created (the shared org-level `product` is written only when share_products is on). Stock and
-- price are ALWAYS per-store, never shared (see tenancy.md / CLAUDE.md).
CREATE TABLE store_product (
    -- store_product id
    store_product_id UUID PRIMARY KEY,
    -- the store this product belongs to (its business unit)
    store_id         UUID NOT NULL REFERENCES store (store_id),
    -- the owning org (the tenant boundary — stamped for RLS and cross-store isolation)
    organization_id  UUID NOT NULL REFERENCES organization (organization_id),
    -- product display name, e.g. 'Espresso Beans 1kg'
    name             VARCHAR(256) NOT NULL,
    -- optional human description
    description      VARCHAR(1024),
    -- stock-keeping unit; the store's own product code (optional, unique per store — index below)
    sku              VARCHAR(64),
    -- this store's selling price (never shared across stores)
    price            NUMERIC NOT NULL,
    -- units on hand at this store (never shared across stores)
    stock            INTEGER NOT NULL,
    -- listing toggle; FALSE = hidden from selling but kept
    is_active        BOOLEAN NOT NULL,
    -- when the product was created at this store (stored UTC)
    created_at       TIMESTAMPTZ NOT NULL,
    -- when it was last modified (stored UTC)
    updated_at       TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed from this store but kept for history
    is_deleted       BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ
);
CREATE INDEX ON store_product (store_id);
-- a SKU is unique within a store (only live products count); NULL SKUs are exempt
CREATE UNIQUE INDEX store_product_sku_per_store ON store_product (store_id, sku) WHERE sku IS NOT NULL AND NOT is_deleted;

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
