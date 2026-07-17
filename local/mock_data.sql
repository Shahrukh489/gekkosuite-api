-- ============================================================================
-- mock_data.sql — a few rows for local dev. Run AFTER the schema migration.
-- The DB has no defaults/generation, so every value (ids, timestamps, flags) is
-- supplied explicitly here, exactly as the app would on insert.
-- Fixed UUIDs are used so the data is stable/repeatable across loads.
-- ============================================================================

-- One organization.
INSERT INTO organization (organization_id, name, description, billing_status, created_at, is_deleted, deleted_at) VALUES
    ('00000000-0000-0000-0000-000000000001', 'Acme Inc', 'Coffee chain', 'ACTIVE', '2026-01-05T12:00:00Z', FALSE, NULL);

-- Users: the owner, plus two staff. created_by_user_id points at the owner for the staff.
INSERT INTO "user" (user_id, organization_id, email, password, name, phone, is_active, is_org_owner, created_by_user_id, created_at, is_deleted, deleted_at) VALUES
    ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'maria@acme.com', '$argon2id$mock', 'Maria',  '+1 555 0100', TRUE, TRUE,  NULL,                                     '2026-01-05T12:00:00Z', FALSE, NULL),
    ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'sara@acme.com',  '$argon2id$mock', 'Sara',   '+1 555 0101', TRUE, FALSE, '10000000-0000-0000-0000-000000000001', '2026-01-06T09:00:00Z', FALSE, NULL),
    ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'marcus@acme.com','$argon2id$mock', 'Marcus', '+1 555 0102', TRUE, FALSE, '10000000-0000-0000-0000-000000000001', '2026-01-06T09:05:00Z', FALSE, NULL);

-- Two stores; Downtown is the org's default store.
INSERT INTO store (store_id, organization_id, name, type, description, address, city, state, postal_code, country, currency, phone, email, is_default, created_at, is_deleted, deleted_at) VALUES
    ('20000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'Downtown', 'PHYSICAL', 'Flagship',    '1 Main St',  'Austin', 'TX', '78701', 'US', 'USD', '+1 555 0200', 'downtown@acme.com', TRUE,  '2026-01-05T12:00:00Z', FALSE, NULL),
    ('20000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Online',   'ONLINE',   'Web storefront', NULL,        NULL,     NULL, NULL,    'US', 'USD', NULL,          'shop@acme.com',     FALSE, '2026-01-07T10:00:00Z', FALSE, NULL);
