-- ============================================================================
-- V1.0.1 — RBAC schema (roles, permissions, memberships) + the managed role
-- catalog. Mirrors docs/database.md's role/permission/membership tables and
-- the four cross-table trigger invariants documented there but not given as
-- SQL. Seeds Cashier, Manager, and Org Admin (the three roles every worked
-- example across the docs settles on) plus every permission those docs name
-- by resource:action.
--
-- Same convention as 1_0_0.sql: NO column defaults, NO server-side value
-- generation. Every value — ids, timestamps, flags — is supplied explicitly,
-- including in the seed INSERTs below (this is reference/system data every
-- environment needs, unlike local/mock_data.sql's throwaway tenant fixtures).
-- ============================================================================

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


-- ============================================================================
-- Triggers — cross-table invariants a CHECK can't express because they read
-- sibling rows or other tables (see database.md's "Optional Triggers" table).
-- ============================================================================

-- An elevated permission (is_elevated = TRUE) may only sit on an ORGANIZATION-scope role, so a store
-- role can never carry a company-level power like user:create.
CREATE FUNCTION role_permission_elevated_guard_fn() RETURNS TRIGGER AS $$
DECLARE
    v_is_elevated BOOLEAN;
    v_role_scope  scope;
BEGIN
    SELECT is_elevated INTO v_is_elevated FROM permission WHERE permission_id = NEW.permission_id;
    SELECT scope INTO v_role_scope FROM role WHERE role_id = NEW.role_id;

    IF v_is_elevated AND v_role_scope <> 'ORGANIZATION' THEN
        RAISE EXCEPTION 'Elevated permission (%) cannot be granted to a % role', NEW.permission_id, v_role_scope;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER role_permission_elevated_guard
    BEFORE INSERT OR UPDATE ON role_permission
    FOR EACH ROW EXECUTE FUNCTION role_permission_elevated_guard_fn();

-- A role's scope must match the membership it's granted to, so a STORE seat can never end up with an
-- ORGANIZATION role's wider reach.
CREATE FUNCTION membership_assignment_type_guard_fn() RETURNS TRIGGER AS $$
DECLARE
    v_role_scope       scope;
    v_membership_scope scope;
BEGIN
    SELECT scope INTO v_role_scope FROM role WHERE role_id = NEW.role_id;
    SELECT scope INTO v_membership_scope FROM membership WHERE membership_id = NEW.membership_id;

    IF v_role_scope <> v_membership_scope THEN
        RAISE EXCEPTION 'Role scope (%) does not match membership scope (%)', v_role_scope, v_membership_scope;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER membership_assignment_type_guard
    BEFORE INSERT OR UPDATE ON membership_assignment
    FOR EACH ROW EXECUTE FUNCTION membership_assignment_type_guard_fn();

-- A user can only be given a membership in their own home organization.
CREATE FUNCTION user_organization_membership_guard_fn() RETURNS TRIGGER AS $$
DECLARE
    v_user_org UUID;
BEGIN
    SELECT organization_id INTO v_user_org FROM "user" WHERE user_id = NEW.user_id;

    IF v_user_org <> NEW.organization_id THEN
        RAISE EXCEPTION 'User (%) belongs to organization (%), cannot be given a membership in (%)', NEW.user_id, v_user_org, NEW.organization_id;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER user_organization_membership
    BEFORE INSERT OR UPDATE ON membership
    FOR EACH ROW EXECUTE FUNCTION user_organization_membership_guard_fn();

-- A user is either an ORGANIZATION member or a STORE member, never both (mixing the two would let a
-- store employee also reach org-wide data). Only checks against other LIVE memberships.
CREATE FUNCTION membership_single_kind_guard_fn() RETURNS TRIGGER AS $$
DECLARE
    v_conflicting_count INTEGER;
BEGIN
    IF NEW.is_deleted THEN
        RETURN NEW;
    END IF;

    SELECT COUNT(*) INTO v_conflicting_count
    FROM membership
    WHERE user_id = NEW.user_id
      AND membership_id <> NEW.membership_id
      AND NOT is_deleted
      AND scope <> NEW.scope;

    IF v_conflicting_count > 0 THEN
        RAISE EXCEPTION 'User (%) already holds a membership of the other scope; a user is either an ORGANIZATION or a STORE member, never both', NEW.user_id;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER membership_single_kind_guard
    BEFORE INSERT OR UPDATE ON membership
    FOR EACH ROW EXECUTE FUNCTION membership_single_kind_guard_fn();


-- ============================================================================
-- Seed — the permission catalog and the three managed roles every worked
-- example in the docs settles on (Cashier, Manager, Org Admin). Reference
-- data every environment needs, so it lives in the migration itself, not a
-- local-only fixture like local/mock_data.sql.
-- ============================================================================

-- Permission catalog. resource:action pairs are drawn directly from docs/api.md's endpoint specs and
-- the worked examples in auth.md/ui.md/plans.md — nothing invented beyond what the docs already name.
INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES
    ('30000000-0000-0000-0000-000000000001', 'organization', 'read',       'Read the organization record and billing state', FALSE),
    ('30000000-0000-0000-0000-000000000002', 'user',         'create',     'Create a user (login only, no membership)',       TRUE),
    ('30000000-0000-0000-0000-000000000003', 'user',         'read',       'List/view users',                                 FALSE),
    ('30000000-0000-0000-0000-000000000004', 'user',         'edit',       'Edit a user''s profile fields',                   TRUE),
    ('30000000-0000-0000-0000-000000000005', 'user',         'deactivate', 'Turn a user account off',                         TRUE),
    ('30000000-0000-0000-0000-000000000006', 'user',         'activate',   'Turn a user account back on',                     TRUE),
    ('30000000-0000-0000-0000-000000000007', 'membership',   'assign',     'Grant a membership + role',                       TRUE),
    ('30000000-0000-0000-0000-000000000008', 'membership',   'revoke',     'Remove a membership entirely',                    TRUE),
    ('30000000-0000-0000-0000-000000000009', 'membership',   'deactivate', 'Suspend a membership',                            FALSE),
    ('30000000-0000-0000-0000-00000000000a', 'membership',   'activate',   'Restore a suspended membership',                  FALSE),
    ('30000000-0000-0000-0000-00000000000b', 'role',         'read',       'List roles / view a role''s permissions',         FALSE),
    ('30000000-0000-0000-0000-00000000000c', 'store',        'read',       'View store record(s)',                            FALSE),
    ('30000000-0000-0000-0000-00000000000d', 'store',        'create',     'Create a store',                                  TRUE),
    ('30000000-0000-0000-0000-00000000000e', 'store',        'edit',       'Edit a store''s details',                         FALSE),
    ('30000000-0000-0000-0000-00000000000f', 'store',        'delete',     'Soft-delete a store',                             TRUE),
    ('30000000-0000-0000-0000-000000000010', 'product',      'read',       'View a store''s products',                        FALSE),
    ('30000000-0000-0000-0000-000000000011', 'product',      'create',     'Add a product to a store',                        FALSE),
    ('30000000-0000-0000-0000-000000000012', 'product',      'edit',       'Edit a store product (incl. price)',              FALSE),
    ('30000000-0000-0000-0000-000000000013', 'customer',     'read',       'View a store''s customers',                       FALSE),
    ('30000000-0000-0000-0000-000000000014', 'customer',     'create',     'Add a customer at a store',                       FALSE),
    ('30000000-0000-0000-0000-000000000015', 'sale',         'create',     'Ring up a sale',                                  FALSE),
    ('30000000-0000-0000-0000-000000000016', 'order',        'read',       'View a store''s sales orders',                    FALSE),
    ('30000000-0000-0000-0000-000000000017', 'order',        'refund',     'Process a return/refund',                         FALSE);

-- Managed roles: Cashier and Manager (STORE scope), Org Admin (ORGANIZATION scope). All three are
-- managed (is_managed = TRUE), so organization_id is NULL per the CHECK.
INSERT INTO role (role_id, name, description, is_managed, organization_id, scope) VALUES
    ('40000000-0000-0000-0000-000000000001', 'Cashier',   'Rings up sales at a store',  TRUE, NULL, 'STORE'),
    ('40000000-0000-0000-0000-000000000002', 'Manager',   'Runs a store',               TRUE, NULL, 'STORE'),
    ('40000000-0000-0000-0000-000000000003', 'Org Admin', 'Full administrative access', TRUE, NULL, 'ORGANIZATION');

-- Cashier: store:read, product:read, sale:create, order:read, customer:read — everything needed to
-- ring up a sale and look up a returning customer, nothing that edits or refunds (see ui.md's Sara).
INSERT INTO role_permission (role_id, permission_id) VALUES
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-00000000000c'), -- store:read
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000010'), -- product:read
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000015'), -- sale:create
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000016'), -- order:read
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000013'); -- customer:read

-- Manager: everything Cashier has, plus product:edit and order:refund (see ui.md's Marcus).
INSERT INTO role_permission (role_id, permission_id) VALUES
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-00000000000c'), -- store:read
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000010'), -- product:read
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000015'), -- sale:create
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000016'), -- order:read
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000013'), -- customer:read
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000012'), -- product:edit
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000017'); -- order:refund

-- Org Admin: every permission in the catalog. An ORGANIZATION membership reaches every store in the
-- org (auth.md), and the role assigned to that membership is what's actually checked on a store
-- action — so Org Admin's role must itself carry the full store-level set too, not just the org-only
-- ones, for "an org admin can sell/edit/refund in any store" (ui.md) to hold under auth.md's query.
INSERT INTO role_permission (role_id, permission_id)
SELECT '40000000-0000-0000-0000-000000000003', permission_id FROM permission;
