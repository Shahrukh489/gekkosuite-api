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
    -- Owner rule, enforced by the DB:
    --   managed role (is_managed=true): BOTH owner columns NULL
    --   custom role  (is_managed=false): EXACTLY ONE owner column set, never both
    -- The `<>` (XOR) means exactly one of the two is NOT NULL.
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
    -- the place is organization_id OR store_id: EXACTLY ONE set, never both, never neither.
    -- The `<>` (XOR) means exactly one of the two is NOT NULL.
    CHECK ((organization_id IS NOT NULL) <> (store_id IS NOT NULL))
);

-- prevent the same role being granted twice to a user at the same place
-- (two partial indexes because the place lives in one of two nullable columns)
CREATE UNIQUE INDEX user_role_store_uq
    ON user_role (user_id, role_id, store_id)
    WHERE store_id IS NOT NULL;

CREATE UNIQUE INDEX user_role_org_uq
    ON user_role (user_id, role_id, organization_id)
    WHERE organization_id IS NOT NULL;

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

Each query returns flat joined rows with all the columns a screen needs. The API layer
groups/combines these rows (e.g. collapsing a user's many roles into one object), so the
SQL stays plain joins — no aggregation here.

Bind parameters: `:organization_id`, `:store_id`, `:user_id`, `:order_id`.

These examples use descriptive columns (`name`, `email`, `phone`, `price`, etc.) that
are added to the tables later — the schema above lists keys only.


## Products for every store in an organization

Each product with its price/stock and which store it's in.

```sql
SELECT p.product_id,
       p.name,
       p.sku,
       p.price,
       p.quantity,
       s.store_id,
       s.name AS store_name
FROM product p
JOIN store s ON s.store_id = p.store_id
WHERE s.organization_id = :organization_id
ORDER BY s.name, p.name;
```


## A user's full access (everything the app loads at login)

One row per permission, tagged with the place it applies to (a store or the org) and
that place's name. The API groups these by place to build the per-place permission lists.

```sql
SELECT ur.organization_id,
       o.name AS organization_name,
       ur.store_id,
       s.name AS store_name,
       p.subject,
       p.action
FROM user_role ur
JOIN role_permission rp ON rp.role_id = ur.role_id
JOIN permission p       ON p.permission_id = rp.permission_id
LEFT JOIN organization o ON o.organization_id = ur.organization_id
LEFT JOIN store s        ON s.store_id        = ur.store_id
WHERE ur.user_id = :user_id;
```


## Users in a store (with their info and roles)

For a store's "staff" screen. One row per user-role, with the user's details. A user with
two roles at the store returns two rows; the API groups them into one user with a role
list.

```sql
SELECT u.user_id,
       u.name,
       u.email,
       u.phone,
       r.role_id,
       r.name AS role_name
FROM "user" u
JOIN user_role ur ON ur.user_id = u.user_id
JOIN role r       ON r.role_id  = ur.role_id
WHERE ur.store_id = :store_id
ORDER BY u.name;
```


## Users in an organization (with info, and where they work)

Every person in the org — at the org level, or at any store under it — one row per grant
with the user's details, the role, and the place. The API groups by user.

```sql
SELECT u.user_id,
       u.name,
       u.email,
       u.phone,
       r.name AS role_name,
       ur.organization_id,
       o.name AS organization_name,
       ur.store_id,
       s.name AS store_name
FROM "user" u
JOIN user_role ur        ON ur.user_id = u.user_id
JOIN role r              ON r.role_id  = ur.role_id
LEFT JOIN organization o ON o.organization_id = ur.organization_id
LEFT JOIN store s        ON s.store_id        = ur.store_id
WHERE ur.organization_id = :organization_id
   OR s.organization_id   = :organization_id
ORDER BY u.name;
```


## Roles available to a store (with their permissions)

The built-in (managed) roles plus this store's own custom roles. One row per
role-permission; the API groups by role.

```sql
SELECT r.role_id,
       r.name,
       r.is_managed,
       p.subject,
       p.action
FROM role r
LEFT JOIN role_permission rp ON rp.role_id = r.role_id
LEFT JOIN permission p       ON p.permission_id = rp.permission_id
WHERE r.is_managed = TRUE          -- built-in roles
   OR r.store_id   = :store_id     -- this store's custom roles
ORDER BY r.is_managed DESC, r.name;
```


## Roles available to an organization (with their permissions)

The built-in roles, the org's own custom roles, and the custom roles created by any store
under the org — so an org admin sees every role in their organization. One row per
role-permission; the API groups by role.

```sql
SELECT r.role_id,
       r.name,
       r.is_managed,
       p.subject,
       p.action
FROM role r
LEFT JOIN role_permission rp ON rp.role_id = r.role_id
LEFT JOIN permission p       ON p.permission_id = rp.permission_id
WHERE r.is_managed = TRUE                     -- built-in roles
   OR r.organization_id = :organization_id    -- the org's own custom roles
   OR r.store_id IN (                          -- custom roles of stores under the org
        SELECT store_id FROM store
        WHERE organization_id = :organization_id
      )
ORDER BY r.is_managed DESC, r.name;
```


## An order with its line items (receipt / order detail)

The order, who placed it, and one row per line item. The API groups the lines under the
order and sums the total.

```sql
SELECT so.order_id,
       so.status,
       c.customer_id,
       c.name AS customer_name,
       p.product_id,
       p.name AS product_name,
       sop.quantity,
       sop.unit_price
FROM sales_order so
LEFT JOIN customer c         ON c.customer_id = so.customer_id
JOIN sales_order_product sop ON sop.order_id = so.order_id
JOIN product p               ON p.product_id = sop.product_id
WHERE so.order_id = :order_id;
```


## Products in a store (catalog / inventory list)

The main product list for a single store's catalog or inventory screen.

```sql
SELECT product_id,
       name,
       sku,
       price,
       quantity
FROM product
WHERE store_id = :store_id
ORDER BY name;
```


## Search products in a store

Type-ahead / search box on the product list. Matches name or SKU.

```sql
SELECT product_id,
       name,
       sku,
       price,
       quantity
FROM product
WHERE store_id = :store_id
  AND (name ILIKE '%' || :search || '%' OR sku ILIKE '%' || :search || '%')
ORDER BY name
LIMIT 50;
```


## Customers in a store

The customer list for a store.

```sql
SELECT customer_id,
       name,
       email,
       phone
FROM customer
WHERE store_id = :store_id
ORDER BY name;
```


## Orders for a store (order list)

The orders screen for a store, newest first, with the customer's name.

```sql
SELECT so.order_id,
       so.status,
       c.name AS customer_name
FROM sales_order so
LEFT JOIN customer c ON c.customer_id = so.customer_id
WHERE so.store_id = :store_id
ORDER BY so.order_id DESC;
```


## Orders for a customer (customer history)

Every order a single customer has placed.

```sql
SELECT order_id,
       status
FROM sales_order
WHERE customer_id = :customer_id
ORDER BY order_id DESC;
```


## Returns for a store (returns list)

The returns screen for a store, with the order each return refunds.

```sql
SELECT pr.return_id,
       pr.order_id
FROM product_return pr
WHERE pr.store_id = :store_id
ORDER BY pr.return_id DESC;
```


## A return with its line items (return detail)

The return, the order it refunds, and one row per returned item. The API groups the lines
under the return.

```sql
SELECT pr.return_id,
       pr.order_id,
       p.product_id,
       p.name AS product_name,
       prp.quantity
FROM product_return pr
JOIN product_return_product prp ON prp.return_id = pr.return_id
JOIN product p                  ON p.product_id = prp.product_id
WHERE pr.return_id = :return_id;
```


## Stores in an organization (store switcher / store list)

The list of stores for an org — used by the org dashboard and the place switcher.

```sql
SELECT store_id,
       name
FROM store
WHERE organization_id = :organization_id
ORDER BY name;
```


## A role with its permissions (role detail / edit)

For viewing or editing a single role. One row per permission; the API groups them under
the role.

```sql
SELECT r.role_id,
       r.name,
       r.is_managed,
       p.subject,
       p.action
FROM role r
LEFT JOIN role_permission rp ON rp.role_id = r.role_id
LEFT JOIN permission p       ON p.permission_id = rp.permission_id
WHERE r.role_id = :role_id;
```


## All available permissions (role builder)

The full list of permissions the UI offers when building or editing a role.

```sql
SELECT permission_id,
       subject,
       action
FROM permission
ORDER BY subject, action;
```


## An organization's plan and its features

For a billing / plan screen: the org's plan and the features it includes.

```sql
SELECT pl.plan_id,
       pl.name AS plan_name,
       f.feature_id,
       f.name AS feature_name
FROM organization o
JOIN plan pl         ON pl.plan_id = o.plan_id
LEFT JOIN plan_feature pf ON pf.plan_id = pl.plan_id
LEFT JOIN feature f       ON f.feature_id = pf.feature_id
WHERE o.organization_id = :organization_id
ORDER BY f.name;
```
