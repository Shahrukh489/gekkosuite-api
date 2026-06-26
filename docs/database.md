## Tables

PostgreSQL `CREATE TABLE` statements. Only keys and structurally-important fields are
included; descriptive columns (name, description, address, etc.) are added later.

Note: `user` is a reserved word in Postgres, so the table name is quoted as `"user"` in DDL.

## Auth / RBAC

```sql
CREATE TABLE "user" (
    user_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY
);

CREATE TABLE role (
    role_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT REFERENCES organization (organization_id),
    store_id        BIGINT REFERENCES store (store_id),
    is_managed      BOOLEAN NOT NULL DEFAULT FALSE,
    -- owner is organization_id OR store_id, or neither when is_managed = true
    CHECK (
        (is_managed = TRUE  AND organization_id IS NULL AND store_id IS NULL)
        OR (is_managed = FALSE AND (organization_id IS NOT NULL) <> (store_id IS NOT NULL))
    )
);

CREATE TABLE permission (
    permission_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    subject       TEXT NOT NULL,   -- e.g. store, organization
    action        TEXT NOT NULL,   -- e.g. refund, edit
    UNIQUE (subject, action)
);

CREATE TABLE user_role (
    user_role_id    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id         BIGINT NOT NULL REFERENCES "user" (user_id),
    role_id         BIGINT NOT NULL REFERENCES role (role_id),
    organization_id BIGINT REFERENCES organization (organization_id),
    store_id        BIGINT REFERENCES store (store_id),
    -- the place is organization_id OR store_id (exactly one set)
    CHECK ((organization_id IS NOT NULL) <> (store_id IS NOT NULL))
);

CREATE TABLE role_permission (
    role_id       BIGINT NOT NULL REFERENCES role (role_id),
    permission_id BIGINT NOT NULL REFERENCES permission (permission_id),
    PRIMARY KEY (role_id, permission_id)
);
```

## Plans / Features

```sql
CREATE TABLE plan (
    plan_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY
);

CREATE TABLE feature (
    feature_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY
);

CREATE TABLE plan_feature (
    plan_id    BIGINT NOT NULL REFERENCES plan (plan_id),
    feature_id BIGINT NOT NULL REFERENCES feature (feature_id),
    PRIMARY KEY (plan_id, feature_id)
);
```

## Organization

```sql
CREATE TABLE organization (
    organization_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    plan_id         BIGINT REFERENCES plan (plan_id),
    owner_user_id   BIGINT REFERENCES "user" (user_id)
);
```

## Store

```sql
CREATE TABLE store (
    store_id        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organization (organization_id)
);

CREATE TABLE product (
    product_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id   BIGINT NOT NULL REFERENCES store (store_id),
    sku        TEXT,
    UNIQUE (store_id, sku)   -- sku is unique within a store
);

CREATE TABLE customer (
    customer_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id    BIGINT NOT NULL REFERENCES store (store_id)
);

CREATE TABLE sales_order (
    order_id    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id    BIGINT NOT NULL REFERENCES store (store_id),
    customer_id BIGINT REFERENCES customer (customer_id),
    status      TEXT NOT NULL   -- e.g. open / paid / refunded
);

CREATE TABLE sales_order_product (
    order_id   BIGINT NOT NULL REFERENCES sales_order (order_id),
    product_id BIGINT NOT NULL REFERENCES product (product_id),
    quantity   INTEGER NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL,   -- snapshots the price at sale time
    PRIMARY KEY (order_id, product_id)
);

CREATE TABLE product_return (
    return_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    store_id  BIGINT NOT NULL REFERENCES store (store_id),
    order_id  BIGINT NOT NULL REFERENCES sales_order (order_id)
);

CREATE TABLE product_return_product (
    return_id  BIGINT NOT NULL REFERENCES product_return (return_id),
    product_id BIGINT NOT NULL REFERENCES product (product_id),
    quantity   INTEGER NOT NULL,
    PRIMARY KEY (return_id, product_id)
);
```


# ER Diagrams

One diagram per entity, showing that entity's own relationships. Legend: `}o--||` means **many-to-one** — the crow's-foot (`}o`) side is the "many," the `||` side is the "one."


## Organization

```mermaid
erDiagram
    organization }o--|| plan : "is on"
    organization }o--|| user : "owned by"
    organization ||--o{ store : "has"
```


## Store

```mermaid
erDiagram
    store }o--|| organization : "belongs to"
    store ||--o{ product : "has"
    store ||--o{ customer : "has"
```


## Plan

```mermaid
erDiagram
    plan    ||--o{ plan_feature : "has"
    feature ||--o{ plan_feature : "in"
```


## User Role

```mermaid
erDiagram
    user_role }o--|| user : "for"
    user_role }o--|| role : "grants"
    user_role }o--|| organization : "at (or)"
    user_role }o--|| store : "at"
```


## Role

```mermaid
erDiagram
    role       }o--|| organization : "owned by (or)"
    role       }o--|| store : "owned by"
    role       ||--o{ role_permission : "has"
    permission ||--o{ role_permission : "in"
```


## Order

```mermaid
erDiagram
    sales_order }o--|| store : "belongs to"
    sales_order }o--|| customer : "placed by"
    sales_order ||--o{ sales_order_product : "contains"
    product     ||--o{ sales_order_product : "appears in"
```


## Return

```mermaid
erDiagram
    product_return }o--|| store : "belongs to"
    product_return }o--|| sales_order : "refunds"
    product_return ||--o{ product_return_product : "contains"
    product        ||--o{ product_return_product : "appears in"
```


# Queries

## Get all the products for every store in an organization

## Get all the permissions for a user

## Get all the users in a store

## Get all the users in an organization 

## Get all the roles for a store (custom and managed)

## Get all the roles for an organization (custom and managed)
