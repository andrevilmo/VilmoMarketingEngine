# User stories — login, roles, provisioning, sales, NF-e, labels

This document is the **depth plan** for identity, company/vendor provisioning, **company stock + sale price**, inbound NF-e ingest, sales, outbound invoice, and shipping labels. It extends [PLAN.md](./PLAN.md) and [MarketPlaceEngine.MD](./MarketPlaceEngine.MD). No application code in this revision.

UI copy in Portuguese; API codes in English.

| Login level | Profile | After login, sees |
| --- | --- | --- |
| **Admin** | `IsPlatformSuperUser` (`admin@vilmomkt.com`) | All companies, all users, all sales. Can **create companies** and **create users** (company + vendor). |
| **Company** | `CompanyAdmin` on `UserCompany` | Everything of **that CNPJ**: vendors, marketplace links, **all their sales**, **stock and sale prices**. Can **create vendor users** for this company. |
| **Vendor** | `UserProfile.Vendor` | Own marketplace links and **own sales** (status, items, NF-e, labels). Cannot create users or companies. **No stock, no sale price, no NF-e ingest.** |

`Operator` / `Viewer` are staff profiles (warehouse / read-only), not a fourth login persona.

Index: [US-01](#us-01--login-as-a-user) login · [US-02](#us-02--show-information-at-my-user-level) home by level · [US-03](#us-03--admin-creates-a-company-ready-to-operate) admin creates company · [US-08](#us-08--admin-creates-users) admin creates users · [US-09](#us-09--admin-creates-a-company-user-with-marketplace-access) admin creates company user · [US-10](#us-10--admin-creates-a-vendor-user-and-related-marketplace-users) admin creates vendor · [US-11](#us-11--company-user-creates-vendors-and-checks-their-sales) company creates vendors · [US-12](#us-12--vendor-checks-own-sales-and-status) vendor sales · [US-13](#us-13--admin-and-company-see-stock-and-set-sale-price) stock + sale price · [US-14](#us-14--ingest-nf-e-by-cnpj-and-chave-to-increase-stock) NF-e ingest to stock · [US-04](#us-04--sales-stay-in-sync-common-table--per-marketplace-attributes)–[US-07](#us-07--after-preparando-para-envio-print-correios-format-shipping-label) sales / NF-e / label. Paid sales **decrease company stock** (US-13).

**Screens:** [WIREFRAMES.md](./WIREFRAMES.md). **First company seed:** [FirstCompany.md](./FirstCompany.md).

---

## US-01 — Login as a user

**As** any registered person  
**I want** to sign in with my email and password  
**So that** the API knows who I am and which level to apply (admin, company, or vendor).

### Flow

```
GET  /          → Metronic branded sign-in (demo1)
POST /auth/login  { email, password }
        │
        ├── 401 InvalidCredentials (same message for unknown email / bad password)
        ├── 403 UserDisabled
        └── 200 { accessToken, expiresIn, user }
                    │
                    ▼
JWT claims: user_id, email, is_platform_super_user,
            memberships[{ companyId, cnpj, legalName, profile }]
                    │
                    ├── Admin     → /companies  (US-02)
                    ├── Company   → /dashboard  that CNPJ (US-02)
                    └── Vendor    → /sales      mine (US-12)
```

### Acceptance

- One login screen for all levels. No separate “admin URL”.
- Email is unique globally. Password hashed (Argon2id or ASP.NET Identity defaults). Never returned.
- Seeded admin: `admin@vilmomkt.com`, password from gitignored secret.
- Token: bearer JWT, short TTL (e.g. 8h) + optional refresh. Sent as `Authorization: Bearer`.
- Active company: `X-Company-Id`. Admin may send any company id. Company/vendor may send only a membership; otherwise `403`.
- One membership and not admin: header optional (that company is implied).
- Many memberships and not admin: header required (`400 CompanyRequired`).
- Logout: `POST /auth/logout` revokes refresh; access token dies at expiry (or denylist if we add one).
- Forgot password: `demo1` reset-password pages; email token. Out of band (no SMTP in v1? — plan a provider; until then admin can `POST /users/{id}/reset-password`).
- Lockout after N failures (e.g. 5 / 15 min). Audit `login_attempts`.
- Idempotency-Key is **not** required on login.

### UI

Metronic `demo1/authentication/branded/sign-in.html`. After success, store token in memory (or httpOnly cookie via BFF). Header shows name + level badge (**Admin** / **Empresa** / **Vendedor**) + company switcher when applicable.

---

## US-02 — Show information at my user level

**As** a logged-in user  
**I want** the home screen and every list to match my level  
**So that** I never see another CNPJ’s data (unless I am admin) and a vendor never sees another vendor’s sales.

### What each level sees (home + menus)

| Area | Admin | Company | Vendor |
| --- | --- | --- | --- |
| Companies | All. Create/edit | Own CNPJ only (read) | Hidden |
| Users | All, filtered by selected company | Users of this CNPJ | Hidden |
| Create company | Yes (US-03) | No | No |
| Create company user | Yes (US-09) | No | No |
| Create vendor | Yes, for selected company (US-10) | Yes, for **my** company (US-11) | No |
| Marketplaces catalog | Yes (`POST /marketplaces`) | No | No |
| Company marketplace apps | Yes (selected company) | Yes (this CNPJ) | Read own shop links only |
| Products / **stock** / sale price | Selected company | **This CNPJ only** | **Hidden** |
| Ingest NF-e (CNPJ + chave) | Yes | Yes (own CNPJ locked) | **Hidden** |
| Advertisements | Selected company | This CNPJ | Own ads only |
| Sales | Selected company, or admin search-all | **All vendors** of this CNPJ | **Own** sales only |
| Sale status, NF-e, label | Same scope | All of this CNPJ | Own sales |
| A1 certificate | Yes | Yes | No |

Wrong-tenant read: **`404`**, never `403` with a leaked id.

### Home widgets

- **Admin** (after picking a company, or a global strip): companies count, users count, open sales by status, marketplaces with broken tokens.
- **Company**: sales by status (all vendors), **stock alerts**, vendors pending marketplace link, NF-e pending.
- **Vendor**: my sales by status, my unpaid / paid / preparing for dispatch counts, my pending marketplace links.

Sidebar: [UI.md](./UI.md).

### API

`GET /me` → `{ user, level, memberships, readiness }` so the shell can hide menus without guessing.

---

## US-03 — Admin creates a company ready to operate

**As** admin  
**I want** to create a company with **every block required** so that company can post items, sync sales, check status, and send NF-e  
**So that** I am not chasing missing CNPJ, A1, or marketplace apps after go-live.

A company is not “created” when only a name exists. Creation is a **wizard** that records readiness flags. The row can be saved as `Draft`; **operate** (publish, sync, invoice) requires the matching flag.

### Wizard (one `Idempotency-Key` for the whole POST, or one key per step with `companyId`)

#### Step 1 — Legal identity (required to save)

| Field | Rule |
| --- | --- |
| `legalName` | Razão social, required |
| `tradeName` | Nome fantasia, optional |
| `cnpj` | 14 digits, unique, checksum. **This is the tenant.** |
| `ie` | Inscrição estadual (required to emit NF-e; may be `ISENTO` where legal) |
| `im` | Optional |
| `email`, `phone` | Contact |
| Address | street, number, complement, neighborhood, city, UF, **CEP 8 digits** |
| `status` | `Draft` / `Active` / `Disabled` |

`POST /companies` → `201` + `companyId`. Duplicate CNPJ + same idempotency key → replay. Different payload → `409`.

#### Step 2 — Fiscal (required to **send invoices**)

| Field | Why |
| --- | --- |
| A1 PKCS#12 + password | DFe.NET `NFeAutorizacao` and DistDFe. `POST /companies/{id}/certificate` |
| `nfeSerie`, next `nNF` | `company_nfe_series` |
| `taxRegime` | Simples / Lucro Presumido / Real (tax profile defaults) |
| Default CFOP intra / interstate | 5102 / 6102 unless overridden per SKU |
| `nfeEnvironment` | Homologation / Production |

Without A1: `ready_to_invoice = false`. Ingest XML still allowed; emit button hidden.

#### Step 3 — Selected marketplaces (required to **post items** and **check sales**)

Admin checks which channels this company will use (`MercadoLivre`, `Shopee`, `Shein`, `Magalu`, …). For **each selected** `code`:

1. Insert `company_marketplace_config` (`is_enabled = true`).
2. Collect **company app** parameters from `marketplace_parameter_definition` where `scope = company` (ClientId, PartnerId, secrets). Secrets never in git.
3. Start OAuth / shop authorize when the protocol needs it (`GET /marketplaces/{code}/connect?companyId=`).
4. Show webhook URL `https://<public>/webhooks/{code}` (must already ACK 200).

Until tokens exist for a selected code: that code is `PendingConnect`. Listings and sale sync for that code stay disabled.

Omitted `marketplaceCodes` on create → no channels yet (`ready_to_list = false`). Admin can add them later with `PUT /companies/{id}/marketplaces/{code}`.

#### Step 4 — Optional first company user

Same payload as [US-09](#us-09--admin-creates-a-company-user-with-marketplace-access) nested in the wizard, or a follow-up screen. Not required to persist the company.

### Readiness (stored on `companies` or computed)

| Flag | True when | Unlocks |
| --- | --- | --- |
| `ready_to_list` | ≥1 marketplace **Linked** (app tokens) + address | Publish advertisements / post items |
| `ready_to_sync_sales` | Linked marketplace + webhook configured | Import sales, show status |
| `ready_to_invoice` | A1 valid for this CNPJ + IE + series + dest CEP rules | **Emitir NF-e** |
| `ready_to_operate` | all three | Full process |

UI: traffic-light on the company card. Clicking a red flag opens the missing step.

### Why this is “all information”

| Process | Needs |
| --- | --- |
| Post sales items | Company catalog (later) + linked marketplace apps + (usually) a vendor subaccount (US-10) |
| Check sales status | Webhooks + FetchOrder + canonical `SaleStatus` (US-04, US-05) |
| Send NF-e | A1 + emitente address/CNPJ/IE + sale dest/items (US-06) |

Admin can save a Draft missing A1; they cannot emit until fiscal is green.

### UI

Metronic settings form + integrations (marketplace checkboxes) + Dropzone for A1. Companies list: `demo1/account/members/teams.html`.

---

## US-08 — Admin creates users

**As** admin  
**I want** to create users and assign a level (company or vendor) on a company  
**So that** those people can log in (US-01) at the right level (US-02).

Admin does **not** create other platform super users from the UI (no second `IsPlatformSuperUser`). Support can seed that in the database if needed.

### Types

| Kind | Profile | Also runs |
| --- | --- | --- |
| Company user | `CompanyAdmin` | [US-09](#us-09--admin-creates-a-company-user-with-marketplace-access) |
| Vendor user | `Vendor` | [US-10](#us-10--admin-creates-a-vendor-user-and-related-marketplace-users) |
| Staff | `Operator` / `Viewer` | Membership only; no marketplace shop |

### Common fields

`email` (unique), `name`, `phone`, `password` **or** invite link, `companyId` (must exist), `profile`.

`POST /users` or dedicated routes below. Idempotent per `(companyId, email, profile)`.

Invite: user sets password on first login (`sign-up` branded page with token). Until then `status = Invited`.

---

## US-09 — Admin creates a company user with marketplace access

**As** admin  
**I want** a company user created **with everything needed to operate that company on the selected marketplaces**  
**So that** they can post items, watch sales, and send NF-e for that CNPJ without a second setup pass.

A **company user** is not a vendor. They use the **company’s** marketplace apps (`company_marketplace_*`). They see **all vendors** of that CNPJ.

### Request

`POST /companies/{companyId}/users` + `Idempotency-Key`

Admin UI: **Empresa** is a select of existing companies (same control as US-10), then e-mail/name, then marketplace checkboxes of **that** company. Do not type the company CNPJ.

```
{
  "email", "name", "password" | "invite": true,
  "profile": "CompanyAdmin",
  "marketplaceCodes": ["MercadoLivre", "Shopee"]   // selected; required non-empty
}
```

`marketplaceCodes` must already be **enabled** on the company (US-03 step 3). Unknown code → `400`. Company missing that config → `400 CompanyMarketplaceNotEnabled` (admin must finish US-03 first).

### Side effects (same transaction + outbox for OAuth)

```
User + UserCompany (CompanyAdmin)
        │
        ▼
for each selected marketplace_code:
    user_company_marketplace
        unique (company_id, user_id, marketplace_code)
        status = Linked if company tokens exist
               | PendingConnect if company app still needs OAuth
        │
        ▼
If company tokens missing: enqueue Connect for that code (company-level, not a vendor shop)
```

`user_company_marketplace` is **which channels this company user may operate**. Tokens still live on `company_marketplace_parameter` (one app per company per code). The company user does not get a personal ML shop.

### “All information on marketplaces”

For each selected code, the company user is ready when:

| Needed | Where |
| --- | --- |
| App credentials (ClientId, PartnerId, …) | Company parameters |
| Access / refresh tokens or HMAC shop | After OAuth / authorize |
| Webhook receiving | Platform URL (already) |
| Fiscal (to send NF-e) | Company A1 (US-03) — not copied onto the user |

If the company is not `ready_to_invoice`, the user is still created; UI shows the same traffic lights. Creating the user does **not** skip A1.

### Acceptance

- User can log in (US-01) as **Company** (US-02).
- They see all sales of that CNPJ.
- They can create vendors (US-11).
- They cannot `POST /companies`. They cannot see another CNPJ.
- Retry same idempotency key does not duplicate membership or `user_company_marketplace` rows.
- Same email as vendor in **another** company is allowed (different `UserCompany`). Same email as another user globally is **not** (one `User` row, extra membership if we allow multi-company; v1: one profile per user per company, one email globally).

### UI

Members datatable + form: company picker, profile = Empresa, marketplace checkboxes (only codes enabled on that company). Connect buttons per pending channel.

---

## US-10 — Admin creates a vendor user and related marketplace users

**As** admin  
**I want** to **select a company that already exists** and then create a vendor **linked to that company**, with shops on the marketplaces that company already enabled  
**So that** I never re-type the company's CNPJ or address on the vendor form.

The vendor does **not** own the CNPJ. The company (US-03) already has razão social, fantasia, CNPJ, address, A1. Creating a vendor only **chooses** that row.

### UI (admin) — company first

1. Required control **Empresa** — searchable select of companies already in the database (`GET /companies`). Each option shows **nome fantasia**, **CNPJ formatted**, status (Ativa/Rascunho).
2. Typing a CNPJ in a free-text box is **wrong**. There is no “CNPJ da empresa” input on this screen.
3. If the list is empty: disable submit and show **Cadastrar nova empresa** (US-03). You cannot create a vendor without a company.
4. If the header company switcher already has a company: pre-select it; admin may change it.
5. After a company is selected: load **that** company’s enabled marketplaces and show only those checkboxes. Codes not enabled are hidden or disabled (“não habilitado nesta empresa”).
6. Vendor **person** fields: e-mail, nome, telefone, optional **CPF/CNPJ da pessoa** (seller document — may differ from the company CNPJ), optional address. Label must say **do vendedor**, not da empresa.
7. Submit: `POST /companies/{companyId}/vendors` where `{companyId}` is the **selected** company’s id (never parsed from a typed CNPJ).

Company-user variant: [US-11](#us-11--company-user-creates-vendors-and-checks-their-sales) — same form, **Empresa** is read-only (their membership). No picker of other CNPJs.

Marketplaces do **not** let us “sign up a seller” with a silent API in production (ML/Shopee/SHEIN/Magalu require the human OAuth / shop authorize). “Create a related user on the marketplace” means:

1. Create the Vilmo vendor **under the selected `companyId`**.
2. Create a **subaccount row** per selected code (`user_detail_marketplace`).
3. Open the official **connect** flow so the remote seller/shop user is **linked** (store `SellerId` / `ShopId` / tokens on that row).
4. Optional: Mercado Livre **test users** in sandbox via a seed operation binding — never against production CNPJ.

### Request

`POST /companies/{companyId}/vendors` + `Idempotency-Key`

`{companyId}` **must already exist**. Unknown id → `404`. Admin may pick any company. Company user: only their membership (US-11).

```
{
  "email", "name", "password" | "invite": true,
  "detail": { "legalName", "documentType", "document", "phone",
              "address": { ... CEP 8 digits } },   // vendor person, not company
  "marketplaceCodes": ["MercadoLivre", "Magalu"]   // subset of the selected company's enabled codes
}
```

Empty `marketplaceCodes` → `400 MustSelectMarketplaces` (or `"*"` for all enabled on **that** company).

Codes not enabled on the **selected** company → `400`. Do not offer them in the UI.

### Side effects

```
User + UserCompany (Vendor)
        │
        ▼
users_detail  unique (company_id, user_id)
        │
        ▼
for each selected marketplace_code:
    user_detail_marketplace
        unique (company_id, user_id, marketplace_code)
        link_status = PendingConnect
        parameters = {}
        │
        ▼
outbox: marketplace.connect_vendor { companyId, vendorUserId, code }
        → connect URL for that vendor (uses company app creds + vendor OAuth)
```

When OAuth completes (`GET /oauth/{code}/callback` with state = company + vendor):

- Store vendor `AccessToken` / `ShopId` / `UserId` on `user_detail_marketplace`.
- `link_status = Linked`.
- `DEL` Redis `vendor-marketplace:{companyId}:{userId}:{code}`.

Until `Linked`, that vendor cannot publish to that code; sales webhooks for an unknown shop stay parked.

### Acceptance

- Vendor logs in as **Vendor** (US-12). Sees only own sales.
- Company (and admin) see this vendor under the CNPJ and **all of his sales** once they exist.
- Second POST with same idempotency key does not create a second Vilmo user or extra subaccounts.
- Adding a marketplace later: `PUT .../vendors/{id}/marketplaces/{code}` creates the missing subaccount + connect URL (same uniqueness).
- Document (CPF/CNPJ) of the **vendor person** may differ from the **selected company** CNPJ. NF-e **emitente** remains the **company** CNPJ (US-06).
- UI never asks for the company CNPJ as a text field. Selecting another company changes the marketplace checkbox set.

### UI

First control: **Empresa** select (admin) or locked company (company user). Then vendor person fields. Then marketplace checkboxes **of that company**. Connect per code (`PendingConnect` / `Linked` / `Error`).

---

## US-11 — Company user creates vendors and checks their sales

**As** a logged-in **company** user  
**I want** to create vendor users **linked to my company** and see **all of their sales**  
**So that** I run my CNPJ without calling the platform admin.

### Create vendor

Same body and side effects as [US-10](#us-10--admin-creates-a-vendor-user-and-related-marketplace-users), except:

- `companyId` is **always** the active membership. The **Empresa** field is visible but **read-only** (fantasia + CNPJ). No dropdown of other companies. Sending another company id → `403`.
- `marketplaceCodes` ⊂ this company’s enabled codes.
- Cannot create a vendor on another company.
- Cannot create a company user or a platform admin.

Route: `POST /vendors` (company implied) or `POST /companies/{myId}/vendors`.

### Check all his sales

- `GET /sales` with no vendor filter → **every** vendor of this CNPJ.
- `GET /sales?vendorUserId={id}` → that vendor only (must belong to this company).
- Sale detail, status chips, Emitir NF-e, Imprimir etiqueta: allowed for **any** sale of this CNPJ (US-05–US-07).
- Vendor list shows link_status per marketplace.

### Acceptance

- Company user created by US-09 can perform US-11 without admin.
- Sales of vendor A and vendor B both appear on the company sales screen.
- Vendor A still cannot see vendor B (US-12).

---

## US-12 — Vendor checks own sales and status

**As** a logged-in **vendor**  
**I want** to see my sales, their canonical status, marketplace extras, NF-e, and labels  
**So that** I fulfill my orders.

### Acceptance

- Home = **My sales** (`GET /sales`, server forces `vendor_user_id = me`).
- Filters: status, marketplace, date. Passing another `vendorUserId` → `403`.
- Detail: common fields + EAV accordion + raw remote status (US-04, US-05).
- Buttons: **Emitir NF-e** / **Imprimir etiqueta** on **own** sales only, same guards as US-06/US-07 (company A1 still signs).
- **My marketplaces**: list `user_detail_marketplace` with `link_status` (no secrets). Can click Connect if `PendingConnect`.
- Cannot: create users, create companies, list other vendors, change company A1 or company app ClientId.
- **Cannot** open **Estoque**, set **preço de venda**, or ingest NF-e (US-13, US-14). Menu hidden; API `404`.

### UI

Sidebar: Dashboard, My sales, My advertisements, My marketplaces, Profile.

---

## US-13 — Admin and company see stock and set sale price

**As** an **admin** or **company** user  
**I want** a page of **actual on-hand stock** for the company, where I can **view and set the sale price** of each product  
**So that** vendors sell from company inventory at prices we control, and a vendor never sees or edits another company’s (or the company’s) stock book.

### Who

| Level | Sees | Edits sale price |
| --- | --- | --- |
| **Admin** | Stock of the **selected** company (`X-Company-Id`). Switcher required. | Yes |
| **Company** | Stock of **their CNPJ only**. No other company. | Yes |
| **Vendor** | Nothing. No menu, `GET /inventory` → `404`. | No |

Stock is **company-owned**, not vendor-owned. Several vendors of the same CNPJ sell from the same `inventory_balance` rows.

### Screen — Estoque (Portuguese)

DataTable, one row per SKU of that company:

| Column | Source |
| --- | --- |
| SKU | `product.sku` unique `(company_id, sku)` |
| Descrição | `product.name` (from NF-e `xProd` on first ingest, editable later) |
| EAN / NCM | identifiers |
| **Saldo** | `inventory_balance.on_hand` — actual count |
| **Preço de venda** | `product.sale_price` (BRL). Inline edit or drawer. Required before publish ads |
| Última entrada | last inbound NF-e chave (short) |

Empty company: CTA **Ingerir NF-e** (US-14).

`PUT /products/{sku}/sale-price` `{ "amount": 129.90, "currency": "BRL" }` + `Idempotency-Key`. Admin/Company only. `400` if amount ≤ 0.

This price is the default unit price on advertisements and on outbound NF-e `vUnCom` unless a sale line already has a marketplace-agreed price.

### When a sale becomes Paid — decrease stock

Canonical `Paid` (US-05) **decreases company on-hand by the sale quantity**. Same transaction / same worker as the status write:

```
status → Paid
  for each sale_items line:
    INSERT InventoryMovement kind=SalePaid
      unique (company_id, sale_id, sku)
    on_hand -= qty
```

- Retry / second webhook must **not** subtract twice (unique movement key).
- `on_hand` must not go negative. If qty > on_hand: keep the sale import, do **not** set `Paid` (or roll back that status write); set attribute `stock_short = true` and leave previous status. UI chip **sem estoque**. Company/admin must ingest stock (US-14) or adjust; then `POST /sales/{id}/commit-stock` retries the decrement.
- **Cancelled** / **Returned** after a successful `SalePaid` posts `SalePaidReversal` (+qty). Unique `(company_id, sale_id, sku, SalePaidReversal)`.
- Outbound NF-e (US-06) does **not** subtract quantity again. It only stores the fiscal XML. The stock effect of the sale already happened at `Paid`.

Fan-out: after a successful decrement, enqueue `stock.publish.requested` so marketplace listings of that company get the new qty.

### Acceptance

- Vendor JWT cannot `GET /inventory` or `PUT` sale-price (`404`).
- Company A cannot see company B SKUs (`404`).
- Admin without `X-Company-Id` on this screen → `400 CompanyRequired`.
- Same sale becoming Paid twice does not double-decrement.
- Setting sale price does not change on-hand.

### UI

Metronic DataTables (demo1 members table). Price as currency input. Sidebar **Estoque** for Admin and Company only.

---

## US-14 — Ingest NF-e by CNPJ and chave to increase stock

**As** an **admin** or **company** user  
**I want** a screen where I enter the **company CNPJ** and the NF-e **chave de acesso** so Vilmo reads the XML from SEFAZ (Receita / DistDFe) and **adds the items to this company’s stock**  
**So that** catalog and quantities come from real inbound invoices, not from typing products by hand.

**Vendor: hidden.** Same as US-13.

### Screen — Ingerir NF-e

| Field | Admin | Company user |
| --- | --- | --- |
| **CNPJ** | Searchable **Empresa** select (fantasia + CNPJ). Sets `X-Company-Id`. Do not type a CNPJ that is not a tenant. | **Locked** to their company CNPJ (read-only). |
| **Chave de acesso** | 44 digits, checksum. Required. | Same |
| XML file | Optional fallback Dropzone if DistDFe cannot return the XML | Same |

Submit: `POST /nfe/chaves/{chave}/ingest` + `Idempotency-Key`. Body may include `cnpj` only as a check; it **must** equal `Company.Cnpj` for the active company (`400 CnpjMismatch` otherwise).

vilmo-nfe: DistDFe `consChNFe` with **that company’s A1**. Parse `det/prod`. For inbound CFOP (purchase / return to us as dest): create/update `product` by `(company_id, EAN)` else `(company_id, cProd + emit CNPJ)`; `InventoryMovement` kind `NfeInbound` unique `(company_id, chave, n_item, NfeInbound)`; **on_hand += qCom**.

Same chave ingested twice → replay, no second increase.

If this CNPJ is emitente of a **sale** NF-e, **do not** add qty (US-06 / CFOP policy). This screen is for **filling stock**, i.e. inbound documents where the company is destinatário (or a documented return). Show the classified movement in the result: `+N` / ignored / error.

Without A1: DistDFe fails with `CertificateNotConfigured`; XML upload still allowed if emit/dest CNPJ matches the company.

### Acceptance

- Company user cannot ingest into another CNPJ (locked field + server check).
- Admin ingest uses the selected company’s cert, never a neighbour’s.
- Vendor cannot open the page (`404`).
- After success, US-13 table shows new/updated SKUs and higher **Saldo**.
- Unknown chave / SEFAZ timeout → row `nfe_documents.status = Failed` with `xMotivo`; saldo unchanged.

### UI

Layout-1 form: CNPJ (select or locked) + chave 44 + Ingerir + Dropzone XML. Status list of recent chaves for **this company only**.

---

## Binding (company CNPJ owns the legal process)

```
Admin
  └── creates Company (CNPJ)           US-03
        ├── company_marketplace_*      selected channels (apps)
        ├── A1 + series                invoices
        ├── Company user               US-09  → user_company_marketplace
        └── Vendor users               US-10 / US-11
              ├── users_detail
              └── user_detail_marketplace (related marketplace user/shop)
                    └── sales (vendor_user_id)
```

Webhook: resolve shop/seller → `user_detail_marketplace` → vendor + company. Unknown shop → park. Never attach to a random vendor.

---


## US-04 — Sales stay in sync: common table + per-marketplace attributes

**As** a company or vendor  
**I want** every marketplace order stored as one common sale plus extra fields only when the channel needs them  
**So that** screens, NF-e, and labels share one model, and Shopee/ML-only data does not pollute the common row.

### Why two tables (not “dump JSON”)

Marketplaces disagree on dozens of fields (ML `pack_id`, Shopee `package_number`, Magalu `channel`, SHEIN `orderType`). They **agree** on buyer, address, amounts, items, payment, shipment identity. Canonical columns stay queryable and indexed. Channel leftovers are EAV rows (`field_name`, `field_value`) so adding Amazon does not `ALTER TABLE sales`.

### Common sale (`sales`)

One row per imported order. Unique `(company_id, marketplace_code, remote_order_id)`.

| Column | Meaning |
| --- | --- |
| `id` | Internal UUID (what the UI and NF-e use) |
| `company_id` | Tenant (CNPJ owner) |
| `vendor_user_id` | Seller who owns the subaccount |
| `marketplace_code` | String PK from `marketplace` (not an enum) |
| `remote_order_id` | Id on the channel |
| `remote_pack_id` | Optional pack/shipment grouping |
| `status` | **Canonical** `SaleStatus` (see US-05) |
| `remote_status` | Last raw status string from the channel |
| `remote_substatus` | Last raw substatus (ML shipment substatus, etc.) |
| `currency` | `BRL` |
| `items_total`, `shipping_total`, `discount_total`, `grand_total` | Money (numeric 18,2) |
| `paid_at`, `cancelled_at` | Timestamps |
| `buyer_name` | Full name or razão social |
| `buyer_document_type` | `CPF` / `CNPJ` |
| `buyer_document` | Digits only |
| `buyer_email`, `buyer_phone` | Optional |
| `recipient_name` | May equal buyer or a gift recipient |
| `recipient_street`, `recipient_number`, `recipient_complement` | Unit/apt |
| `recipient_neighborhood`, `recipient_city`, `recipient_uf`, `recipient_cep` | CEP 8 digits |
| `recipient_country` | Default `BR` |
| `sender_snapshot_json` | Frozen company sender at import/invoice time (name, CNPJ, full address, CEP) |
| `shipping_mode` | Canonical: `MarketplaceLogistics` / `SellerCorreios` / `SellerOther` |
| `carrier_name` | e.g. `Correios`, `Mercado Envios`, `Shopee Xpress` |
| `tracking_code` | When known |
| `nfe_document_id` | FK after emission |
| `label_id` | FK after print |
| `last_synced_at` | Worker clock |
| `created_at`, `updated_at` | |

`sender_snapshot_json` is filled from `Company` + `users_detail` (store name) so a later address change does not rewrite a printed label.

### Common sale items (`sale_items`)

Unique `(sale_id, line_no)`.

| Column | Meaning |
| --- | --- |
| `sku` | Company product SKU (matched; unmatched kept as `remote_sku`) |
| `remote_item_id`, `remote_sku`, `title` | Channel identity |
| `quantity`, `unit_price`, `line_total` | |
| `ncm`, `cfop_hint`, `ean` | Copied from catalog when matched; else from channel |

### Marketplace-specific attributes (`sale_marketplace_attributes`)

EAV. Unique `(sale_id, field_name)`.

| Column | Meaning |
| --- | --- |
| `sale_id` | FK `sales` |
| `marketplace_code` | Denormalized for queries |
| `field_name` | From `marketplace_sale_field_definition.field_name` (data, not enum) |
| `field_value` | Text (JSON string allowed for nested leftovers) |
| `is_secret` | Rare (tokens). UI masks |

Catalog of allowed names (seed, like marketplace parameters):

| Table | One job |
| --- | --- |
| `marketplace_sale_field_definition` | Per `marketplace_code`: `field_name`, `value_kind` (`string`/`json`/`money`/`datetime`), `is_required_on_import` |

Unknown `field_name` on write → `400`. New channel = new definition rows, no `ALTER` of `sales`.

Examples (seed, not columns on `sales`):

| code | field_name |
| --- | --- |
| `MercadoLivre` | `pack_id`, `shipment_id`, `shipping_substatus`, `logistic_type`, `need_invoice_xml`, `buyer_nickname` |
| `Shopee` | `shop_id`, `package_number`, `checkout_shipping_carrier`, `invoice_pending`, `booking_sn` |
| `Shein` | `order_type`, `package_invoice_status`, `warehouse_code` |
| `Magalu` | `channel`, `delivery_id`, `invoice_key_sent` |

### Sync flow

```
Webhook POST /webhooks/{code}     (ACK 200, enqueue)
        │
        ▼
Worker FetchOrder (generic binding)
        │
        ▼
ISaleNormalizer (JSON mapping in marketplace_operation_binding)
        │
        ├── UPSERT sales              unique (company_id, marketplace_code, remote_order_id)
        ├── REPLACE sale_items        for that sale
        ├── UPSERT sale_marketplace_attributes
        ├── Map remote status → SaleStatus (US-05)
        └── If buyer CEP invalid (not 8 digits) → sale stays importable, flag `address_incomplete`
```

Idempotency: `(company_id, marketplace_code, remote_order_id)` plus webhook `(company_id, marketplace_code, event_id)`. Re-delivery updates `remote_status`, attributes, and **may** advance canonical status only through the state machine (never backwards except `Cancelled` / `Returned` rules).

The engine **does not** persist the raw marketplace JSON as source of truth. Optional `sale_import_payloads` (debug, TTL 7 days, not shown in UI) is allowed for support; production screens read `sales` + attributes only.

---

## US-05 — One common status for every marketplace

**As** a user looking at a sale  
**I want** a single status that means the same on ML, Shopee, SHEIN, and Magalu  
**So that** buttons (emit NF-e, print label) appear for the same business moment on every channel.

### Canonical `SaleStatus`

| Code | PT (UI) | Meaning |
| --- | --- | --- |
| `PendingPayment` | Aguardando pagamento | Created, not paid |
| `Paid` | Pago | Paid / approved. **Emitir NF-e** is available |
| `Invoicing` | Emitindo NF-e | DFe.NET lote in flight |
| `InvoiceRejected` | NF-e rejeitada | SEFAZ rejected; user may retry emit |
| `PreparingForDispatch` | Preparando para envio | NF-e authorized. **Imprimir etiqueta para envio** is available |
| `LabelPrinted` | Etiqueta impressa | Label PDF stored; still can reprint |
| `Shipped` | Enviado | Tracking handed to carrier / marketplace |
| `Delivered` | Entregue | Final success |
| `Cancelled` | Cancelado | Terminal (no emit / no label) |
| `Returning` | Em devolução | Return in progress |
| `Returned` | Devolvido | Terminal |

`remote_status` + `remote_substatus` always remain visible on the sale detail (read-only chips) so support can debug the channel.

### Mapping (seed rows, not C# switch)

Table `marketplace_sale_status_map`: `(marketplace_code, remote_status, remote_substatus_or_null, sale_status)`.

| marketplace_code | remote (typical) | SaleStatus |
| --- | --- | --- |
| `MercadoLivre` | order `confirmed` / `payment_in_process` | `PendingPayment` |
| `MercadoLivre` | order `paid` | `Paid` |
| `MercadoLivre` | shipment `ready_to_ship` + `invoice_pending` | still `Paid` until **our** NF-e exists |
| `MercadoLivre` | shipment `ready_to_ship` after we uploaded XML | `PreparingForDispatch` or `LabelPrinted` |
| `MercadoLivre` | shipment `shipped` | `Shipped` |
| `MercadoLivre` | shipment `delivered` | `Delivered` |
| `MercadoLivre` | order `cancelled` | `Cancelled` |
| `Shopee` | `UNPAID` | `PendingPayment` |
| `Shopee` | `READY_TO_SHIP` / `PROCESSED` / `RETRY_SHIP` | `Paid` (until our NF-e) then `PreparingForDispatch` |
| `Shopee` | `SHIPPED` | `Shipped` |
| `Shopee` | `COMPLETED` / `TO_CONFIRM_RECEIVE` | `Delivered` (or `Shipped` until confirm — map `TO_CONFIRM_RECEIVE` → `Shipped`) |
| `Shopee` | `CANCELLED` / `IN_CANCEL` | `Cancelled` / keep `Paid` until confirmed cancel — prefer `IN_CANCEL` stay current + flag |
| `Magalu` | approved / paid | `Paid` |
| `Magalu` | invoiced | `PreparingForDispatch` if we emitted; else still `Paid` |
| `Magalu` | dispatched | `Shipped` |
| `Shein` | paid / awaiting shipment | `Paid` |
| `Shein` | shipped | `Shipped` |

**Rule:** marketplace “ready to ship / invoice pending” does **not** skip our NF-e. Canonical `Paid` means “money is in, we still owe an NF-e”. Only **our** successful `NFeAutorizacao` moves the row to `PreparingForDispatch`.

### State machine (writes)

```
PendingPayment → Paid | Cancelled
Paid           → Invoicing | Cancelled
Invoicing      → PreparingForDispatch | InvoiceRejected
InvoiceRejected→ Invoicing (retry) | Cancelled
PreparingForDispatch → LabelPrinted | Shipped (if channel marks shipped before print) | Cancelled
LabelPrinted   → Shipped | Cancelled
Shipped        → Delivered | Returning
Returning      → Returned | Delivered (rare)
```

Worker updates from webhooks **cannot** jump `Paid` → `Shipped` if `nfe_document_id` is null for seller-fulfill orders that legally need NF-e. Exception: marketplace fulfillment (fullfilment / Magalu fulfillment) where the channel invoices — flag `sale_marketplace_attributes.invoiced_by_marketplace = true`; then skip our emit button and allow `Paid` → `PreparingForDispatch` when the channel already has an invoice key.

---

## US-06 — When the sale is Paid, emit NF-e (DFe.NET) and move to Preparando para envio

**As** a company admin or the vendor of that sale  
**I want** a button **Emitir nota fiscal eletrônica** on a paid sale  
**So that** ZeusAutomacao/DFe.NET authorizes a model-55 NF-e with that sale's buyer/items and the sale becomes `PreparingForDispatch`.

This **replaces** the earlier “v1 does not emit NF-e” note. Inbound ingest (chave / XML / DistDFe) remains for **inventory**. This story is **outbound NF-e de saída** for a marketplace sale.

### Button visibility

| SaleStatus | Admin | Company | Vendor (own sale) |
| --- | --- | --- | --- |
| `Paid` or `InvoiceRejected` | Show **Emitir NF-e** | Show | Show |
| `Invoicing` | Disabled + spinner | same | same |
| others | Hidden | Hidden | Hidden |

### Data the NF-e must use (sale → DFe.NET)

| NF-e block | Source |
| --- | --- |
| `ide.cUF`, `ide.serie`, `ide.nNF` | Company fiscal config (UF from company address; next number from `company_nfe_series`) |
| `ide.natOp` | `Venda de mercadoria` (config) |
| `ide.tpNF` | `1` (saída) |
| `ide.tpAmb` | Homologation/Production from company |
| `emit` | **Company**: CNPJ, IE, razão social, full address, CEP, UF — **not** the vendor CPF unless the company *is* the vendor |
| `dest` | Common sale: `buyer_document` + `buyer_name` + recipient address (CEP 8 digits). Incomplete address → `400 AddressIncomplete`, do not call SEFAZ |
| `det/prod` | Each `sale_items` line: `cProd` = sku, `xProd`, `NCM`, `CFOP` (5102 same UF / 6102 interstate vs company UF), `qCom`, `vUnCom`, `vProd`, EAN if any |
| `det/imposto` | Tax profile on `Product` / company default (ICMS/PIS/COFINS). Missing NCM or tax profile → `400 TaxProfileMissing` |
| `transp` | `modFrete` from `shipping_mode` |
| `infAdic` | `marketplace_code` + `remote_order_id` (and pack id from attributes if present) |
| Certificate | `ICompanyCertificateStore.Get(companyId)` A1 of **that CNPJ** |

Library: [ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET).

Concrete calls (names from the library):

1. Build `NFe.Classes.NFe` from the sale (our mapper, not a Zeus UI).
2. `new ServicosNFe(ConfiguracaoServico)` with that company's A1 (`TipoCertificado.A1ByteArray` or file under `/certs/{cnpj}.pfx`).
3. `servicoNFe.NFeAutorizacao(loteId, IndicadorSincronizacao.Sincrono, list, compact: false)`.
4. On `cStat` authorized (100/150): persist `nfeProc` XML, chave 44, protocol. Link `sales.nfe_document_id`.
5. On rejection: `InvoiceRejected`, store `cStat` + `xMotivo` on `nfe_documents`; button stays **Emitir NF-e** (retry, new idempotency or same key with fingerprint of payload).
6. DANFE PDF optional later (`libgdiplus` on Linux if OpenFastReport). **Not** required to move status; XML authorization is.

Linux/Docker: `libgdiplus` only if we print DANFE. Emission + XML persist works without it.

### Flow

```
UI POST /sales/{saleId}/nfe  + Idempotency-Key
        │
        ▼
Authorize: Admin | CompanyAdmin of sale.company | Vendor of sale.vendor_user_id
        │
        ▼
Guard: status in (Paid, InvoiceRejected); company A1 present; dest CEP 8 digits; items matched
        │
        ▼
Redis lock  lock:nfe-emit:{companyId}:{saleId}
status → Invoicing
        │
        ▼
Stream nfe.emit.requested { companyId, saleId }
        │
        ▼
vilmo-nfe  (DFe.NET NFeAutorizacao, this company's A1)
        │
        ├── Authorized → save XML/chave,
        │                **no second stock decrement** (qty already left at Paid, US-13),
        │                status → PreparingForDispatch
        │                outbox: marketplace.upload_invoice { saleId }
        └── Rejected / timeout → InvoiceRejected (sale stays payable for retry)
```

Inventory: on-hand already decreased at `Paid` (`SalePaid`, US-13). Authorized NF-e stores XML/chave and may link `nfe_document_id` for audit — it does **not** post a second quantity movement.

After authorization the worker **uploads the XML** to the channel when the binding exists:

- Shopee: `v2.order.upload_invoice_doc` (`file_type=4`), wait ~5 min after SEFAZ (already in PLAN).
- Mercado Livre: invoice/XML when shipment is `ready_to_ship` + `invoice_pending` (`need_invoice_xml`).
- Magalu / SHEIN: operation binding `UploadInvoice` if the seed has it; else store XML and show “enviar XML” later.

Upload failure does **not** roll back the NF-e. Sale stays `PreparingForDispatch`; UI shows a warning chip **XML pendente no marketplace**.

Idempotency: unique `(company_id, sale_id)` on outbound `nfe_documents` of kind `OutboundSale`. Retry of the same HTTP key replays the stored result. A second emit on an authorized sale → `409 AlreadyInvoiced`.

---

## US-07 — After Preparando para envio, print Correios-format shipping label

**As** a company admin or the vendor of that sale  
**I want** a button **Imprimir etiqueta para envio**  
**So that** a 10×15 cm (or 13.8×10.6 cm) label goes to the printer with sender, recipient, and postal data, without covering the barcode or wrapping folds.

### Button visibility

Visible when `SaleStatus` is `PreparingForDispatch` or `LabelPrinted` (reprint). Hidden before NF-e. Disabled if `address_incomplete` or CEP ≠ 8 digits.

### Format (postal / e-commerce standard)

| Preference | Size | Notes |
| --- | --- | --- |
| **Default** | **10 × 15 cm** (100 × 150 mm) | Usual Correios PAC/SEDEX e-commerce label |
| Alternate | **13.8 × 10.6 cm** (138 × 106 mm) | Company setting `label_size` |

PDF page box **is** that size (not A4 with a small sticker). Thermal printers (Zebra / Elgin / Correios) receive the PDF or PNG at 203 dpi. Browser fallback: `@page { size: 100mm 150mm; margin: 0 }` and one label per page.

**Placement instructions** (shown next to the button, also printed as a one-line footer at ≤ 6 pt **outside** the barcode quiet zone, or only in the UI — do not clutter the barcode):

- Affix on the **largest side** of the box.
- Do **not** cover the barcode.
- Do **not** wrap around edges or folds.

### Required data on the label

**Recipient (customer)** — from common sale:

- Full name of the individual **or** company legal name (`recipient_name`)
- Street/avenue, number, unit/apartment (`recipient_street`, `recipient_number`, `recipient_complement`)
- Neighborhood (`recipient_neighborhood`)
- City and UF (`recipient_city`, `recipient_uf`)
- CEP **8 digits** (`recipient_cep`), printed as `00000-000` and encoded in the barcode when we generate Correios labels

**Sender (store)** — from company (CNPJ owner), snapshotted on the sale:

- Store name or legal business name (`Company.LegalName` / trade name)
- Full origin address (street, number, unit, neighborhood, city, UF)
- Origin CEP (8 digits)
- Sender **CNPJ** (company) or CPF if the legal entity were PF (Vilmo companies are CNPJ)

**Postal service:**

- Service name: PAC, SEDEX, Mercado Envios, Shopee Xpress, Magalu Entregas — from `carrier_name` / attributes
- Tracking code when already issued (marketplace or Correios PLP)
- Barcode (Code 128 or Correios DIGEP) of the tracking code; quiet zone respected
- NF-e chave (44) in small type **or** as a second barcode if the channel requires it on the package (Shopee/ML often want DANFE + label; chave on the label helps)

### Two label sources (KISS)

| `shipping_mode` | What we print |
| --- | --- |
| `MarketplaceLogistics` | Prefer **FetchShipmentLabel** binding (PDF from ML/Shopee/Magalu). If the PDF is already 10×15, store and print as-is. If the API only returns tracking + addresses, compose **our** Correios-layout PDF. |
| `SellerCorreios` | Our PDF from common sale + company sender. Tracking from Correios prepostagem/PLP when that integration exists; until then print **without** a fake tracking barcode (show “Coletar código nos Correios”) rather than inventing a CEP range barcode. |
| `SellerOther` | Same layout; barcode = `tracking_code` if present. |

Never draw a decorative barcode that does not scan.

### Persistence (`shipment_labels`)

| Column | Meaning |
| --- | --- |
| `id` | UUID |
| `sale_id`, `company_id` | |
| `format` | `Mm100x150` / `Mm138x106` |
| `pdf_bytes` or object-store key | Not git; volume or S3 later |
| `tracking_code` | |
| `printed_at`, `printed_by_user_id` | |
| `content_sha256` | Reprint same bytes unless addresses change (then new version) |

After first successful generate: `sales.status = LabelPrinted`, `sales.label_id` set. Reprint does not change status.

Idempotency: `POST /sales/{saleId}/label` unique per sale+format until invalidated. Same key → same PDF.

### API / UI

- `POST /sales/{saleId}/label` body `{ "format": "Mm100x150" }` → `202` then `GET /sales/{saleId}/label.pdf`
- UI: button **Imprimir etiqueta para envio** → download/open PDF → `window.print()` targeting the label iframe
- Company setting default format; user may pick the other size in a dropdown next to the button

### Printer

v1 does not talk IPP/raw ZPL unless we add it later. The operator prints the PDF from the browser or a PDF-to-thermal driver. The **layout contract** is the 100×150 (or 138×106) page, required fields, and barcode rules above.

---

## Authorization matrix (all stories)

| Resource | Admin | Company (same CNPJ) | Vendor (own) | Vendor (other in same CNPJ) |
| --- | --- | --- | --- | --- |
| Login / `GET /me` | yes | yes | yes | — |
| List companies | all | own membership | own membership | — |
| Create company | yes | no | no | no |
| Create company user | yes | no | no | no |
| Create vendor | yes (selected company) | yes (own CNPJ only) | no | no |
| Users of company | yes | yes | no | no |
| Vendor subaccounts | yes | yes | own only | no |
| Company marketplace apps | yes | yes | no | no |
| **Estoque + preço de venda** | selected company | **this CNPJ only** | **no** | no |
| **Ingerir NF-e** (CNPJ + chave) | yes | yes (own CNPJ) | **no** | no |
| Sales list | selected company / admin search-all | **all vendors** of CNPJ | own `vendor_user_id` | no |
| Emit NF-e | yes | yes | own sale | no |
| Print label | yes | yes | own sale | no |
| A1 | yes | yes | no | no |
| Marketplace catalog (`POST /marketplaces`) | yes | no | no | no |

Every query: `company_id` from context. Vendor queries add `vendor_user_id = me`. Super user still sends `X-Company-Id` for lists inside a CNPJ.

---

## API (identity + sales)

Mutating business calls: JWT + company context + `Idempotency-Key` (not on login/webhooks).

| Method | Path | Who | Behavior |
| --- | --- | --- | --- |
| `POST` | `/auth/login` | public | US-01 |
| `POST` | `/auth/logout` | any | |
| `GET` | `/me` | any | Level, memberships, readiness |
| `POST` | `/companies` | Admin | US-03 wizard (legal). Idempotent on CNPJ |
| `PUT` | `/companies/{id}` | Admin | Legal/fiscal fields |
| `POST` | `/companies/{id}/certificate` | Admin / Company | A1 |
| `PUT` | `/companies/{id}/marketplaces/{code}` | Admin / Company | Enable app + parameters (US-03 step 3) |
| `POST` | `/companies/{id}/users` | Admin | Company user + `user_company_marketplace` (US-09) |
| `POST` | `/companies/{id}/vendors` | Admin / Company (own id) | Vendor + related marketplace users (US-10, US-11) |
| `PUT` | `/companies/{id}/vendors/{userId}/marketplaces/{code}` | Admin / Company | Extra channel for existing vendor |
| `GET` | `/inventory` | Admin/Company | US-13 on-hand + sale price for active company. Vendor `404`. |
| `PUT` | `/products/{sku}/sale-price` | Admin/Company | Set BRL sale price. Idempotent. |
| `POST` | `/nfe/chaves/{chaveAcesso}/ingest` | Admin/Company | US-14. CNPJ must match company. Vendor `404`. |
| `POST` | `/nfe/xml` | Admin/Company | XML fallback. |
| `GET` | `/nfe/chaves/{chaveAcesso}` | Admin/Company | Ingestion status, this company only. |
| `POST` | `/sales/{id}/commit-stock` | Admin/Company | Retry SalePaid decrement after restock. |
| `GET` | `/sales` | Admin/Company: company. Vendor: own | Filter `status`, `marketplace_code`, `vendorUserId` |
| `GET` | `/sales/{saleId}` | Owner per matrix | Common + items + EAV + `remote_status` |
| `POST` | `/sales/{saleId}/sync` | Admin/Company | Enqueue FetchOrder |
| `POST` | `/sales/{saleId}/nfe` | Paid/InvoiceRejected | DFe.NET (US-06) |
| `GET` | `/sales/{saleId}/nfe` | | Outbound XML/chave |
| `POST` | `/sales/{saleId}/label` | PreparingForDispatch/LabelPrinted | US-07 |
| `GET` | `/sales/{saleId}/label.pdf` | | PDF |

Existing `GET` orders path in [UI.md](./UI.md) is this same resource (`sales`). Do not keep a second `orders` table.

---

## UI map (Metronic)

See [UI.md](./UI.md) for file sources. Behavior:

| Screen | Roles | Extra |
| --- | --- | --- |
| Sales list | All (scoped) | Status badges in PT. DataTables |
| Sale detail | All (scoped) | Common fields + accordion **Dados do marketplace** (EAV). Status chips: canonical + raw |
| Button Emitir NF-e | US-06 | Confirm modal: dest name, CNPJ/CPF, CEP, items, emitente CNPJ |
| Button Imprimir etiqueta para envio | US-07 | Size selector 10×15 / 13.8×10.6. Placement hint |
| Sign-in / home by level | US-01, US-02 | Badge Admin / Empresa / Vendedor |
| Create company wizard | US-03 | Admin. Readiness lights |
| Create company user | US-09 | Admin. Marketplace checkboxes |
| Create vendor | US-10, US-11 | Admin or Company. Selected marketplaces + Connect |
| Sidebar | US-02 | Admin / Company / Vendor menus |
| **Estoque** | US-13 | Admin + Company. Vendor hidden. Inline **preço de venda**. |
| **Ingerir NF-e** | US-14 | CNPJ select (admin) or locked (company) + chave 44 |

---

## Testing these stories (when coding)

- Unit: CEP 8 digits, status map, state machine (cannot emit from `PendingPayment`; cannot skip NF-e to `PreparingForDispatch` unless `invoiced_by_marketplace`).
- Login: wrong password `401`; vendor JWT cannot `POST /companies`; company JWT cannot create vendor for another CNPJ (`403`).
- Tenancy: vendor A cannot `GET` vendor B sale (404). Company A cannot see company B sale **or stock**. Vendor cannot `GET /inventory` (404). Admin with `X-Company-Id` of A sees A's sales and stock only in that list.
- Stock: ingest inbound NF-e twice does not double qty; Paid twice does not double-decrement; company B saldo unchanged.
- Price: `PUT` sale-price as vendor → 404; as company A on company B sku → 404.
- Provisioning: create vendor with two codes → two `user_detail_marketplace` `PendingConnect`; OAuth callback sets `Linked`. Same idempotency key does not duplicate. Create company user with a code not enabled on the company → `400`.
- Company readiness: no A1 → `ready_to_invoice = false`; emit returns `409 CertificateNotConfigured`.
- Import: fixture ML order → one `sales` row + attributes `shipment_id`; second webhook updates `remote_status` only.
- Emit: Testcontainers + stub `INfeAuthorizer` (do not call SEFAZ in CI); assert status `PreparingForDispatch` and XML stored; missing CEP → 400.
- Label: generated PDF page size 100×150 mm (±1 mm); contains recipient name, CEP, sender CNPJ; reprint same sha256.
- Idempotency: double-click Emitir NF-e does not send two lotes.

---

## Out of scope (still)

- Full Correios SIGEP/PLP contract in v1 (layout is in; posting ticket API can follow).
- NFCe (model 65), CT-e, MDF-e.
- Auto-print to a named IPP printer without the browser.
- Marketplace fulfillment warehouses that ship without our NF-e (we only skip emit when the attribute says the channel already invoiced).
