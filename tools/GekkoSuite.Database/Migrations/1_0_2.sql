-- ============================================================================
-- V1.0.2 — offering catalog (plans & add-ons). Backs GET /plans and GET /addons
-- (docs/api.md). Mirrors docs/database.md's `offering` table, minus
-- `feature`/`offering_feature` — those aren't needed until the feature-gating
-- endpoints (GET /organization/features etc.) are built, so they're deferred.
--
-- Same convention as 1_0_0.sql/1_0_1.sql: NO column defaults, NO server-side
-- value generation. Every value is supplied explicitly, including in the seed
-- INSERT below — reference/catalog data every environment needs (like the
-- permission catalog and managed roles in 1_0_1.sql), not a throwaway tenant
-- fixture, so it lives in the migration rather than local/mock_data.sql.
-- ============================================================================

-- What an org can subscribe to: a base PLAN or a stackable ADDON. Both are the same shape — a priced
-- bundle of features — so they share one table, told apart by `type` (see docs/plans.md).
CREATE TYPE offering_type AS ENUM ('PLAN', 'ADDON');

-- An offering: a priced bundle of features an org subscribes to. Priced per store.
CREATE TABLE offering (
    offering_id     UUID PRIMARY KEY,
    -- PLAN = baseline plan; ADDON = stackable extra bought on top
    type            offering_type NOT NULL,
    -- display name, e.g. 'Basic', 'Pro', 'Marketing' (unique across offerings)
    name            TEXT NOT NULL UNIQUE,
    -- optional blurb about the offering
    description     TEXT,
    -- per-store price; the bill adds this × store count for each of the org's live offerings (0 = free)
    price_per_store NUMERIC NOT NULL,
    -- still offered in the catalog? (FALSE = retired; existing subscriptions keep it)
    is_active       BOOLEAN NOT NULL
);

-- Seed catalog: the same "Pro" plan and "Marketing" add-on already used as the worked examples in
-- docs/api.md (GET /plans, GET /addons, and GET /organization's billing.subscriptions), so the demo data
-- matches the docs verbatim.
INSERT INTO offering (offering_id, type, name, description, price_per_store, is_active) VALUES
    ('60000000-0000-0000-0000-000000000001', 'PLAN',  'Pro',       'For growing chains',    150.00, TRUE),
    ('60000000-0000-0000-0000-000000000002', 'ADDON', 'Marketing', 'Email & SMS campaigns', 30.00,  TRUE);
