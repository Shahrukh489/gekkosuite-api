-- ============================================================================
-- V1.0.4 — product variant groups. Backs a real parent/child relationship for
-- variants (product.md "Variants"), replacing the old flat, name-matched
-- design: `store_product_group` owns the shared identity (name, description,
-- category, brand, variant dimension NAMES) once per family; each
-- `store_product` variant row keeps only its own dimension VALUES and links
-- back via `group_id`. A row either stands alone and owns its own name, or
-- belongs to a group and defers naming to it — enforced by the CHECK below,
-- not just convention.
-- ============================================================================

-- The variant parent: never itself sellable (no price/sku/stock/tax fields —
-- those stay on store_product, exactly like the shared `product` table).
CREATE TABLE store_product_group (
    -- group id
    group_id                   UUID PRIMARY KEY,
    -- the store this product family belongs to (groups are store-scoped, matching
    -- the rest of the catalog's default-isolated model)
    store_id                   UUID NOT NULL REFERENCES store (store_id),
    -- the owning org (the tenant boundary — stamped for RLS)
    organization_id            UUID NOT NULL REFERENCES organization (organization_id),
    -- product display name, owned here once instead of on every variant row
    name                       VARCHAR(256) NOT NULL,
    -- optional human description
    description                VARCHAR(1024),
    -- free-text grouping for browsing/filtering, e.g. 'Beverages'
    category                   VARCHAR(128),
    -- free-text brand/manufacturer, e.g. 'Lavazza'
    brand                      VARCHAR(128),
    -- up to 3 variant dimension NAMES this product varies along, e.g. 'Flavor'.
    -- Defined once per group; each store_product variant supplies the matching VALUE.
    variant_option_one_name    VARCHAR(64),
    variant_option_two_name    VARCHAR(64),
    variant_option_three_name  VARCHAR(64),
    -- when the group was created (stored UTC)
    created_at                 TIMESTAMPTZ NOT NULL,
    -- when it was last modified (stored UTC)
    updated_at                 TIMESTAMPTZ NOT NULL,
    -- soft-delete flag; TRUE = removed but kept for history
    is_deleted                 BOOLEAN NOT NULL,
    -- when it was soft-deleted (stored UTC); NULL while active
    deleted_at                 TIMESTAMPTZ
);
CREATE INDEX ON store_product_group (store_id);

-- A variant links back to its family. NULL = standalone product (the common case).
ALTER TABLE store_product ADD COLUMN group_id UUID REFERENCES store_product_group (group_id);
CREATE INDEX ON store_product (group_id) WHERE group_id IS NOT NULL;

-- name was NOT NULL; a grouped variant defers naming to its group instead, so it
-- must now be nullable. The CHECK below is what actually enforces "required
-- exactly when standalone" — dropping NOT NULL alone would allow both null and
-- the group to disagree.
ALTER TABLE store_product ALTER COLUMN name DROP NOT NULL;
ALTER TABLE store_product ADD CONSTRAINT store_product_group_name_xor
    CHECK ((group_id IS NULL) = (name IS NOT NULL));

-- Dimension NAMES move to store_product_group (defined once per family); each row
-- keeps only its own VALUE. Applies to both store_product and the shared `product`
-- table, which mirrors store_product's per-variant fields when sharing is on.
ALTER TABLE store_product DROP COLUMN variant_option_one_name;
ALTER TABLE store_product DROP COLUMN variant_option_two_name;
ALTER TABLE store_product DROP COLUMN variant_option_three_name;
ALTER TABLE product DROP COLUMN variant_option_one_name;
ALTER TABLE product DROP COLUMN variant_option_two_name;
ALTER TABLE product DROP COLUMN variant_option_three_name;
