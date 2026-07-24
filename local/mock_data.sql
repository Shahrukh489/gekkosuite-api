INSERT INTO organization (organization_id, name, description, billing_status, created_at, updated_at, is_deleted, deleted_at) VALUES ('11111111-1111-1111-1111-111111111111', 'Dev Org', 'Seeded for local testing', 'ACTIVE', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', FALSE, NULL);


INSERT INTO user_account (user_id, organization_id, email, password, first_name, last_name, phone, is_active, is_org_owner, created_by_user_id, created_at, updated_at, is_deleted, deleted_at) VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'admin@gekkosuite.com', 'fzR6hQsI5Vr6eeTO8qCZLw==:duYlawAZ54cuHXLRDMu4vAUlMu0WDO/+UfC7LobIwK0=', 'Dev', 'User', NULL, TRUE, TRUE, NULL, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', FALSE, NULL);
INSERT INTO user_account (user_id, organization_id, email, password, first_name, last_name, phone, is_active, is_org_owner, created_by_user_id, created_at, updated_at, is_deleted, deleted_at) VALUES ('10000000-0000-0000-0000-000000000003', '11111111-1111-1111-1111-111111111111', 'marcus@gekkosuite.com', 'bmQ2G6/ojp92m1cBFOEkcw==:MzCFYs9YtTb+iv4YoAR1uuaW17EXRrBPAHWC9gVTg3Y=', 'Marcus', 'Manager', '+1 555 0102', TRUE, FALSE, '22222222-2222-2222-2222-222222222222', '2026-01-06T09:05:00Z', '2026-01-06T09:05:00Z', FALSE, NULL);


INSERT INTO store (store_id, organization_id, name, type, description, address, city, state, postal_code, country, currency, phone, email, is_default, created_at, updated_at, is_deleted, deleted_at) VALUES ('20000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'Downtown', 'PHYSICAL', 'Flagship', '1 Main St', 'Austin', 'TX', '78701', 'US', 'USD', '+1 555 0200', 'downtown@gekkosuite.com', TRUE, '2026-01-05T12:00:00Z', '2026-01-05T12:00:00Z', FALSE, NULL);
INSERT INTO store (store_id, organization_id, name, type, description, address, city, state, postal_code, country, currency, phone, email, is_default, created_at, updated_at, is_deleted, deleted_at) VALUES ('20000000-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'Online', 'ONLINE', 'Web storefront', NULL, NULL, NULL, NULL, 'US', 'USD', NULL, 'shop@gekkosuite.com', FALSE, '2026-01-07T10:00:00Z', '2026-01-07T10:00:00Z', FALSE, NULL);


INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES ('30000000-0000-0000-0000-000000000001', 'organization', 'read', 'Read the organization record and billing state', FALSE);
INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES ('30000000-0000-0000-0000-000000000002', 'user', 'create', 'Create a user (login only, no membership)', TRUE);
INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES ('30000000-0000-0000-0000-000000000003', 'sale', 'create', 'Ring up a sale', FALSE);
INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES ('30000000-0000-0000-0000-000000000004', 'product', 'read', 'View products', FALSE);
INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES ('30000000-0000-0000-0000-000000000005', 'product', 'edit', 'Edit products and stock', FALSE);
INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES ('30000000-0000-0000-0000-000000000006', 'order', 'refund', 'Refund an order', FALSE);
INSERT INTO permission (permission_id, resource, action, description, is_elevated) VALUES ('30000000-0000-0000-0000-000000000007', 'store', 'read', 'View a store record and its features', FALSE);


INSERT INTO role (role_id, name, description, is_managed, organization_id, scope, created_at, updated_at) VALUES ('40000000-0000-0000-0000-000000000003', 'Org Admin', 'Full administrative access', TRUE, NULL, 'ORGANIZATION', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
INSERT INTO role (role_id, name, description, is_managed, organization_id, scope, created_at, updated_at) VALUES ('40000000-0000-0000-0000-000000000004', 'Cashier', 'Ring up sales', TRUE, NULL, 'STORE', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
INSERT INTO role (role_id, name, description, is_managed, organization_id, scope, created_at, updated_at) VALUES ('40000000-0000-0000-0000-000000000005', 'Manager', 'Run a store', TRUE, NULL, 'STORE', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');


INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000001');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000002');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000007');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000003');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000004');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000007');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000004');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000005');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000006');
INSERT INTO role_permission (role_id, permission_id) VALUES ('40000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000007');


INSERT INTO membership (membership_id, user_id, scope, organization_id, store_id, is_active, created_at, updated_at, is_deleted, deleted_at) VALUES ('50000000-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', 'ORGANIZATION', '11111111-1111-1111-1111-111111111111', NULL, TRUE, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', FALSE, NULL);
INSERT INTO membership (membership_id, user_id, scope, organization_id, store_id, is_active, created_at, updated_at, is_deleted, deleted_at) VALUES ('50000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000003', 'STORE', '11111111-1111-1111-1111-111111111111', '20000000-0000-0000-0000-000000000001', TRUE, '2026-01-06T09:05:00Z', '2026-01-06T09:05:00Z', FALSE, NULL);
INSERT INTO membership (membership_id, user_id, scope, organization_id, store_id, is_active, created_at, updated_at, is_deleted, deleted_at) VALUES ('50000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003', 'STORE', '11111111-1111-1111-1111-111111111111', '20000000-0000-0000-0000-000000000002', TRUE, '2026-01-08T09:05:00Z', '2026-01-08T09:05:00Z', FALSE, NULL);


INSERT INTO membership_assignment (membership_id, role_id, assigned_at, assigned_by_user_id, expires_at) VALUES ('50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', '2026-01-01T00:00:00Z', '22222222-2222-2222-2222-222222222222', NULL);
INSERT INTO membership_assignment (membership_id, role_id, assigned_at, assigned_by_user_id, expires_at) VALUES ('50000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000004', '2026-01-06T09:05:00Z', '22222222-2222-2222-2222-222222222222', NULL);
INSERT INTO membership_assignment (membership_id, role_id, assigned_at, assigned_by_user_id, expires_at) VALUES ('50000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000005', '2026-01-06T09:05:00Z', '22222222-2222-2222-2222-222222222222', NULL);
INSERT INTO membership_assignment (membership_id, role_id, assigned_at, assigned_by_user_id, expires_at) VALUES ('50000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000004', '2026-01-08T09:05:00Z', '22222222-2222-2222-2222-222222222222', NULL);


INSERT INTO offering (offering_id, type, name, description, price_per_store, is_active) VALUES ('60000000-0000-0000-0000-000000000001', 'PLAN', 'Essentials', 'The base plan — everything a store needs to start selling', 50.00, TRUE);


INSERT INTO feature (feature_id, code, label, scope, description) VALUES ('70000000-0000-0000-0000-000000000001', 'multi_store', 'Multi-store', 'ORGANIZATION', 'Run more than one store under the organization');
INSERT INTO feature (feature_id, code, label, scope, description) VALUES ('70000000-0000-0000-0000-000000000002', 'cross_store_reports', 'Cross-store reports', 'ORGANIZATION', 'Reporting across every store in the organization');
INSERT INTO feature (feature_id, code, label, scope, description) VALUES ('70000000-0000-0000-0000-000000000003', 'store_reports', 'Store reports', 'STORE', 'Sales and inventory reports for a single store');
INSERT INTO feature (feature_id, code, label, scope, description) VALUES ('70000000-0000-0000-0000-000000000004', 'returns', 'Returns', 'STORE', 'Process customer returns and refunds');


INSERT INTO offering_feature (offering_id, feature_id) VALUES ('60000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001');
INSERT INTO offering_feature (offering_id, feature_id) VALUES ('60000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000002');
INSERT INTO offering_feature (offering_id, feature_id) VALUES ('60000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000003');
INSERT INTO offering_feature (offering_id, feature_id) VALUES ('60000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000004');


INSERT INTO subscription (subscription_id, organization_id, offering_id, status, trial_ends_at, current_period_end, created_at, ended_at) VALUES ('80000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '60000000-0000-0000-0000-000000000001', 'ACTIVE', NULL, '2026-08-01T00:00:00Z', '2026-01-01T00:00:00Z', NULL);
