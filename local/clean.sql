-- Drops every GekkoSuite table and enum type so the local database can be rebuilt from scratch.
-- CASCADE handles FK dependencies, so drop order does not matter; IF EXISTS keeps it error-free
-- when an object was never created locally.

-- Tables (CASCADE also drops their indexes and FK constraints)
DROP TABLE IF EXISTS membership_assignment CASCADE;
DROP TABLE IF EXISTS membership          CASCADE;
DROP TABLE IF EXISTS role_permission     CASCADE;
DROP TABLE IF EXISTS permission          CASCADE;
DROP TABLE IF EXISTS role                CASCADE;
DROP TABLE IF EXISTS store_customer      CASCADE;
DROP TABLE IF EXISTS store_product       CASCADE;
DROP TABLE IF EXISTS store               CASCADE;
DROP TABLE IF EXISTS user_account        CASCADE;
DROP TABLE IF EXISTS organization        CASCADE;

-- Removed-from-code billing tables (may still exist in an older local DB)
DROP TABLE IF EXISTS subscription        CASCADE;
DROP TABLE IF EXISTS offering_feature    CASCADE;
DROP TABLE IF EXISTS offering            CASCADE;
DROP TABLE IF EXISTS feature             CASCADE;

-- Enum types (dropped after the tables that reference them)
DROP TYPE  IF EXISTS scope               CASCADE;
DROP TYPE  IF EXISTS store_type          CASCADE;
DROP TYPE  IF EXISTS billing_status      CASCADE;
DROP TYPE  IF EXISTS offering_status     CASCADE;
DROP TYPE  IF EXISTS offering_type       CASCADE;
