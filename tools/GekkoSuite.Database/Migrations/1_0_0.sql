-- ============================================================================
-- V1.0.0 — initial schema (organizations, users, stores, billing/offerings)
-- Mirrors docs/database.md. Tables are ordered so every FK points at a table
-- that already exists — organization is referenced by user/store/subscription,
-- and never references them back (the owner/default-store pointers live on the
-- child as is_org_owner / is_default flags, which avoids a circular FK).
-- ============================================================================

-- The scope a membership/role/feature operates at.
CREATE TYPE scope AS ENUM ('ORGANIZATION', 'STORE');

-- How a store sells: a storefront kind.
CREATE TYPE store_type AS ENUM ('ONLINE', 'PHYSICAL');

-- The org's overall billing status (organization.billing_status). Purely about paying — no TRIALING
-- (that's a per-subscription lifecycle state). See docs/database.md and auth.md's billing gate.
CREATE TYPE billing_status AS ENUM ('ACTIVE', 'PAST_DUE', 'UNPAID', 'CANCELED');

-- The organization: the business and the tenant (unit of isolation). Owns stores, users, and settings.
CREATE TABLE organization (
    -- tenant id; 
    organization_id  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- the business's display name (required)
    name             TEXT NOT NULL,
    -- optional free-text note about the business
    description      TEXT,
    -- the org's overall payment state for its ONE itemized bill (all subscriptions). 
    billing_status   billing_status NOT NULL,
    -- when the org was onboarded (stored UTC)
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- soft-delete flag; TRUE = org removed but kept for history
    is_deleted       BOOLEAN NOT NULL DEFAULT FALSE,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at       TIMESTAMPTZ
);

-- A user: one login per person. Where they can act comes from their memberships (see auth.md).
CREATE TABLE "user" (
    user_id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- home org (set once, immutable) — the tenant boundary
    organization_id    UUID NOT NULL REFERENCES organization (organization_id),
    email              TEXT NOT NULL,
    -- password hash — slow salted KDF (argon2id/bcrypt); NEVER plaintext (see auth.md)
    password           TEXT NOT NULL,
    name               TEXT NOT NULL,
    phone              TEXT,
    -- account kill switch; false = all memberships suspended (revoke, not delete)
    is_active          BOOLEAN NOT NULL DEFAULT TRUE,
    -- the org owner — full access that can't be stripped (transfer only). Exactly one per org (index below).
    is_org_owner       BOOLEAN NOT NULL DEFAULT FALSE,
    created_by_user_id UUID REFERENCES "user" (user_id),
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted         BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at         TIMESTAMPTZ,
    UNIQUE (email)
);
CREATE INDEX ON "user" (organization_id);
-- exactly one owner per org (only live users count)
CREATE UNIQUE INDEX user_one_owner_per_org ON "user" (organization_id) WHERE is_org_owner AND NOT is_deleted;


-- A store: the business unit where selling happens. Owned by one org.
CREATE TABLE store (
    store_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id  UUID NOT NULL REFERENCES organization (organization_id),
    name             TEXT NOT NULL,
    type             store_type NOT NULL DEFAULT 'PHYSICAL',
    description      TEXT,
    -- physical location (all NULL for an ONLINE store)
    address          TEXT,
    city             TEXT,
    state            TEXT,
    postal_code      TEXT,
    country          TEXT,
    currency         TEXT,
    phone            TEXT,
    email            TEXT,
    -- the org's default store — where single-store orgs (and org users) land. Exactly one per org (index below).
    is_default       BOOLEAN NOT NULL DEFAULT FALSE,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted       BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at       TIMESTAMPTZ
);
CREATE INDEX ON store (organization_id);
-- at most one default store per org (only live stores count)
CREATE UNIQUE INDEX store_one_default_per_org ON store (organization_id) WHERE is_default AND NOT is_deleted;
