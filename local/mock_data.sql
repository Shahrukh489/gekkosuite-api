INSERT INTO organization
    (organization_id, name, description, billing_status, created_at, updated_at, is_deleted, deleted_at)
VALUES
    ('00000000-0000-0000-0000-000000000001', 'Acme Inc', 'Coffee chain',                   'ACTIVE', '2026-01-05T12:00:00Z', '2026-01-05T12:00:00Z', FALSE, NULL),
    ('11111111-1111-1111-1111-111111111111', 'Dev Org',  'Seeded for local login testing', 'ACTIVE', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', FALSE, NULL);


INSERT INTO "user"
    (user_id, organization_id, email, password, first_name, last_name, phone, is_active, is_org_owner, created_by_user_id, created_at, updated_at, is_deleted, deleted_at)
VALUES
    ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'maria@acme.com',       'Pvi7LJ2oxQRczv/hO3b29Q==:4c5IvuqMIVUxSw4ZP2obc77lOL0qoOCRBcCm81uRlrM=', 'Maria',  'Owner',   '+1 555 0100', TRUE, TRUE,  NULL,                                     '2026-01-05T12:00:00Z', '2026-01-05T12:00:00Z', FALSE, NULL),
    ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'sara@acme.com',        'PQUJ8QnHyQZInhTvRIXO0w==:aCdPwONtNFWH1wiuq7cika4aYDmNewJMWFyVe+dGmc8=', 'Sara',   'Cashier', '+1 555 0101', TRUE, FALSE, '10000000-0000-0000-0000-000000000001', '2026-01-06T09:00:00Z', '2026-01-06T09:00:00Z', FALSE, NULL),
    ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'marcus@acme.com',      'bmQ2G6/ojp92m1cBFOEkcw==:MzCFYs9YtTb+iv4YoAR1uuaW17EXRrBPAHWC9gVTg3Y=', 'Marcus', 'Manager', '+1 555 0102', TRUE, FALSE, '10000000-0000-0000-0000-000000000001', '2026-01-06T09:05:00Z', '2026-01-06T09:05:00Z', FALSE, NULL),
    ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'dev@gekkosuite.local', 'fzR6hQsI5Vr6eeTO8qCZLw==:duYlawAZ54cuHXLRDMu4vAUlMu0WDO/+UfC7LobIwK0=', 'Dev',    'User',    NULL,          TRUE, TRUE,  NULL,                                     '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', FALSE, NULL);


INSERT INTO store
    (store_id, organization_id, name, type, description, address, city, state, postal_code, country, currency, phone, email, is_default, created_at, updated_at, is_deleted, deleted_at)
VALUES
    ('20000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'Downtown', 'PHYSICAL', 'Flagship',       '1 Main St', 'Austin', 'TX', '78701', 'US', 'USD', '+1 555 0200', 'downtown@acme.com', TRUE,  '2026-01-05T12:00:00Z', '2026-01-05T12:00:00Z', FALSE, NULL),
    ('20000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Online',   'ONLINE',   'Web storefront', NULL,        NULL,     NULL, NULL,    'US', 'USD', NULL,          'shop@acme.com',     FALSE, '2026-01-07T10:00:00Z', '2026-01-07T10:00:00Z', FALSE, NULL);


INSERT INTO permission
    (permission_id, resource, action, description, is_elevated)
VALUES
    ('30000000-0000-0000-0000-000000000001', 'organization', 'read',   'Read the organization record and billing state', FALSE),
    ('30000000-0000-0000-0000-000000000002', 'user',         'create', 'Create a user (login only, no membership)',       TRUE);


INSERT INTO role
    (role_id, name, description, is_managed, organization_id, scope, created_at, updated_at)
VALUES
    ('40000000-0000-0000-0000-000000000003', 'Org Admin', 'Full administrative access', TRUE, NULL, 'ORGANIZATION', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');


INSERT INTO role_permission
    (role_id, permission_id)
VALUES
    ('40000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000001'),
    ('40000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000002');


INSERT INTO membership
    (membership_id, user_id, scope, organization_id, store_id, is_active, created_at, updated_at, is_deleted, deleted_at)
VALUES
    ('50000000-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', 'ORGANIZATION', '11111111-1111-1111-1111-111111111111', NULL, TRUE, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', FALSE, NULL);


INSERT INTO membership_assignment
    (membership_id, role_id, assigned_at, assigned_by_user_id, expires_at)
VALUES
    ('50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', '2026-01-01T00:00:00Z', '22222222-2222-2222-2222-222222222222', NULL);
