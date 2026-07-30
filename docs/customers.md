# Customer data model: B2C + B2B

What fields a customer record actually needs, and why a flat "name / email / phone" row — which is
all `store_customer` has today — stops being enough once a **business** account (not just a person) can
be a customer. Nothing here is built yet; it's the plan, in the same spirit as `docs/returns.md`.


## Where things stand today

`store_customer` (the real, migrated table — `tools/GekkoSuite.Database/Migrations/1_0_0.sql`) is:

```sql
CREATE TABLE store_customer (
    store_customer_id UUID PRIMARY KEY,
    store_id          UUID NOT NULL REFERENCES store (store_id),
    organization_id   UUID NOT NULL REFERENCES organization (organization_id),
    name              VARCHAR(256) NOT NULL,
    email             VARCHAR(254),
    phone             VARCHAR(32),
    is_active         BOOLEAN NOT NULL,   -- opt-in to the store's loyalty/marketing list
    created_at        TIMESTAMPTZ NOT NULL,
    updated_at        TIMESTAMPTZ NOT NULL,
    is_deleted        BOOLEAN NOT NULL,
    deleted_at        TIMESTAMPTZ
);
```

`docs/database.md` also sketches a shared, org-wide `customer` table (one identity recognized across
every store, the way `docs/overview.md` describes: *"With sharing on, a new customer is also recognized
org-wide"*) — designed, not yet migrated. Everything below extends **that** shared shape, since a
business account in particular needs to be recognized wherever the org sells, not re-created per store.
`docs/returns.md`'s Phase 1a already added one field to it: `store_credit_balance`. This doc is the rest
of the identity.


## Why B2C and B2B can't share one flat shape

A B2C customer *is* one person — `name`/`email`/`phone` describes them completely. A B2B customer is a
**company**, and a company:

- has a **legal identity** distinct from any person (a registered business name, a tax ID) — you don't
  bill "Jane," you bill "Jane's employer."
- is bought from by **more than one person** — a purchasing department, not an individual — so "the
  customer's email" is ambiguous the moment more than one employee places orders on the account.
- often pays on **terms**, not at the register (net-30 invoicing, a credit limit, a required PO number
  on every order) — concepts that don't exist for a walk-in shopper.
- can be **tax-exempt** for resale, which has to be backed by an actual certificate on file, not just a
  checkbox — compliance, not preference.

Cramming all of that onto the same row a walk-in customer uses either bloats every B2C row with columns
that are always `NULL`, or — worse — gets built as loose free-text fields that don't hold up if the org
is ever audited on its exemption certificates. The fix, and it's the same pattern already used for
memberships (`membership` + `store_membership_detail` / `organization_membership_detail` in
`docs/database.md`): **one shared identity, a type flag, and a detail table for the type that actually
needs one.**


## Proposed shape

```sql
CREATE TYPE customer_type AS ENUM ('INDIVIDUAL', 'BUSINESS');

-- Core identity shared by every customer, individual or business. Extends the existing sketch in
-- docs/database.md (email, name, phone, created_at, soft-delete were already there).
CREATE TABLE customer (
    customer_id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id             UUID NOT NULL REFERENCES organization (organization_id),
    -- INDIVIDUAL | BUSINESS — decides whether customer_business_detail applies
    customer_type               customer_type NOT NULL DEFAULT 'INDIVIDUAL',
    -- display name: the person's name (INDIVIDUAL) or trade/display name (BUSINESS) — what shows in
    -- receipts and the UI either way
    name                        TEXT NOT NULL,
    -- primary contact email — a person's email for INDIVIDUAL; for BUSINESS this is often a shared
    -- inbox (billing@, orders@) rather than one employee's, since employees come and go (see
    -- customer_contact below for the actual humans on the account)
    email                       TEXT,
    phone                       TEXT,
    -- billing address — needed for invoicing (B2B) and for tax jurisdiction on any receipt
    address_line1               TEXT,
    address_line2               TEXT,
    city                        TEXT,
    state                       TEXT,
    postal_code                 TEXT,
    country                     TEXT,
    -- INDIVIDUAL-only: age-restricted sales (alcohol/tobacco/vape) and birthday promos. Sensitive PII —
    -- collect only when a store actually sells age-restricted goods, not by default (see Compliance below)
    date_of_birth               DATE,
    -- tax-exempt (resale, nonprofit, government). The certificate fields are what make this auditable
    -- rather than an honor-system checkbox — see Compliance below
    tax_exempt                  BOOLEAN NOT NULL DEFAULT FALSE,
    tax_exemption_certificate_number TEXT,
    tax_exemption_expires_at    TIMESTAMPTZ,
    -- running store-credit balance (docs/returns.md's Phase 1a) — org-wide like the identity itself
    store_credit_balance        NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- consent to marketing contact, with when it was given — the timestamp is what makes an opt-in
    -- defensible later, not just the flag (see Compliance below)
    marketing_opt_in            BOOLEAN NOT NULL DEFAULT FALSE,
    marketing_opt_in_at         TIMESTAMPTZ,
    -- free-text staff notes about the account
    notes                       TEXT,
    -- opt-in flag for the store's loyalty/marketing list (kept from the existing store_customer shape)
    is_active                   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted                  BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at                  TIMESTAMPTZ,
    UNIQUE (organization_id, email)
);
CREATE INDEX ON customer (organization_id);

-- BUSINESS-only fields, 1:1 with a customer row where customer_type = 'BUSINESS'. Same pattern as
-- store_membership_detail — an INDIVIDUAL customer simply has no row here.
CREATE TABLE customer_business_detail (
    customer_id              UUID PRIMARY KEY REFERENCES customer (customer_id),
    -- the registered legal name, which can differ from the display name (a "doing business as" trade name)
    legal_business_name      TEXT NOT NULL,
    -- EIN or local equivalent — needed for invoicing, 1099 reporting, and to verify a resale certificate
    -- actually belongs to this business
    tax_id                   TEXT,
    -- DUE_ON_RECEIPT | NET_15 | NET_30 | NET_60 — how this account is invoiced, if not paying at the till
    billing_terms            TEXT NOT NULL DEFAULT 'DUE_ON_RECEIPT',
    -- how much trade credit this account can carry before payment is required; NULL = no credit extended
    credit_limit             NUMERIC(12, 2),
    -- whether a sale to this account must reference a PO number (their AP process, not ours)
    purchase_order_required  BOOLEAN NOT NULL DEFAULT FALSE
);

-- The actual humans authorized to buy on a BUSINESS account — a purchasing department, not one person.
-- One is flagged primary for "who do we call about this account." Deactivating someone (they left the
-- company) doesn't delete the history of what they ordered.
CREATE TABLE customer_contact (
    customer_contact_id  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id          UUID NOT NULL REFERENCES customer (customer_id),
    name                 TEXT NOT NULL,
    title                TEXT,             -- e.g. 'Purchasing Manager'
    email                TEXT,
    phone                TEXT,
    is_primary           BOOLEAN NOT NULL DEFAULT FALSE,
    is_active            BOOLEAN NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted           BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at           TIMESTAMPTZ
);
CREATE INDEX ON customer_contact (customer_id);
-- at most one primary contact per business account (only live contacts count)
CREATE UNIQUE INDEX customer_contact_one_primary ON customer_contact (customer_id) WHERE is_primary AND NOT is_deleted;
```

**Why `date_of_birth` sits on the shared table but the business fields don't:** an individual needs
*one* extra field; a business needs several plus a whole one-to-many contacts list. One field doesn't
earn a detail table (simple over flexible); a company's legal identity and its purchasing department do.


## Field reference

### Shared by every customer

| Field | Why it's needed |
|---|---|
| `customer_type` | Decides whether `customer_business_detail` / `customer_contact` apply. |
| `name` | Display name — a person's name, or a business's trade name. |
| `email`, `phone` | Primary contact channel; also the identity key (`UNIQUE (organization_id, email)`). |
| `address_line1/2, city, state, postal_code, country` | Billing address — invoicing (B2B) and tax jurisdiction on any receipt. |
| `tax_exempt` + certificate fields | Resale/nonprofit/government exemption — see Compliance. |
| `store_credit_balance` | From `docs/returns.md` — a `STORE_CREDIT` refund lands here. |
| `marketing_opt_in` + `marketing_opt_in_at` | Consent to contact, with proof of when it was given. |
| `is_active`, soft-delete columns | Existing loyalty opt-in / removal pattern, unchanged. |

### INDIVIDUAL-only

| Field | Why it's needed |
|---|---|
| `date_of_birth` | Age-verification for alcohol/tobacco/vape sales, and birthday offers. Only collect it if a store actually sells age-restricted goods — see Compliance. |

### BUSINESS-only (`customer_business_detail`)

| Field | Why it's needed |
|---|---|
| `legal_business_name` | The registered entity being billed — may differ from the display `name`. |
| `tax_id` | EIN/equivalent — invoicing, 1099s, and verifying a resale certificate belongs to this business. |
| `billing_terms` | Whether this account pays at the till or on an invoice, and on what schedule. |
| `credit_limit` | How much trade credit the account can carry before payment is required. |
| `purchase_order_required` | Whether a sale must reference the customer's own PO number. |

### `customer_contact` (the authorized buyers on a BUSINESS account)

| Field | Why it's needed |
|---|---|
| `name`, `title` | Who this person is and their role (e.g. "Purchasing Manager"). |
| `email`, `phone` | Reaching this specific person, distinct from the account's general contact info. |
| `is_primary` | The one person to call about the account as a whole. |
| `is_active` | A contact who's left the company is deactivated, not deleted — their order history stays attributable. |


## Compliance notes

These are the parts that bite if skipped, not nice-to-haves:

- **Resale/exemption certificates must be verifiable, not an honor system.** `tax_exempt = TRUE` with no
  certificate on file is an audit finding waiting to happen. `tax_exemption_expires_at` matters as much
  as the flag — an expired certificate means tax should be charged again regardless of what the boolean
  says, so the checkout/return logic needs to check the expiry, not just the flag (same shape as
  `docs/returns.md`'s "the app enforces it, the column doesn't assume it" pattern).
- **Minimize PII, especially `date_of_birth`.** It's sensitive, and collecting it "just in case" is a
  liability with no offsetting benefit for a store that doesn't sell age-restricted goods. Gate the field
  behind an actual need, not a default on every signup.
- **Never store SSNs on the customer record.** One real, easy-to-miss compliance trap: a **cash**
  purchase (or related purchases) over $10,000 triggers a federal requirement (IRS Form 8300) to collect
  the payer's SSN/TIN and government ID and file a report. That's a rare, transaction-triggered event —
  it should be its own compliance record tied to the specific sale, **not** a field sitting on every
  customer row inviting casual over-collection. Treat as a Phase-later addition, scoped to the order that
  triggers it.
- **PCI-DSS: never store a raw card number**, on the customer record or anywhere else. If "card on file"
  for recurring B2B billing is ever wanted, that's a tokenized reference from the payment processor, the
  same "never the raw secret, only what proves it" principle `docs/auth.md` already applies to passwords.
- **Consent needs a timestamp, not just a flag.** `marketing_opt_in_at` is what makes an opt-in
  defensible under CAN-SPAM/TCPA-style rules later — "we have a boolean" isn't proof of when or that
  consent was actually given.
- **Soft-delete isn't erasure.** The existing `is_deleted`/`deleted_at` pattern keeps a row for history
  (matches every other soft-deletable table in `docs/database.md`), which is the opposite of what a
  GDPR/CCPA deletion request asks for. If this system ever needs to honor "delete my data" for real, that's
  a separate anonymization path (null out PII, keep the row's shape for referential integrity) — not
  something the current soft-delete flag does today, and worth flagging now rather than discovering it
  during an actual request.


## Open questions

1. **Does a BUSINESS customer bypass the per-store sharing toggle?** `docs/overview.md` describes
   customer sharing as an org setting, off by default — a customer is per-store unless the org turns
   sharing on. A business relationship arguably isn't a "per-store" thing the same way a walk-in is — the
   same company can reasonably buy from any of the org's stores. Worth deciding whether `customer_type =
   'BUSINESS'` should always be org-wide regardless of that toggle, or follow the same rule as everyone else.
2. **Does billing on terms (`NET_30`, etc.) need its own invoice/AR tracking**, or is `billing_terms` +
   `credit_limit` just informational until a real accounts-receivable feature exists? I'd treat actual
   invoice generation and aging as its own later feature, not implied by these two fields.
3. **Is a business ever also, separately, a walk-in individual** (an employee buying something personal
   at the same store)? If so, that's a different `customer` row entirely (different `customer_type`), not
   a special case of the business one — flagging so it isn't assumed away.


## Summary: build order

1. Migrate the shared `customer` table for real (it's currently sketch-only, per `docs/database.md`),
   with `customer_type` and the fields in the "Shared by every customer" table above.
2. Add `customer_business_detail` + `customer_contact` — only needed once a real B2B account shows up,
   so this can trail the core table by a step.
3. Wire the exemption-expiry check and the PCI/SSN guardrails into the checkout/return service methods
   as they're built (`docs/returns.md`), not as an afterthought once tax is already being charged wrong.
