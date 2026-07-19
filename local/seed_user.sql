-- ============================================================================
-- Dev seed — NOT a migration. Deliberately kept outside Migrations/: that folder
-- is wildcard-embedded into the DbUp migration runner (see
-- GekkoSuite.Database.csproj — "Migrations\*.sql"), so anything placed there
-- runs as a real migration against whatever --env is targeted, including prod.
-- This script is for local testing only. Run it directly against a local DB,
-- never through `dotnet run migrate`:
--
--   psql "<local connection string>" -f Seed/dev_seed.sql
--
-- Creates one organization + one active user so POST /auth/login has
-- something to authenticate against.
--
-- Login with:
--   email:    dev@gekkosuite.local
--   password: DevPassword123!
--
-- The password hash below was generated with the exact same Argon2id params
-- AuthService.HashPassword uses (memory=19 MiB, iterations=2, parallelism=1,
-- 32-byte output), stored as "{base64Salt}:{base64Hash}" — the same format
-- AuthService.VerifyPassword expects to read back.
-- ============================================================================

INSERT INTO organization (organization_id, name, description, billing_status, created_at, is_deleted, deleted_at)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'Dev Org',
    'Seeded for local login testing',
    'ACTIVE',
    '2026-01-01T00:00:00Z',
    FALSE,
    NULL
);

INSERT INTO "user" (user_id, organization_id, email, password, name, phone, is_active, is_org_owner, created_by_user_id, created_at, is_deleted, deleted_at)
VALUES (
    '22222222-2222-2222-2222-222222222222',
    '11111111-1111-1111-1111-111111111111',
    'dev@gekkosuite.local',
    'fzR6hQsI5Vr6eeTO8qCZLw==:duYlawAZ54cuHXLRDMu4vAUlMu0WDO/+UfC7LobIwK0=',
    'Dev User',
    NULL,
    TRUE,
    TRUE,
    NULL,
    '2026-01-01T00:00:00Z',
    FALSE,
    NULL
);
