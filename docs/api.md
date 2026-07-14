
│ 4   │ Stores                         │ org creates/manages stores                     │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 5   │ Users & Access                 │ users, memberships, roles (the RBAC admin)     │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 6   │ Products                       │ store-level selling data                       │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 7   │ Customers                      │ store + shared                                 │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 8   │ Sales & Returns                │ the core transactions                          │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 9   │ Suppliers, Purchases, Expenses │ org-level money                                │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 10  │ Plans & Features               │ billing                                        │
└─────┴────────────────────────────────┴────────────────────────────────────────────────┘

# Flows

The ordered API calls a client makes for each scenario.

**Sign up (new tenant):**
1. `GET /plans` — show plan options on the signup page.
2. `POST /onboarding` — create the org, owner (pending), plan, and first store.
3. `POST /onboarding/verify` — owner clicks the email link to activate the account.

**Log in:**
1. `POST /auth/login` — get an access token.
2. `GET /user` — get the user and their switcher list (org and/or stores).
3. Enter a place from the switcher — load that context (org or store).

**Log out:**
1. `POST /auth/logout` — end the session.

---

## Onboarding

Onboarding is signup — it runs *before* auth and creates a whole tenant from nothing (org, owner, plan,
first store). All onboarding endpoints are **public** (no token; the user doesn't exist yet), so they're
a prime abuse target: rate-limit them, protect with a captcha, and gate account use behind email
verification. The owner account is created **pending** and cannot log in until verified.

### List plans

- **Description:** Lists the available plans so the signup page can show options and the user can pick one.
- **Security:** public and read-only, so low risk.
  - Rate-limit by IP.
  - Cache the response (plans rarely change).
- **Method:** `GET`
- **URL:** `/plans`
- **Scope:** _(public — no token required)_
- **Permission:** _(none)_
- **Request Headers:** _(none)_
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  [
    {
      "planId": "plan_pro",
      "name": "Pro",
      "description": "For growing chains",
      "pricePerStore": 150.00
    }
  ]
  ```

- **Errors:** _(none — public)_


### Sign up - @TODO: investigate

- **Description:** Creates a new tenant in one transaction — the organization, its owner (pending email
  verification), a **subscription** to the chosen plan (status `TRIALING` or `ACTIVE`), and a first
  store. Sends a verification email and returns no token.
- **Security:** public **and** it writes data, so this is the main abuse target. Layer these:
  - **Email verification** — the account is created *pending* and inert until verified; a background job hard-deletes unverified signups after ~24–48h.
  - **Rate-limit by IP and by email** — caps volume and prevents email-bombing a victim.
  - **CAPTCHA** — an invisible challenge (e.g. Cloudflare Turnstile / reCAPTCHA v3) to block bots.
  - **Block disposable-email domains** — reject known throwaway providers.
  - **WAF at the edge** — Cloudflare / AWS WAF for bot fingerprinting and DDoS mitigation.
  - **(Optional, strongest)** require a valid card up front — for a B2B money app this nearly eliminates spam.
  - **Honeypot field** — a hidden form field real users never fill; if filled, drop as a bot.
- **Method:** `POST`
- **URL:** `/onboarding`
- **Scope:** _(public — no token required)_
- **Permission:** _(none)_
- **Request Headers:**
  - `Content-Type: application/json`
- **Request Body:**

  ```json
  {
    "organizationName": "Acme Inc",
    "planId": "plan_pro",
    "owner": {
      "email": "maria@acme.com",
      "password": "••••••••",
      "name": "Maria"
    },
    "store": {
      "name": "Downtown",
      "type": "PHYSICAL"
    }
  }
  ```

- **Response Status:** `201 Created`
- **Response Body:**

  ```json
  {
    "organizationId": "a3f9...",
    "message": "Check your email to verify your account before logging in."
  }
  ```

- **Errors:**
  - `400` — missing/invalid fields (e.g. weak password, unknown `planId`)
  - `409` — the email is already registered
  - `429` — too many attempts (rate-limited)

### Verify email

- **Description:** Confirms the owner's email using the token from the verification email, activating the
  account.
- **Security:** public, so protect against token-guessing.
  - **Single-use, expiring tokens** — high-entropy, invalidated after first use, short lifetime.
  - **Rate-limit by IP** — stops brute-forcing verification tokens.
- **Method:** `POST`
- **URL:** `/onboarding/verify`
- **Scope:** _(public — no token required)_
- **Permission:** _(none)_
- **Request Headers:**
  - `Content-Type: application/json`
- **Request Body:**

  ```json
  {
    "token": "verify_abc123..."
  }
  ```

- **Response Status:** `200 OK`
- **Response Body:** _(none)_
- **Errors:**
  - `400` — token missing, invalid, or expired

---

## Auth

Auth endpoints are the way in, so they don't follow the usual scope/permission model — `login` is public
(you have no token yet), and `logout` only needs a valid token (any authenticated user)

### Login

- **Description:** Exchanges an email and password for an access token.
- **Method:** `POST`
- **URL:** `/auth/login`
- **Scope:** _(public — no token required)_
- **Permission:** _(none)_
- **Request Headers:**
  - `Content-Type: application/json`
- **Request Body:**

  ```json
  {
    "email": "maria@acme.com",
    "password": "••••••••"
  }
  ```
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  {
    "accessToken": "eyJhbGciOi...", //carries `userId` + `organizationId`
    "expiresIn": 900 //in seconds ?? TODO: maybe utc?
  }
  ```

- **Errors:**
  - `400` — missing email or password
  - `401` — invalid credentials (same response whether the email is unknown or the password is wrong)
  - `403` — the account is disabled (`user.is_active = false`)
  - `429` — too many attempts (rate-limited / locked out)

### Logout

- **Description:** Ends the current session and invalidates the token so it can no longer be used.
- **Method:** `POST`
- **URL:** `/auth/logout`
- **Scope:** _(any authenticated user)_
- **Permission:** _(none)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `204 No Content`
- **Response Body:** _(none)_
- **Errors:**
  - `401` — not authenticated

---

## Organization

The organization is the tenant, and there's exactly one per caller — fixed in the token at login. It's a
**singleton** resource: the endpoints are id-less (`/organization`), because the token already says which
org. The resource, the caller's permissions, and the plan's enabled features are three separate endpoints.

### Get organization

- **Description:** Returns the organization — its name, plan, default store, settings, and its **billing
  state**. The org is the paying entity, so this is the source-of-truth surface for whether the account is
  read-only: `billing.status` (derived from the org's live subscriptions) and `billing.readOnly` (writes
  frozen when overdue), with a `message` prompting payment. The UI uses this on entering the org context
  to disable write actions and show a "pay now" banner.
- **Method:** `GET`
- **URL:** `/organization`
- **Scope:** `ORGANIZATION`
- **Permission:** `organization:read`
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  {
    "organizationId": "acme...",
    "name": "Acme Inc",
    "description": "Coffee chain",
    "planId": "plan_pro",
    "defaultStoreId": "s1...",
    "allowShareProducts": false,
    "createdAt": "2026-01-05T12:00:00Z",
    "billing": {
      "status": "PAST_DUE",
      "readOnly": false,
      "message": "Your payment is overdue. Pay now to avoid losing write access."
    }
  }
  ```

  - **`billing`** — the org's overall billing state, derived from its live subscriptions:
    - `status` — one of `TRIALING` / `ACTIVE` / `PAST_DUE` / `UNPAID` / `CANCELED`.
    - `readOnly` — `true` when writes are blocked (overdue: `UNPAID` / `CANCELED`). The UI disables write
      buttons and shows `message` when this is `true`.
    - `message` — a human prompt (with a "pay now" call to action when overdue); `null` when all is well.
    - This is separate from features: `GET .../features` returns what the plan *includes* (a missed
      payment doesn't strip features — an unpaid Pro org still lists Pro features), while `readOnly` says
      whether the org is *frozen from writing*. It's a UI hint; the server still enforces it — a write
      while read-only returns `402` (see `auth.md`'s billing gate).

- **Errors:**
  - `401` — not authenticated
  - `403` — lacks `organization:read`

### Get my organization permissions

- **Description:** Returns the permissions the caller holds in the organization. Used by the UI to show
  or hide org tabs and actions.
- **Method:** `GET`
- **URL:** `/organization/permissions`
- **Scope:** `ORGANIZATION`
- **Permission:** _(none beyond an org membership)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  {
    "permissions": ["organization:read", "user:create", "store:create", "product:edit", "order:refund"]
  }
  ```

- **Errors:**
  - `401` — not authenticated
  - `403` — the caller has no organization membership

### Get organization features

- **Description:** Returns the **organization-scoped** feature codes the org's plan enables (e.g. billing,
  multi-store). Used by the UI to show or hide org-level features. Store-scoped features are not returned
  here — a store reads its own via `GET /stores/{storeId}/features`.
- **Method:** `GET`
- **URL:** `/organization/features`
- **Scope:** `ORGANIZATION`
- **Permission:** _(none beyond an org membership)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  {
    "features": ["billing", "multi_store", "cross_store_reports", "user_management"]
  }
  ```

- **Errors:**
  - `401` — not authenticated
  - `403` — the caller has no organization membership

---

## Stores

A store is where selling happens. Store endpoints live under `/stores/{storeId}/...`, and the store must
belong to the caller's org — any other id returns `404`.

### List stores — @TODO - **URL:** `GET /stores`

### Get store

- **Description:** Returns a single store's full record — name, type, description, address, contact, and
  currency. Used for the store's detail and edit screens. The store must be in the caller's org, and a
  store user must have access to it. Also carries a **`billing.readOnly`** flag so a store user (who can't
  call the org endpoint) learns on entering the store whether the account is frozen. This is the **org's**
  billing state mirrored down — a store never pays on its own; it's read-only only because its org is
  overdue. Store users get just the flag and a generic message, not the org's billing internals.
- **Method:** `GET`
- **URL:** `/stores/{storeId}`
- **Scope:** `ORGANIZATION` or `STORE`
- **Permission:** `store:read`
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  {
    "storeId": "s1...",
    "name": "Downtown",
    "type": "PHYSICAL",
    "description": "Flagship location",
    "address": "123 Main St",
    "city": "Austin",
    "state": "TX",
    "postalCode": "78701",
    "country": "US",
    "currency": "USD",
    "phone": "+1 512 555 0100",
    "email": "downtown@acme.com",
    "isDefault": true,
    "createdAt": "2026-01-05T12:00:00Z",
    "billing": {
      "readOnly": true,
      "message": "This store is read-only. Contact your Organization Admin."
    }
  }
  ```

- **Errors:**
  - `401` — not authenticated
  - `403` — lacks `store:read`
  - `404` — the store is not in the caller's org, or a store user has no access to it (existence not leaked)

### Create store

- **Description:** Creates a new store in the caller's organization. Org-only — a store user can't create
  stores. The store starts empty (its own products, customers, and sales). A new store raises the org's
  per-store bill (see `plans.md` → *Billing & store count*).
  - **@TODO — how to collect payment for the new store.** A store add changes what the org owes, so
    creation must tie into billing (e.g. pay-first via Stripe Checkout + webhook, or charge the saved
    card). The exact payment flow is unsettled; decide and wire it before this ships.
- **Method:** `POST`
- **URL:** `/stores`
- **Scope:** `ORGANIZATION`
- **Permission:** `store:create` _(elevated)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
  - `Content-Type: application/json`
- **Request Body:**

  ```json
  {
    "name": "Westside",
    "type": "PHYSICAL",
    "description": "New location",
    "address": "456 West Ave",
    "city": "Austin",
    "state": "TX",
    "postalCode": "78703",
    "country": "US",
    "currency": "USD",
    "phone": "+1 512 555 0200",
    "email": "westside@acme.com"
  }
  ```

  Only `name` and `type` are required; everything else is optional.

- **Response Status:** `201 Created`
- **Response Body:** the created store (same shape as **Get store**).
- **Errors:**
  - `400` — missing/invalid fields (e.g. missing `name`, unknown `type`)
  - `401` — not authenticated
  - `402` — org is read-only (overdue billing) — see `auth.md`'s billing gate
  - `403` — lacks `store:create`

### Update store

- **Description:** Updates a store's details (name, type, description, address, contact, currency).
  Org-only. A partial update — only the fields sent are changed. Does not affect billing (the store count
  is unchanged).
- **Method:** `PATCH`
- **URL:** `/stores/{storeId}`
- **Scope:** `ORGANIZATION`
- **Permission:** `store:edit`
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
  - `Content-Type: application/json`
- **Request Body:** (any subset of the editable fields)

  ```json
  {
    "name": "Westside Flagship",
    "phone": "+1 512 555 0299"
  }
  ```

- **Response Status:** `200 OK`
- **Response Body:** the updated store (same shape as **Get store**).
- **Errors:**
  - `400` — invalid field values (e.g. unknown `type`)
  - `401` — not authenticated
  - `402` — org is read-only (overdue billing)
  - `403` — lacks `store:edit`
  - `404` — the store is not in the caller's org

### Delete store

- **Description:** Soft-deletes a store (`is_deleted = true`, `deleted_at` set) — kept for history since
  past sales reference it, so it's never hard-deleted. Org-only. The organization's **default store cannot
  be deleted** — reassign the default first. Removing a store lowers the org's per-store bill.
  - **@TODO — how to reflect the removal in billing.** A delete lowers what the org owes; tie into the
    same billing flow settled for Create store.
- **Method:** `DELETE`
- **URL:** `/stores/{storeId}`
- **Scope:** `ORGANIZATION`
- **Permission:** `store:delete` _(elevated)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `204 No Content`
- **Response Body:** _(none)_
- **Errors:**
  - `401` — not authenticated
  - `402` — org is read-only (overdue billing)
  - `403` — lacks `store:delete`
  - `404` — the store is not in the caller's org
  - `409` — the store is the org's default store (reassign the default before deleting)

### Get store features

- **Description:** Returns the **store-scoped** feature codes the store's plan enables (e.g. returns, AI
  recommendations). The store inherits its org's plan, but only store-applicable features are returned —
  a store user never sees the org's features (like billing). Used by the UI to show or hide store tabs.
- **Method:** `GET`
- **URL:** `/stores/{storeId}/features`
- **Scope:** `STORE`
- **Permission:** _(none beyond access to the store)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  {
    "features": ["reports", "returns", "ai_recommendations"]
  }
  ```

- **Errors:**
  - `401` — not authenticated
  - `403` — the caller has no access to this store
  - `404` — the store is not in the caller's org

### List store users

- **Description:** Lists the users who have a membership at this store — the store's staff roster, with
  each person's role there. A store admin can list **their own** store's roster; an org user can list any
  store's in the org. Identity fields plus the store role only — no other-store or org membership info is
  exposed. (Uses the same `user:read` permission as the org user list; the store scope limits a store
  admin to their own store.)
- **Method:** `GET`
- **URL:** `/stores/{storeId}/users`
- **Scope:** `ORGANIZATION` or `STORE`
- **Permission:** `user:read`
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  [
    {
      "userId": "u3...",
      "name": "Sara",
      "email": "sara@acme.com",
      "isActive": true,
      "role": "Cashier"
    },
    {
      "userId": "u4...",
      "name": "Marcus",
      "email": "marcus@acme.com",
      "isActive": true,
      "role": "Manager"
    }
  ]
  ```

- **Errors:**
  - `401` — not authenticated
  - `403` — lacks `user:read`, or (store user) the store isn't theirs
  - `404` — the store is not in the caller's org

---

## Users & Access

The RBAC admin. Three separate concerns, kept as separate endpoints so each stays single-responsibility:

- **Users** — the login/person (`/users`). A user is just an identity; on its own it can't act anywhere.
- **Memberships** — *where* a user can act: the whole organization, or one store (`/users/{userId}/memberships`).
- **Roles & permissions** — *what* they can do there: roles granted on a membership, built from permissions.

Everything here is org-administered — a store user manages neither users nor access. All ids in the path
must belong to the caller's org; anything else returns `404`. The one exception is **`GET /user`** (self,
below), which any authenticated user may call about themselves with no permission.

### Get current user

- **Description:** Returns the logged-in user and the list of places they can act — the organization
  and/or stores — as lightweight entries (id, name, type, role), without permissions. An org user gets
  the organization plus every store in it; a store user gets only the stores they belong to. This is the
  **self** read (the singleton user tied to the token) — the caller sees their *own* memberships, needs no
  permission, and always may. (Seeing *another* user's memberships is the admin endpoint
  `GET /users/{userId}/memberships`, below.)
- **Method:** `GET`
- **URL:** `/user`
- **Scope:** _(any authenticated user)_
- **Permission:** _(none)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body** (org user — org + all stores):

  ```json
  {
    "user": {
      "userId": "u1...",
      "name": "Maria",
      "email": "maria@acme.com",
      "organizationId": "acme..."
    },
    "memberships": [
      { "type": "ORGANIZATION", "organizationId": "acme...", "name": "Acme Inc", "role": "Org Admin" },
      { "type": "STORE", "storeId": "s1...", "name": "Downtown", "role": "Org Admin" },
      { "type": "STORE", "storeId": "s2...", "name": "Uptown", "role": "Org Admin" }
    ]
  }
  ```

- **Response Body** (store user — only their stores):

  ```json
  {
    "user": {
      "userId": "u2...",
      "name": "Bob",
      "email": "bob@acme.com",
      "organizationId": "acme..."
    },
    "memberships": [
      { "type": "STORE", "storeId": "s1...", "name": "Downtown", "role": "Cashier" }
    ]
  }
  ```

  - **`memberships`** — each entry carries its `type` (`ORGANIZATION` or `STORE`), the place's id and
    `name`, and the `role` the user holds there. An org user gets the `ORGANIZATION` entry plus every
    store (the same org role name on each, e.g. `Org Admin`); a store user gets just their stores with
    their store role at each. Permissions are not included.

- **Errors:**
  - `401` — not authenticated

### Create user

- **Description:** Creates a user (a login) in the caller's organization. Org-only. The user is created
  with **no memberships** — they exist but can't act anywhere until granted a membership (see *Grant
  membership*). The account starts active; `organizationId` is stamped from the caller's token, not the
  body.
- **Method:** `POST`
- **URL:** `/users`
- **Scope:** `ORGANIZATION`
- **Permission:** `user:create` _(elevated)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
  - `Content-Type: application/json`
- **Request Body:**

  ```json
  {
    "name": "Sara",
    "email": "sara@acme.com",
    "password": "••••••••",
    "phone": "+1 512 555 0300"
  }
  ```

  `name`, `email`, and `password` are required; `phone` is optional.

- **Response Status:** `201 Created`
- **Response Body:**

  ```json
  {
    "userId": "u3...",
    "name": "Sara",
    "email": "sara@acme.com",
    "phone": "+1 512 555 0300",
    "isActive": true,
    "createdAt": "2026-07-13T12:00:00Z"
  }
  ```

- **Errors:**
  - `400` — missing/invalid fields (e.g. weak password, malformed email)
  - `401` — not authenticated
  - `402` — org is read-only (overdue billing)
  - `403` — lacks `user:create`
  - `409` — the email is already registered (globally unique)

### List users

- **Description:** Lists the users in the caller's organization — identity fields only (no memberships;
  those are on the per-user detail). Org-only. Used for the Users admin screen. To list the staff of a
  single store, use `GET /stores/{storeId}/users` instead.
- **Method:** `GET`
- **URL:** `/users`
- **Scope:** `ORGANIZATION`
- **Permission:** `user:read`
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  [
    {
      "userId": "u1...",
      "name": "Maria",
      "email": "maria@acme.com",
      "phone": "+1 512 555 0100",
      "isActive": true,
      "createdAt": "2026-01-05T12:00:00Z"
    },
    {
      "userId": "u3...",
      "name": "Sara",
      "email": "sara@acme.com",
      "phone": "+1 512 555 0300",
      "isActive": true,
      "createdAt": "2026-07-13T12:00:00Z"
    }
  ]
  ```

- **Errors:**
  - `401` — not authenticated
  - `403` — lacks `user:read`

### Grant membership

- **Description:** Grants a user a place to act — either the whole **organization** or one **store** — and
  assigns the initial role there. Org-only. A membership is meaningless without a role, so `roleId` is
  required. A user may hold **either** org memberships **or** store memberships, not both; granting the
  wrong kind is rejected. The role's scope must match the membership kind (a STORE role for a store
  membership, an ORGANIZATION role for an org membership). For a store membership, an optional register
  `pin` may be set (stored hashed). The role grant may carry an optional `expiresAt` (auto-expires the
  role; `null` = never).
- **Method:** `POST`
- **URL:** `/users/{userId}/memberships`
- **Scope:** `ORGANIZATION`
- **Permission:** `membership:assign` _(elevated)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
  - `Content-Type: application/json`
- **Request Body** (store membership):

  ```json
  {
    "scope": "STORE",
    "storeId": "s1...",
    "roleId": "role_cashier",
    "pin": "4821",
    "expiresAt": "2026-12-31T00:00:00Z"
  }
  ```

  An org membership omits `storeId` and `pin`:

  ```json
  {
    "scope": "ORGANIZATION",
    "roleId": "role_org_admin"
  }
  ```

  `scope` and `roleId` are always required; `storeId` is required for `STORE`; `pin` and `expiresAt` are
  optional.

- **Response Status:** `201 Created`
- **Response Body:**

  ```json
  {
    "membershipId": "m9...",
    "userId": "u3...",
    "scope": "STORE",
    "storeId": "s1...",
    "role": { "roleId": "role_cashier", "name": "Cashier", "expiresAt": "2026-12-31T00:00:00Z" },
    "isActive": true,
    "createdAt": "2026-07-13T12:05:00Z"
  }
  ```

- **Errors:**
  - `400` — invalid body: `STORE` without `storeId`, `ORGANIZATION` with `storeId`, unknown `scope`, or
    the role's scope doesn't match the membership kind (e.g. an org role on a store membership)
  - `401` — not authenticated
  - `402` — org is read-only (overdue billing)
  - `403` — lacks `membership:assign`
  - `404` — the user, store, or role is not in the caller's org
  - `409` — conflicts with the one-kind rule (user already has the other kind), or a live membership
    already exists at this store/org
