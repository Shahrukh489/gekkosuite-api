-- V1: smoke-test migration to confirm DbUp runs against local Postgres.
CREATE TABLE hello_world (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message    TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO hello_world (message) VALUES ('DbUp is connected 🎉');
