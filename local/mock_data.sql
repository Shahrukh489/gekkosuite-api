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
