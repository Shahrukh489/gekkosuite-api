SELECT * FROM offering;

SELECT * FROM offering_feature;

SELECT * FROM feature;

SELECT * FROM subscription;

SELECT * FROM organization;

SELECT * FROM store;

SELECT * FROM user_account;

SELECT * FROM membership;

SELECT * FROM membership_assignment;

SELECT
      o.organization_id AS OrganizationId,
      o.name AS Name,
      r.role_id AS RoleId,
      r.name AS RoleName,
      ma.assigned_at AS AssignedAt,
      ma.expires_at AS ExpiresAt
  FROM membership m
  JOIN membership_assignment ma ON ma.membership_id = m.membership_id
  JOIN role r ON r.role_id = ma.role_id AND r.scope = 'ORGANIZATION'
  JOIN organization o ON o.organization_id = m.organization_id AND NOT o.is_deleted
  JOIN user_account u ON u.user_id = m.user_id
  WHERE m.user_id = '22222222-2222-2222-2222-222222222222'
      AND m.organization_id = '11111111-1111-1111-1111-111111111111'
      AND m.scope = 'ORGANIZATION'
      AND u.is_active AND NOT u.is_deleted
      AND m.is_active AND NOT m.is_deleted
      AND (ma.expires_at IS NULL OR ma.expires_at > now());


SELECT
    s.store_id AS StoreId,
    s.name AS Name,
    r.role_id AS RoleId,
    r.name AS RoleName,
    ma.assigned_at AS AssignedAt,
    ma.expires_at AS ExpiresAt
FROM membership m
JOIN membership_assignment ma ON ma.membership_id = m.membership_id
JOIN role r ON r.role_id = ma.role_id AND r.scope = 'STORE'
JOIN store s ON s.store_id = m.store_id AND NOT s.is_deleted
JOIN user_account u ON u.user_id = m.user_id
  WHERE m.user_id = '10000000-0000-0000-0000-000000000003'
      AND m.organization_id = '11111111-1111-1111-1111-111111111111'
    AND m.scope = 'STORE'
    AND u.is_active AND NOT u.is_deleted
    AND m.is_active AND NOT m.is_deleted
    AND (ma.expires_at IS NULL OR ma.expires_at > now())
ORDER BY m.created_at
