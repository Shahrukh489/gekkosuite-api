-- ============================================================================
-- mock_data.sql — a few rows for local dev. Run AFTER the schema migration.
-- The DB has no defaults/generation, so every value (ids, timestamps, flags) is
-- supplied explicitly here, exactly as the app would on insert.
-- Fixed UUIDs are used so the data is stable/repeatable across loads.
-- ============================================================================

-- Two organizations: Acme Inc (the main mock tenant) and a small standalone Dev Org (a single-user
-- login fixture, plus handy for testing cross-org isolation since it's a separate tenant).
INSERT INTO organization (organization_id, name, description, billing_status, created_at, is_deleted, deleted_at) VALUES
    ('00000000-0000-0000-0000-000000000001', 'Acme Inc', 'Coffee chain',                 'ACTIVE', '2026-01-05T12:00:00Z', FALSE, NULL),
    ('11111111-1111-1111-1111-111111111111', 'Dev Org',  'Seeded for local login testing', 'ACTIVE', '2026-01-01T00:00:00Z', FALSE, NULL);

-- Users: Acme's owner (Maria) plus two staff, and Dev Org's own single owner/user. created_by_user_id
-- points at the owner for Acme's staff; NULL where system-seeded (Maria, the Dev Org user).
-- Passwords are real, checkable Argon2id hashes (same params as AuthService.HashPassword: memory=19 MiB,
-- iterations=2, parallelism=1, 32-byte output), stored as "{base64Salt}:{base64Hash}" so POST /auth/login
-- actually works against these rows — the old '$argon2id$mock' placeholder never matched any password
-- (VerifyPassword expects exactly one ':' in the stored value, so it always failed closed).
-- Acme's three share one password for convenience: Password123! Dev Org's user: DevPassword123!
INSERT INTO "user" (user_id, organization_id, email, password, name, phone, is_active, is_org_owner, created_by_user_id, created_at, is_deleted, deleted_at) VALUES
    ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'maria@acme.com',      'Pvi7LJ2oxQRczv/hO3b29Q==:4c5IvuqMIVUxSw4ZP2obc77lOL0qoOCRBcCm81uRlrM=', 'Maria',    '+1 555 0100', TRUE, TRUE,  NULL,                                     '2026-01-05T12:00:00Z', FALSE, NULL),
    ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'sara@acme.com',       'PQUJ8QnHyQZInhTvRIXO0w==:aCdPwONtNFWH1wiuq7cika4aYDmNewJMWFyVe+dGmc8=', 'Sara',     '+1 555 0101', TRUE, FALSE, '10000000-0000-0000-0000-000000000001', '2026-01-06T09:00:00Z', FALSE, NULL),
    ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'marcus@acme.com',     'bmQ2G6/ojp92m1cBFOEkcw==:MzCFYs9YtTb+iv4YoAR1uuaW17EXRrBPAHWC9gVTg3Y=', 'Marcus',   '+1 555 0102', TRUE, FALSE, '10000000-0000-0000-0000-000000000001', '2026-01-06T09:05:00Z', FALSE, NULL),
    ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'dev@gekkosuite.local','fzR6hQsI5Vr6eeTO8qCZLw==:duYlawAZ54cuHXLRDMu4vAUlMu0WDO/+UfC7LobIwK0=', 'Dev User', NULL,          TRUE, TRUE,  NULL,                                     '2026-01-01T00:00:00Z', FALSE, NULL);

-- Two stores; Downtown is the org's default store.
INSERT INTO store (store_id, organization_id, name, type, description, address, city, state, postal_code, country, currency, phone, email, is_default, created_at, is_deleted, deleted_at) VALUES
    ('20000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'Downtown', 'PHYSICAL', 'Flagship',    '1 Main St',  'Austin', 'TX', '78701', 'US', 'USD', '+1 555 0200', 'downtown@acme.com', TRUE,  '2026-01-05T12:00:00Z', FALSE, NULL),
    ('20000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Online',   'ONLINE',   'Web storefront', NULL,        NULL,     NULL, NULL,    'US', 'USD', NULL,          'shop@acme.com',     FALSE, '2026-01-07T10:00:00Z', FALSE, NULL);



-- -- Permission catalog. resource:action pairs are drawn directly from docs/api.md's endpoint specs and
-- -- the worked examples in auth.md/ui.md/plans.md — nothing invented beyond what the docs already name.
-- INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES
--     ('30000000-0000-0000-0000-000000000001', 'organization', 'read',       'Read the organization record and billing state', FALSE),
--     ('30000000-0000-0000-0000-000000000002', 'user',         'create',     'Create a user (login only, no membership)',       TRUE),
--     ('30000000-0000-0000-0000-000000000003', 'user',         'read',       'List/view users',                                 FALSE),
--     ('30000000-0000-0000-0000-000000000004', 'user',         'edit',       'Edit a user''s profile fields',                   TRUE),
--     ('30000000-0000-0000-0000-000000000005', 'user',         'deactivate', 'Turn a user account off',                         TRUE),
--     ('30000000-0000-0000-0000-000000000006', 'user',         'activate',   'Turn a user account back on',                     TRUE),
--     ('30000000-0000-0000-0000-000000000007', 'membership',   'assign',     'Grant a membership + role',                       TRUE),
--     ('30000000-0000-0000-0000-000000000008', 'membership',   'revoke',     'Remove a membership entirely',                    TRUE),
--     ('30000000-0000-0000-0000-000000000009', 'membership',   'deactivate', 'Suspend a membership',                            FALSE),
--     ('30000000-0000-0000-0000-00000000000a', 'membership',   'activate',   'Restore a suspended membership',                  FALSE),
--     ('30000000-0000-0000-0000-00000000000b', 'role',         'read',       'List roles / view a role''s permissions',         FALSE),
--     ('30000000-0000-0000-0000-00000000000c', 'store',        'read',       'View store record(s)',                            FALSE),
--     ('30000000-0000-0000-0000-00000000000d', 'store',        'create',     'Create a store',                                  TRUE),
--     ('30000000-0000-0000-0000-00000000000e', 'store',        'edit',       'Edit a store''s details',                         FALSE),
--     ('30000000-0000-0000-0000-00000000000f', 'store',        'delete',     'Soft-delete a store',                             TRUE),
--     ('30000000-0000-0000-0000-000000000010', 'product',      'read',       'View a store''s products',                        FALSE),
--     ('30000000-0000-0000-0000-000000000011', 'product',      'create',     'Add a product to a store',                        FALSE),
--     ('30000000-0000-0000-0000-000000000012', 'product',      'edit',       'Edit a store product (incl. price)',              FALSE),
--     ('30000000-0000-0000-0000-000000000013', 'customer',     'read',       'View a store''s customers',                       FALSE),
--     ('30000000-0000-0000-0000-000000000014', 'customer',     'create',     'Add a customer at a store',                       FALSE),
--     ('30000000-0000-0000-0000-000000000015', 'sale',         'create',     'Ring up a sale',                                  FALSE),
--     ('30000000-0000-0000-0000-000000000016', 'order',        'read',       'View a store''s sales orders',                    FALSE),
--     ('30000000-0000-0000-0000-000000000017', 'order',        'refund',     'Process a return/refund',                         FALSE);

-- -- Managed roles: Cashier and Manager (STORE scope), Org Admin (ORGANIZATION scope). All three are
-- -- managed (is_managed = TRUE), so organization_id is NULL per the CHECK.
-- INSERT INTO role (role_id, name, description, is_managed, organization_id, scope) VALUES
--     ('40000000-0000-0000-0000-000000000001', 'Cashier',   'Rings up sales at a store',  TRUE, NULL, 'STORE'),
--     ('40000000-0000-0000-0000-000000000002', 'Manager',   'Runs a store',               TRUE, NULL, 'STORE'),
--     ('40000000-0000-0000-0000-000000000003', 'Org Admin', 'Full administrative access', TRUE, NULL, 'ORGANIZATION');

-- -- Cashier: store:read, product:read, sale:create, order:read, customer:read — everything needed to
-- -- ring up a sale and look up a returning customer, nothing that edits or refunds (see ui.md's Sara).
-- INSERT INTO role_permission (role_id, permission_id) VALUES
--     ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-00000000000c'), -- store:read
--     ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000010'), -- product:read
--     ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000015'), -- sale:create
--     ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000016'), -- order:read
--     ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000013'); -- customer:read

-- -- Manager: everything Cashier has, plus product:edit and order:refund (see ui.md's Marcus).
-- INSERT INTO role_permission (role_id, permission_id) VALUES
--     ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-00000000000c'), -- store:read
--     ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000010'), -- product:read
--     ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000015'), -- sale:create
--     ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000016'), -- order:read
--     ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000013'), -- customer:read
--     ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000012'), -- product:edit
--     ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000017'); -- order:refund

-- -- Org Admin: every permission in the catalog. An ORGANIZATION membership reaches every store in the
-- -- org (auth.md), and the role assigned to that membership is what's actually checked on a store
-- -- action — so Org Admin's role must itself carry the full store-level set too, not just the org-only
-- -- ones, for "an org admin can sell/edit/refund in any store" (ui.md) to hold under auth.md's query.
-- INSERT INTO role_permission (role_id, permission_id)
-- SELECT '40000000-0000-0000-0000-000000000003', permission_id FROM permission;
