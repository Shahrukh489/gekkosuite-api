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
  verification), the chosen plan, and a first store. Sends a verification email. Returns **no token**;
  the owner must verify their email, then log in.
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
  account. After this, the owner can log in via `/auth/login`.
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

- **Description:** Exchanges an email and password for an access token. This is the entry point — the
  returned token is sent on every subsequent request.
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

- **Description:** Returns the logged-in user along with their memberships and the permissions they hold
  at each place. The UI calls this right after login to decide what to show — an `ORGANIZATION` membership
  unlocks the org context (Dashboard, Users, Settings…), while `STORE` memberships unlock those stores.
- **Method:** `GET`
- **URL:** `/auth/me`
- **Scope:** _(any authenticated user)_
- **Permission:** _(none)_
- **Request Headers:**
  - `Authorization: Bearer <accessToken>`
- **Request Body:** _(none)_
- **Response Status:** `200 OK`
- **Response Body:**

  ```json
  {
    "user": {
      "userId": "u1...",
      "name": "Maria",
      "email": "maria@acme.com"
    },
    "organizationId": "acme...",
    "organizationMembership": {
      "permissions": ["organization:read", "user:create", "store:create"]
    },
    "storeMemberships": [
      {
        "storeId": "s1...",
        "storeName": "Downtown",
        "permissions": ["product:read", "sale:create", "order:refund"]
      }
    ]
  }
  ```

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
