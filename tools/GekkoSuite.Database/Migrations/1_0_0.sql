-- The scope a membership/role/feature operates at.
CREATE TYPE scope AS ENUM ('ORGANIZATION', 'STORE');

-- How a store sells: a storefront kind.
CREATE TYPE store_type AS ENUM ('ONLINE', 'PHYSICAL');

-- The org's overall billing status (organization.billing_status). Purely about paying — no TRIALING
-- (that's a per-subscription lifecycle state). See docs/database.md and auth.md's billing gate.
CREATE TYPE billing_status AS ENUM ('ACTIVE', 'PAST_DUE', 'UNPAID', 'CANCELED');

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
    -- soft-delete flag; TRUE = org removed but kept for history
    is_deleted       BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ
);

-- A user: one login per person. Where they can act comes from their memberships (see auth.md).
CREATE TABLE "user" (
    -- user id
    user_id            UUID PRIMARY KEY,
    -- home org (set once, immutable) — the tenant boundary
    organization_id    UUID NOT NULL REFERENCES organization (organization_id),
    -- login identifier; globally unique (see UNIQUE below). 254 = RFC 5321 max email length
    email              VARCHAR(254) NOT NULL,
    -- password hash — slow salted KDF (argon2id); NEVER plaintext (see auth.md). Sized for the encoded
    -- self-describing hash (algorithm + params + salt + hash), not just the raw digest
    password           VARCHAR(512) NOT NULL,
    -- display name shown in the UI
    name               VARCHAR(256) NOT NULL,
    -- contact phone (TEXT-like: '+', spaces, extensions); optional
    phone              VARCHAR(32),
    -- account kill switch; false = all memberships suspended (revoke, not delete)
    is_active          BOOLEAN NOT NULL,
    -- the org owner — full access that can't be stripped (transfer only). Exactly one per org (index below)
    is_org_owner       BOOLEAN NOT NULL,
    -- the admin who created this account (audit); NULL only for the bootstrap owner
    created_by_user_id UUID REFERENCES "user" (user_id),
    -- when the account was created (stored UTC)
    created_at         TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted         BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at         TIMESTAMPTZ,
    -- one email = one account across the whole system
    UNIQUE (email)
);

CREATE INDEX ON "user" (organization_id);
-- exactly one owner per org (only live users count)
CREATE UNIQUE INDEX user_one_owner_per_org ON "user" (organization_id) WHERE is_org_owner AND NOT is_deleted;


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
    -- ISO country code, e.g. 'US'
    country          VARCHAR(128),
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
    -- soft-delete flag; TRUE = store removed but kept for history
    is_deleted       BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ
);
CREATE INDEX ON store (organization_id);
-- at most one default store per org (only live stores count)
CREATE UNIQUE INDEX store_one_default_per_org ON store (organization_id) WHERE is_default AND NOT is_deleted;
