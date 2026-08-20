# Marketplace Engine — Opção A: Construção Própria (In-House)

This document is the architectural contract for Vilmo's marketplace layer. New marketplaces are **rows in PostgreSQL**, not a new enum, project, or deploy. The running API loads the definition from the database (cached in Redis) and executes it with a generic HTTP engine.

## Decision

**Opção A — In-House.** We own the canonical catalog, inventory, orders, and the anti-corruption layer that talks to each marketplace. We do **not** route through a third-party aggregator (API2Cart, Plugg.to, Tiny, Bling as the system of record).

Why this option:

- Inventory is born from Brazilian NF-e, not from a marketplace. An aggregator would invert that ownership.
- Auth, catalog, and stock **shapes** differ per channel. Those differences live in **marketplace definition tables** (URLs, auth protocol, parameter keys, JSON mappings), not in compiled C# per brand.
- Idempotency, CFOP-based stock movements, and A1 certificate handling are domain rules, not generic "sync SKU" calls.

What we still reuse: official SDKs/libraries only (DFe.NET for SEFAZ). Marketplace HTTP clients are ours.

## Multi-company tenancy (root of every record)

The system is for **N companies**. `CompanyId` is the tenant. Catalog, inventory, NF-e, marketplace shops, certificates, idempotency keys, and users never exist in a global bag.

| Concept | Meaning | Identity |
| --- | --- | --- |
| `Company` | Legal entity we operate for | Internal id + unique CNPJ |
| `User` | Person who signs in | Email |
| `UserCompany` | Membership of a user in a company | `(userId, companyId)` + `CompanyRole` |
| `CompanyCertificate` | A1 PKCS#12 used for that company's SEFAZ calls | `(companyId, cnpj)` |
| `CompanyMarketplaceConfig` | One marketplace enabled for one company (app/partner credentials) | `(companyId, marketplaceCode)` |
| `MarketplaceParameter` | Company-level config key/value | `(configId, parameterKey)` |
| `UserProfile` | How this user acts in the company | `Vendor`, `CompanyAdmin`, `Operator`, `Viewer` |
| `users_detail` | Vendor profile shared by all marketplace subaccounts | `(companyId, userId)` |
| `user_detail_marketplace` | That vendor's **subaccount** on one marketplace (key/value params, `link_status`) | `(companyId, userId, marketplaceCode)` |
| `user_company_marketplace` | Company user's selected channels (company app, not a shop) | `(companyId, userId, marketplaceCode)` |

Login personas (see [USER_STORIES.md](./USER_STORIES.md)):

| Persona | Who | Visibility | Creates |
| --- | --- | --- | --- |
| **Admin** | `IsPlatformSuperUser` | **All** companies, users, sales, **stock of selected CNPJ** | Companies, company users, vendors, **sale prices**, NF-e ingest |
| **Company** | `CompanyAdmin` of a CNPJ | **All users of that company**, **all of their sales**, **stock of this CNPJ only** | Vendors of **this** CNPJ, **sale prices**, ingest for this CNPJ |
| **Vendor user** | `UserProfile.Vendor` | Own marketplace links + **own sales** only | — **No stock, no sale price, no ingest** |

Login is `POST /auth/login` (US-01). Home and menus follow the level (US-02). Creating a company includes legal + selected marketplace apps + A1 so listings, sale status, and NF-e can run (US-03). Creating a vendor on **selected** marketplaces inserts `user_detail_marketplace` and **connects** the remote shop user (US-10); production APIs do not silently register a seller. Depth: [USER_STORIES.md](./USER_STORIES.md).

A vendor **belongs to the company by CNPJ** (`Company.Cnpj` unique). Marketplace links are `user_detail_marketplace` rows under that `company_id`. Sales attach to `vendor_user_id`. Cross-vendor reads inside the same CNPJ are allowed for Company and Admin, not for Vendor.

Rules:

- Every mutating API call runs inside a **company context**.
- Regular users see and change **only** companies they belong to.
- One platform super user (`admin@vilmomkt.com`) may see and act on **all** companies. That privilege is a `IsPlatformSuperUser` flag seeded for that email, not a string compare scattered in handlers.
- A user without membership and without super-user flag cannot call business APIs.
- Super user selects the target company with `X-Company-Id` (or an equivalent claim). Other users may send that header only if they are a member; otherwise `403`.

`CompanyRole` / `UserProfile` values: `CompanyAdmin`, `Operator`, `Viewer`, **`Vendor`**. No magic numbers. A vendor is a selling user of that company, not a second tenant.

## Canonical model (system of record)

Marketplaces never own product identity. The engine owns, **always scoped by `CompanyId`**:

| Concept | Meaning | Identity |
| --- | --- | --- |
| `Product` | Sellable item in that company's catalog (`sale_price` BRL) | `(companyId, sku)` |
| `ProductIdentifier` | EAN, `cProd` from NF-e, NCM, seller SKU | `(companyId, identifierKind, value)` |
| `InventoryBalance` | Quantity that company can sell, per warehouse | `(companyId, sku, warehouse)` |
| `InventoryMovement` | Immutable stock event (`NfeInbound`, `SalePaid`, `SalePaidReversal`, …) | NfeInbound `(companyId, chave, nItem, kind)`; SalePaid `(companyId, saleId, sku, kind)` |
| `Offer` | Price + allocated stock for one channel | `(companyId, sku, marketplace, vendorSubaccount)` |
| `Listing` | Remote advertisement published by a vendor subaccount | `(companyId, vendorUserId, marketplace, remoteListingId)` |
| `Sale` | Normalized marketplace order (common columns) | `(companyId, id)` unique remote `(companyId, marketplaceCode, remoteOrderId)` |
| `SaleItem` | Line of that sale | `(saleId, lineNo)` |
| `SaleMarketplaceAttribute` | Channel-only field (`field_name`, `field_value`) | `(saleId, fieldName)` |
| `SaleStatus` | Canonical status shared by all channels | See state machine below |
| `OutboundNfe` | NF-e de saída for a paid sale (DFe.NET) | `(companyId, saleId)` |
| `ShipmentLabel` | Correios-format PDF (100×150 or 138×106 mm) | `(saleId, format)` |
| `ConnectedAccount` | Company-level marketplace app config (not the vendor shop) | `(companyId, marketplace)` |

A company has **many marketplaces**. Each marketplace has **many vendor subaccounts**. Company app credentials live in `company_marketplace_*`. Vendor identity lives in `users_detail` + `user_detail_marketplace`. See **Vendors and marketplace subaccounts** below.

Mapping rule: **inbound** marketplace payloads become canonical objects via **stored JSON mappings**. **Outbound** commands are rendered from templates in the same tables. The engine never persists marketplace JSON as the source of truth. Workers always carry `CompanyId` and a **string** `marketplace_code` (from the `marketplace` table). A consumer must not infer company from a global default, and must not `switch` on marketplace names in C#.

## Data-driven marketplaces (no rebuild)

There is **no** `MarketplaceCode` enum. The code is a string primary key: `MercadoLivre`, `Shopee`, `Shein`, `Magalu`, later `Amazon`. Adding a channel is an INSERT (admin UI or SQL), then Redis `DEL`. The process is not rebuilt, no new project, no DI registration.

What the binary contains (stable, few):

| Type | One job |
| --- | --- |
| `IAuthProtocol` | Sign or attach credentials. Seeded implementations: `OAuth2AuthorizationCode`, `HmacSha256`, `BearerToken`, `ApiKeyHeader` |
| `IMarketplaceOperationExecutor` | Run one canonical operation (`PublishListing`, `UpdateStock`, `UpdatePrice`, `FetchOrder`, `UploadInvoice`, `FetchShipmentLabel`, `VerifyWebhook`) using a binding row |
| `IJsonMappingEngine` | Apply stored request/response mappings (JSON templates + JSONPath) |
| `IMarketplaceDefinitionReader` | Load definition from Redis/Postgres by `marketplace_code` |

What is **data** (no compile):

| Table | One job |
| --- | --- |
| `marketplace` | `code` (string PK), display name, `base_url`, `auth_protocol_code`, `is_active` |
| `marketplace_parameter_definition` | Allowed keys for company vs vendor (`ClientId`, `PartnerKey`, …), `is_secret` |
| `marketplace_operation_binding` | Canonical operation → HTTP method, path template, headers, body template, response mapping, timeout |
| `marketplace_webhook_binding` | How to read event id / shop id / resource from the payload |

`auth_protocol_code` must match a protocol **already compiled**. OAuth2 and HMAC cover Mercado Livre, Magalu, Shopee, and SHEIN. A future channel that needs a **new protocol** (for example AWS SigV4) is the only case that needs a code change — that change is a new `IAuthProtocol` class, not a marketplace project.

Generic HTTP routes, one pair for every code in the table:

- `GET /oauth/{marketplaceCode}/callback`
- `POST /webhooks/{marketplaceCode}`

Unknown `marketplaceCode` → `404`. No per-brand controllers.

Parameter keys are rows in `marketplace_parameter_definition`, not C# enums. A write with an unknown key for that code → `400`. New marketplace = new definition rows, no ALTER of other channels.

Liskov: every protocol and the executor return `Result<T>`. Timeouts, retries, and idempotency are the same for every `marketplace_code`.

Do not create `MercadoLivreConnector` or `ShopeeStockPublisher`. The four launch channels are **seed fixtures** of the tables above.

## Company marketplace configuration (Postgres + Redis)

Each company can connect to Mercado Livre, Shopee, SHEIN, Magalu, and later channels **at the same time**. Parameters differ per company and per marketplace (client id, partner key, shop id, `site_id`, tokens). They are **not** global.

PostgreSQL is the source of truth. The data almost never changes after setup, so **reads go through Redis**.

| Table | One job |
| --- | --- |
| `marketplace` | Channel catalog: string `code`, `auth_protocol_code`, `base_url`, `is_active` |
| `marketplace_parameter_definition` | Keys allowed for that code (company or vendor scope) |
| `marketplace_operation_binding` | How to call PublishListing / UpdateStock / … for that code |
| `company_marketplace_config` | This company uses this `marketplace_code` (`is_enabled`) |
| `company_marketplace_parameter` | One parameter row: key, value, `is_secret` |

Unique: `(company_id, marketplace_code)` on config; `(config_id, parameter_key)` on parameters; `marketplace.code` is the only identity of a channel.

`parameter_key` must exist in `marketplace_parameter_definition` for that code. Keys are data. Mercado Livre may have `ClientId` + `SiteId`; Shopee may have `PartnerId` + `PartnerKey`. Amazon later adds its own keys in new rows.

Secrets (`ClientSecret`, refresh tokens, SHEIN `secretKey`) are stored encrypted in Postgres (`is_secret = true`). Redis may cache the **resolved config DTO** for that company+marketplace; invalidate on any write.

```
ICompanyMarketplaceConfigReader.Get(companyId, marketplaceCode)
        │
        ▼
Redis GET marketplace-config:{companyId}:{marketplaceCode}
        │
        ├── hit  → return
        └── miss → Postgres (config + parameters)
                   Redis SET (no short TTL needed; delete on write)
```

Writes: Postgres first, then `DEL marketplace-config:{companyId}:{marketplaceCode}`. Never update Redis only.

Connectors receive `CompanyMarketplaceConfig` (app credentials) plus the vendor's `user_detail_marketplace` (subaccount). They must not read `IConfiguration["Shopee:PartnerId"]`. The executor looks up bindings by `marketplace_code` string.

## Vendors and marketplace subaccounts

Companies have users. Users with profile **`Vendor`** sell. Each marketplace must hold **multiple subaccounts** — one per vendor in that company — not one shared shop for the whole company.

```
Company
  └── many Vendors (users + users_detail)
        └── one subaccount per marketplace (user_detail_marketplace)
              └── advertisements / listings
```

| Table | One job |
| --- | --- |
| `users_detail` | Data **common to all** marketplace subaccounts of that vendor (legal name, document, phone, address, display name, …) |
| `user_detail_marketplace` | Data **specific to one marketplace** as parameter key/values (shop id, nickname, tokens, fulfillment flags, …) |

`users_detail` exists only for `UserProfile.Vendor`. Staff profiles do not get this row.

**Create vendor (idempotent, per company, selected marketplaces):**

1. Insert `User` + `UserCompany` with profile `Vendor`.
2. Insert `users_detail` unique `(company_id, user_id)`.
3. For **each selected** `marketplace_code` (must be company-enabled), insert `user_detail_marketplace` unique `(company_id, user_id, marketplace_code)` with `link_status = PendingConnect`.
4. OAuth/callback stores the remote shop/user ids and tokens → `Linked`. That is the related marketplace user.
5. Retry with the same `Idempotency-Key` + `CompanyId` must not create a second vendor or a second subaccount set.

Admin may do this for any company (US-10). Company users may do this only for their CNPJ (US-11).

If a marketplace is enabled on the company **later**, provision a missing `user_detail_marketplace` for every existing vendor of that company (same uniqueness; skip rows that already exist).

**Publish advertisement:**

- Vendor (or company admin acting for that vendor) chooses marketplaces.
- **Default = all** marketplaces where the company is enabled **and** the vendor has a subaccount.
- The request may pass an explicit list (`marketplaceCodes`). Empty/omitted list means all.
- Worker publishes **once per selected marketplace** using that vendor's `user_detail_marketplace` params + the company's app credentials.
- Listing uniqueness: `(company_id, vendor_user_id, sku, marketplace_code)`.

Vendor marketplace params are also cache-aside in Redis (`vendor-marketplace:{companyId}:{userId}:{marketplaceCode}`), invalidated on write — same pattern as company config.

## Sales (common + EAV) and canonical status

Webhooks never persist marketplace JSON as the source of truth. `FetchOrder` + stored JSON mappings fill:

1. **`sales`** — buyer, recipient address (CEP 8 digits), amounts, vendor, marketplace code, remote id, `SaleStatus`, tracking, sender snapshot (company CNPJ + address).
2. **`sale_items`** — sku/qty/price/NCM.
3. **`sale_marketplace_attributes`** — only what does not belong on the common row (`field_name` / `field_value`). Allowed names: `marketplace_sale_field_definition` (data).

Unique import: `(company_id, marketplace_code, remote_order_id)`. Re-delivery updates attributes and `remote_status`; canonical status follows `marketplace_sale_status_map` **and** the state machine (marketplace “invoice pending” ≠ our NF-e done).

When status becomes **`Paid`**, company **on-hand decreases by sale qty** (`InventoryMovement` `SalePaid`, unique per sale+sku). Outbound NF-e does not subtract again. Stock screens and ingest (CNPJ + chave) are **Admin and Company only**; vendors never see `inventory`. Depth: [USER_STORIES.md](./USER_STORIES.md) US-13, US-14.

`SaleStatus`: `PendingPayment` → `Paid` → `Invoicing` → `PreparingForDispatch` → `LabelPrinted` → `Shipped` → `Delivered` (+ `InvoiceRejected`, `Cancelled`, `Returning`, `Returned`).

Buttons:

- `Paid` / `InvoiceRejected` → **Emitir NF-e** (`NFeAutorizacao` with sale dest/items, company A1). Success → `PreparingForDispatch`.
- `PreparingForDispatch` / `LabelPrinted` → **Imprimir etiqueta para envio** (PDF 100×150 mm default, or 138×106 mm). Required: recipient name + full address + CEP; sender name + full address + CEP + CNPJ/CPF; postal service + real tracking barcode. Affix on largest side; do not cover barcode; do not wrap folds.

Visibility: Admin all; Company all sales of the CNPJ; Vendor `WHERE vendor_user_id = me`. Depth: [USER_STORIES.md](./USER_STORIES.md).

## Idempotency (mandatory, always keyed by company)

Every write that can be retried must be idempotent. Retries are normal (webhooks, SEFAZ timeouts, 429s). **Company A and Company B may send the same `Idempotency-Key`.** Those are different operations.

### HTTP API

- Client sends `Idempotency-Key` (UUIDv4) on every mutating request.
- Company comes from auth context (`CompanyId`), never from the key itself.
- Store in Redis: `SET idempotency:{companyId}:{idempotencyKey} NX EX 86400` with payload fingerprint + status (`Processing` / `Completed` / `Failed`).
- Same company + same key + same fingerprint → replay stored response.
- Same company + same key + different fingerprint → `409 Conflict`.
- Same key on another company → independent lock (no collision).
- Concurrent same company+key while `Processing` → `409` with retry-after.
- Missing company context → `401`/`403`, do not write a global key.

### Domain uniqueness (PostgreSQL)

| Operation | Natural key |
| --- | --- |
| Ingest NF-e | `(company_id, chave_acesso)` |
| NfeInbound movement | `(company_id, chave_acesso, n_item, NfeInbound)` |
| SalePaid movement | `(company_id, sale_id, sku, SalePaid)` |
| SalePaidReversal | `(company_id, sale_id, sku, SalePaidReversal)` |
| Connect shop (company app) | `(company_id, marketplace_code)` |
| Create vendor | `(company_id, user_id)` on `users_detail` |
| Create vendor subaccount | `(company_id, user_id, marketplace_code)` on `user_detail_marketplace` |
| Company user channel | `(company_id, user_id, marketplace_code)` on `user_company_marketplace` |
| Upsert listing | `(company_id, vendor_user_id, sku, marketplace_code)` |
| Import sale | `(company_id, marketplace_code, remote_order_id)` |
| Sale attribute | `(sale_id, field_name)` |
| Outbound NF-e | `(company_id, sale_id)` |
| Shipment label | `(sale_id, format)` until regenerated |
| Webhook | `(company_id, marketplace_code, event_id)` |
| Push stock | `(company_id, listing_id, command_id)` |
| HTTP idempotency audit | `(company_id, idempotency_key)` |

Redis is the short-window lock. PostgreSQL unique constraints are the source of truth. Never rely on Redis alone for money or stock. Never use a unique index on `chave_acesso` alone.

### Queue consumers

Redis Streams messages carry `MessageId` **and** `CompanyId`. Consumers use `XREADGROUP` and persist processed ids as `(company_id, message_id)`. Duplicate delivery is a no-op. A message without `CompanyId` is rejected, not processed under a fallback tenant.

## Event flow

```
Webhook / API command  (CompanyId from auth or ConnectedAccount)
        │
        ▼
   HTTP 200 fast ACK          ← Mercado Livre requires 200 within 500ms
        │
        ▼
   Redis Stream (command + CompanyId)
        │
        ▼
   Worker (idempotent handler, key = company + operation)
        │
        ├── Catalog / Inventory (PostgreSQL, row = that company)
        └── Outbox row (CompanyId)
                │
                ▼
         Redis Stream (integration + CompanyId)
                │
                ▼
         Generic executor (definition + company creds + vendor subaccount)
```

Webhooks never call marketplace APIs inside the HTTP request. They only validate, enqueue, and return.

## Adding a marketplace (no rebuild)

Super user (or SQL seed). Idempotent per `code`.

1. `POST /marketplaces` with `code`, name, `base_url`, `auth_protocol_code` (must already exist: `OAuth2AuthorizationCode`, `HmacSha256`, `BearerToken`, `ApiKeyHeader`).
2. Insert `marketplace_parameter_definition` rows (company + vendor keys).
3. Insert `marketplace_operation_binding` rows for the operations this channel supports. Unsupported operations stay absent; the executor returns `OperationNotBound` for that code only.
4. Insert `marketplace_webhook_binding` if the channel pushes events.
5. `DEL` Redis keys `marketplace-definition:{code}`.
6. Companies enable it: `PUT /companies/{id}/marketplaces/{code}` → backfill `user_detail_marketplace` for existing vendors.

OAuth callback and webhook URLs are already `/{marketplaceCode}/…`. No new route, no new container, no new project.

If the channel needs an auth protocol that is not compiled yet, add **one** `IAuthProtocol` class and redeploy **once**. That is a protocol extension, not a marketplace project.

Do not add `MarketplaceCode.Amazon` to C#. Do not add `Vilmo.Marketplace.Amazon`.

## Per-company SEFAZ certificate

Each company has its own A1 certificate bound to **that company's CNPJ**. `vilmo-nfe` loads the cert through `ICompanyCertificateStore.Get(companyId)` and passes it into DFe.NET. There is no process-wide `Nfe__CertificatePath`.

- One company, one active A1 (replace by uploading a new cert; old row is revoked, not edited in place if you need history).
- SEFAZ calls for company X never use company Y's cert.
- CNPJ on the certificate must match `Company.Cnpj`.
- `.pfx` bytes and passwords are secrets (Docker volume + encrypted password). They are not committed to git.

## What this engine does not do

- It does not scrape storefronts.
- It does not treat any marketplace as catalog master.
- It does not update stock inside a webhook handler.
- It does not embed marketplace ids in domain entities as primary keys.
- It does not keep a global inventory or a single shared A1 certificate.
- It does not keep marketplace client ids or partner keys in environment variables for each tenant. Those rows live in `company_marketplace_parameter`.
- It does not compile one C# project per marketplace or use a `MarketplaceCode` enum. Channels are table rows.
- It does not publish advertisements under a shared company shop when a vendor exists. Publish uses that vendor's `user_detail_marketplace` subaccount.
- It does not let a vendor read another vendor's sales in the same CNPJ. Company admin and platform admin may.
- It does not treat marketplace JSON as the sale source of truth. Common columns + EAV attributes only.
- It does not move a sale to `PreparingForDispatch` until **our** NF-e is authorized (unless `invoiced_by_marketplace`).
- It does not print an A4 page as the shipping label. Page box is 100×150 mm or 138×106 mm.
- It does not invent Correios tracking barcodes.
- It does not ship all Metronic demos or the React/Next packages as the running UI. HTML layout-1 + demo1 mapped pages only.
