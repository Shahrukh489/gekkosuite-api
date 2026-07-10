┌─────┬────────────────────────────────┬────────────────────────────────────────────────┐
│  #  │            Section             │                 Why this order                 │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 1   │ Onboarding                     │ signup — creates the tenant before any login   │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 2   │ Auth                           │ login/logout — everything needs a token first  │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
│ 3   │ Organization                   │ the tenant; settings, the org itself           │
├─────┼────────────────────────────────┼────────────────────────────────────────────────┤
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
2. `GET /auth/me` — get the user and their switcher list (org and/or stores).
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
  verification), the chosen plan, and a first store. Sends a verification email and returns no token.
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

### Get current user (me)

- **Description:** Returns the logged-in user and the list of places they can act — the organization
  and/or stores — as lightweight entries (id, name, type, role), without permissions. An org user gets
  the organization plus every store in it; a store user gets only the stores they belong to.
- **Method:** `GET`
- **URL:** `/auth/me`
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

## Organizations

The organization is the tenant. There's exactly one per caller, and the `{organizationId}` in the path
must match the caller's org (from the token) — any other id returns `404`. The resource, the caller's
permissions, and the plan's enabled features are three separate endpoints.

### Get organization

- **Description:** Returns the organization — its name, plan, default store, and settings.
- **Method:** `GET`
- **URL:** `/organizations/{organizationId}`
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
    "createdAt": "2026-01-05T12:00:00Z"
  }
  ```

- **Errors:**
  - `401` — not authenticated
  - `403` — lacks `organization:read`
  - `404` — the `organizationId` is not the caller's org

### Get my organization permissions

- **Description:** Returns the permissions the caller holds in the organization. Used by the UI to show
  or hide org tabs and actions.
- **Method:** `GET`
- **URL:** `/organizations/{organizationId}/permissions`
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
  - `404` — the `organizationId` is not the caller's org

### Get organization features

- **Description:** Returns the **organization-scoped** feature codes the org's plan enables (e.g. billing,
  multi-store). Used by the UI to show or hide org-level features. Store-scoped features are not returned
  here — a store reads its own via `GET /stores/{storeId}/features`.
- **Method:** `GET`
- **URL:** `/organizations/{organizationId}/features`
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
  - `404` — the `organizationId` is not the caller's org

---

## Stores

A store is where selling happens. Store endpoints live under `/stores/{storeId}/...`, and the store must
belong to the caller's org — any other id returns `404`.

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
