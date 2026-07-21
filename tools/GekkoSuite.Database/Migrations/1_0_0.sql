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
    name             TEXT NOT NULL,
    -- optional free-text note about the business
    description      TEXT,
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
    user_id            UUID PRIMARY KEY,
    -- home org (set once, immutable) — the tenant boundary
    organization_id    UUID NOT NULL REFERENCES organization (organization_id),
    email              TEXT NOT NULL,
    -- password hash — slow salted KDF (argon2id/bcrypt); NEVER plaintext (see auth.md)
    password           TEXT NOT NULL,
    name               TEXT NOT NULL,
    phone              TEXT,
    -- account kill switch; false = all memberships suspended (revoke, not delete)
    is_active          BOOLEAN NOT NULL,
    -- the org owner — full access that can't be stripped (transfer only). Exactly one per org (index below).
    is_org_owner       BOOLEAN NOT NULL,
    created_by_user_id UUID REFERENCES "user" (user_id),
    created_at         TIMESTAMPTZ NOT NULL,
    is_deleted         BOOLEAN NOT NULL,
    deleted_at         TIMESTAMPTZ,
    UNIQUE (email)
);
CREATE INDEX ON "user" (organization_id);
-- exactly one owner per org (only live users count)
CREATE UNIQUE INDEX user_one_owner_per_org ON "user" (organization_id) WHERE is_org_owner AND NOT is_deleted;


-- A store: the business unit where selling happens. Owned by one org.
CREATE TABLE store (
    store_id         UUID PRIMARY KEY,
    organization_id  UUID NOT NULL REFERENCES organization (organization_id),
    name             TEXT NOT NULL,
    type             store_type NOT NULL,
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
    is_default       BOOLEAN NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL,
    is_deleted       BOOLEAN NOT NULL,
    deleted_at       TIMESTAMPTZ
);
CREATE INDEX ON store (organization_id);
-- at most one default store per org (only live stores count)
CREATE UNIQUE INDEX store_one_default_per_org ON store (organization_id) WHERE is_default AND NOT is_deleted;


-- A role: a named bundle of permissions. Either a managed role we ship, or an org's own custom role.
CREATE TABLE role (
    role_id         UUID PRIMARY KEY,
    name            TEXT NOT NULL,
    description     TEXT,
    -- TRUE = system role we ship (org-wide); FALSE = an org's own custom role
    is_managed      BOOLEAN NOT NULL,
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
    permission_id UUID PRIMARY KEY,
    -- the thing acted on, e.g. product, order, role
    resource      TEXT NOT NULL,
    -- what may be done to it, e.g. read, create, refund, assign
    action        TEXT NOT NULL,
    description   TEXT,
    -- TRUE = for org roles only, FALSE = can be added to both STORE and ORGANIZATION roles
    is_elevated   BOOLEAN NOT NULL,
    -- resource + action is the permission's natural key, e.g. (product, read)
    UNIQUE (resource, action)
);

-- role_permission: which permissions a role grants (many-to-many). Guarded on write (see auth.md).
CREATE TABLE role_permission (
    role_id       UUID NOT NULL REFERENCES role (role_id),
    permission_id UUID NOT NULL REFERENCES permission (permission_id),
    PRIMARY KEY (role_id, permission_id)
);

-- A membership: a place a user belongs — the whole ORGANIZATION, or one STORE. Sets their reach there.
CREATE TABLE membership (
    membership_id   UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES "user" (user_id),
    -- ORGANIZATION | STORE: the kind of place (see CHECK for the store_id rule)
    scope           scope NOT NULL,
    -- the tenant this membership is in (the boundary every request is checked against)
    organization_id UUID NOT NULL REFERENCES organization (organization_id),
    -- the store, for a STORE membership; NULL for an ORGANIZATION membership
    store_id        UUID REFERENCES store (store_id),
    -- suspend switch for this one place; false = access off here but kept
    is_active       BOOLEAN NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    is_deleted      BOOLEAN NOT NULL,
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
    membership_id UUID NOT NULL REFERENCES membership (membership_id),
    role_id       UUID NOT NULL REFERENCES role (role_id),
    assigned_at   TIMESTAMPTZ NOT NULL,
    -- who granted it (the admin) — for audit; NULL if system-seeded
    assigned_by_user_id UUID REFERENCES "user" (user_id),
    -- optional expiry; NULL = never expires
    expires_at    TIMESTAMPTZ,
    PRIMARY KEY (membership_id, role_id)
);

-- Fields that only apply to a STORE membership (1:1 with membership). Keeps store-only columns off
-- the shared table; org memberships simply have no row here.
CREATE TABLE store_membership_detail (
    membership_id UUID PRIMARY KEY REFERENCES membership (membership_id),
    -- register PIN, stored as a salted KDF hash (never plaintext) — see auth.md
    store_pin     TEXT
);

-- Fields that only apply to an ORGANIZATION membership (1:1 with membership). Empty for now.
CREATE TABLE organization_membership_detail (
    membership_id UUID PRIMARY KEY REFERENCES membership (membership_id)
);



