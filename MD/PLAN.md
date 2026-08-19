# Plan — Marketplace API + NF-e Inventory

The nginx gateway Compose stack in [`deploy/docker-compose.yml`](../deploy/docker-compose.yml) is implemented (echo stubs behind slugs). Application code, .NET images, and certificates are not created until a later task explicitly asks to implement them.

Referenced architecture: [MarketPlaceEngine.MD](./MarketPlaceEngine.MD) — **Opção A (in-house)** + **idempotency**. Service map, data model, Redis resources, and sequence diagrams: [ARCHITECTURE.md](./ARCHITECTURE.md). Default UI: [UI.md](./UI.md) (Metronic 9.5.0 HTML). CNPJ seller + developer apps: [HowToCreateCnpjMarketplaceAccounts.md](./HowToCreateCnpjMarketplaceAccounts.md). Depth on login roles, sales sync, outbound NF-e, and Correios labels: [USER_STORIES.md](./USER_STORIES.md). First tenant: [FirstCompany.md](./FirstCompany.md).

---

## 1. Goal

A .NET API, run entirely from Docker Compose, that:

1. Serves **N companies**. Every business record belongs to a `CompanyId`. Users belong to companies. Users with profile **`Vendor`** sell through **per-marketplace subaccounts**. One platform super user sees all.
2. Owns a **per-company** product inventory derived from Brazilian **NF-e** (ingest by **chave de acesso**).
3. Talks to SEFAZ through [ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET). **Each company has its own A1 certificate** (CNPJ-bound). XML upload remains a fallback.
4. Each vendor publishes advertisements to **Mercado Livre**, **Shopee**, **SHEIN**, and **Magalu** using **their** subaccount on each channel (default: all marketplaces).
5. Accepts a fifth marketplace **without a rebuild**: insert a `marketplace` row + bindings + parameter definitions (admin UI). Existing vendors get a `user_detail_marketplace` when the company enables that code.
6. Ships a **Metronic 9.5.0 HTML** admin UI (default templates from `template-metronic/.../metronic-v9.5.0/`). See [UI.md](./UI.md).
7. Logs users in as **Admin** (see all), **Company** (see all of that CNPJ's users and sales), or **Vendor** (see only own sales and marketplace links).
8. Keeps marketplace orders in a **common `sales` table** plus **`sale_marketplace_attributes`** (`field_name`, `field_value`) for channel-only data. One **canonical `SaleStatus`** across ML, Shopee, SHEIN, Magalu.
9. On **Pago**, shows **Emitir nota fiscal eletrônica**: ZeusAutomacao/DFe.NET `NFeAutorizacao` with sale dest/items and the **company** A1. Success → `PreparingForDispatch` (Preparando para envio).
10. Then shows **Imprimir etiqueta para envio**: PDF **10×15 cm** (or 13.8×10.6 cm) with sender (company CNPJ + address), recipient (name, full address, CEP 8 digits), and postal/tracking data. Affix rules: largest side, do not cover barcode, do not wrap folds.

Idempotency is always `(CompanyId, key)`. See [MarketPlaceEngine.MD](./MarketPlaceEngine.MD) and [USER_STORIES.md](./USER_STORIES.md).

---

## 2. Why this shape (and not a pile of microservices)

KISS: four HTTP clients plus one fiscal SOAP client do not justify eight deployable services.

**Split only where failure domains differ.**

| Deployable | Why it exists |
| --- | --- |
| `vilmo-gateway` | Only public HTTP (`:80`). Path slugs `/web` `/api` `/nfe` `/worker`. |
| `vilmo-api` | Public HTTP via gateway `/api`: commands, OAuth callbacks, webhook ACK. Must answer fast. |
| `vilmo-web` | Metronic HTML UI at `/web`. Browser calls `/api` on the same origin. |
| `vilmo-worker` | Slow marketplace I/O, retries, stock fan-out. Internal + slug `/worker`. |
| `vilmo-nfe` | DFe.NET: DistDFe ingest **and** `NFeAutorizacao` emit, **per-company** A1, SEFAZ rate limits. Isolated so a SEFAZ outage does not take the API down. Slug `/nfe`. |
| `postgres` | Source of truth. |
| `redis` | Idempotency keys, marketplace-config cache, **vendor-subaccount cache**, **Redis Streams**. |

One process per marketplace would duplicate auth, idempotency, and outbox. That violates SRP at the *system* level. Marketplace differences live in **definition tables**, not in extra containers or extra C# projects.

Redis is enough as the queue for this scope. Do not add RabbitMQ until stream lag or multi-consumer topology actually hurts. Redis also already covers `SET NX EX` for HTTP idempotency.

---

## 3. Official developer documents (connected for this plan)

### 3.1 Mercado Livre (Mercado Libre)

| Topic | Source |
| --- | --- |
| Portal | https://developers.mercadolivre.com.br |
| OAuth access token | https://developers.mercadolivre.com.br/pt_br/publicacao-de-produtos/obtencao-do-access-token |
| Notifications | https://developers.mercadolivre.com.br/en_us/users-addresses/products-receive-notifications |
| Distributed stock | https://developers.mercadolivre.com.br/pt_br/pt_br/estoque-distribuido |
| Fulfillment stock | https://developers.mercadolivre.com.br/pt_br/envio-de-produto/envios-fulfillment |
| Platform hygiene / 429 | https://developers.mercadolivre.com.br/pt_br/envio-de-produto/boas-praticas-para-usar-a-plataforma |

Facts the **seed definition** (table rows) must encode:

- Brazil `site_id` = `MLB`. Auth URL for Brazil: `https://auth.mercadolivre.com.br/authorization`. Token: `POST https://api.mercadolibre.com/oauth/token`. Access token ~6 hours; refresh with `offline_access`.
- REST + Bearer token. Publish via `POST /items`. Stock is **not** a single field: User Products + `x-version` optimistic concurrency; `PUT .../stock/type/seller_warehouse` when `warehouse_management` is on; fulfillment uses inventory ids.
- Notifications: configure callback; topics `items`, `orders_v2`, `shipments`, `stock_locations`, `payments`. **HTTP 200 within 500 ms** or Mercado Livre disables the topic. Payload is a pointer (`resource`); the worker must GET the resource.
- Rate limit: treat `429` as backpressure, never as a retry storm.

### 3.2 Shopee Open Platform

| Topic | Source |
| --- | --- |
| Portal | https://open.shopee.com |
| Intro / Brazil | https://open.shopee.com/developer-guide/4 |
| Auth / token | https://open.shopee.com/documents/v2/v2.public.get_access_token?module=104&type=1 |
| Update stock | https://open.shopee.com/documents/v2/v2.product.update_stock?module=89&type=1 |
| Push (webhooks) | https://open.shopee.com/developer-guide/18 |
| Brazil NF-e upload | https://open.shopee.com/developer-guide/382 |
| Upload invoice API | https://open.shopee.com/documents/v2/v2.order.upload_invoice_doc?module=94&type=1 |

Facts the **seed definition** (table rows) must encode:

- Brazil live base: `https://openplatform.shopee.com.br/api/v2/`.
- Every call: `partner_id`, `timestamp`, HMAC-SHA256 `sign`, plus `access_token` + `shop_id` for shop APIs. Access token **4 hours**, refresh **30 days**.
- Stock: `v2.product.update_stock` updates **seller_stock** only; one `item_id` per call, up to 50 `model_id`s. Respect reserved promotion stock.
- Push: HTTP POST to our callback; verify HMAC; then GET the resource. Codes include order status (3), tracking (4), reserved stock (8), auth expiry (12).
- Brazil: CNPJ sellers must upload NF-e XML (`file_type=4`) via `v2.order.upload_invoice_doc` when status is invoice-pending. Wait ~5 minutes after NF-e authorization (SERPRO). This is **outbound invoice**, distinct from inbound inventory NF-e.

Partner eligibility for Brazil is gated (registered business + recent orders). Sandbox exists; CI must use fixtures, not the live partner API.

### 3.3 SHEIN Open Platform

| Topic | Source |
| --- | --- |
| Portal | https://open.sheincorp.com/en |
| Signature helper | https://open.sheincorp.com/documents/system/passwdrule |
| Production API | `https://openapi.sheincorp.com` |

Facts the **seed definition** (table rows) must encode:

- Access is **application + review**. Full OpenAPI and webhook schemas sit behind login. First implementation step is an approved Open Platform account, not code.
- Auth: seller authorization → `get-by-token` using **APP_ID / APP_Secret** to mint `openKeyId` + `secretKey`. Later calls sign with HMAC-SHA256 over `openKeyId`, timestamp, URL path, and a random key; headers `x-lt-openKeyId`, `x-lt-timestamp`, `x-lt-signature`.
- Documented solution areas: product publish, SHEIN-fulfill orders, seller-fulfill orders (tracking), stock-preparation orders, webhooks.
- Until the gated catalog is available, seed SHEIN with `is_active = false` (or with no operation bindings). Other codes stay usable. Fill SHEIN bindings later via admin UI, without a rebuild.

### 3.4 Magalu (Magazine Luiza)

| Topic | Source |
| --- | --- |
| Portal | https://developers.magalu.com |
| Create application | https://developers.magalu.com/docs/first-steps/create-an-application/ |
| OAuth | https://developers.magalu.com/docs/first-steps/create-an-application/authentication-authorization |
| Products / portfolio | https://developers.magalu.com/docs/apis/products/overview |
| API catalog | https://developers.magalu.com/docs/apis/ |

Facts the **seed definition** (table rows) must encode:

- OAuth 2.0 Authorization Code via **ID Magalu**. Token: `POST https://id.magalu.com/oauth/token`. Access ~7200s; refresh token rotation. Seller must log in as **PJ (store)**, not PF.
- App credentials via ID Magalu CLI (`client_id` / `client_secret`) plus scopes.
- Portfolio is three resources: **SKU**, **Stock**, **Price** (separate write scopes). Stock is not a field on the SKU payload.
- Scopes we need at minimum: `open:portfolio-skus-seller:read|write`, `open:portfolio-prices-seller:read|write`, `open:portfolio-stocks-seller:read|write`, plus order read/write when orders go live.
- Webhooks for SKU and order events. Same rule: ACK then enqueue.
- Sandbox uses the same methods with a sandbox channel; product scopes must be enabled on the sandbox client.

### 3.5 NF-e / SEFAZ via DFe.NET

| Topic | Source |
| --- | --- |
| Library | https://github.com/ZeusAutomacao/DFe.NET |
| DistDFe WSDL wrapper | https://github.com/ZeusAutomacao/DFe.NET/blob/master/NFe.Wsdl/DistribuicaoDFe/NfeDistDFeInteresse.cs |
| Consulta protocolo (status only) | `ServicosNFe.NfeConsultaProtocolo(chave)` — 44 chars |
| National spec | NT 2014.002 — `NFeDistribuicaoDFe` (`distNSU`, `consNSU`, `consChNFe`) |

Facts the fiscal module must encode:

- **Chave de acesso** = 44 digits. Validate length, numeric charset, and check digit before any network call.
- `NfeConsultaProtocolo` returns **situation** (`cStat`, protocol), **not** the XML with items. Inventory **cannot** be built from this call alone.
- Full XML comes from `NFeDistribuicaoDFe` with `consChNFe`, and only if our CNPJ is emitente, destinatário, transportador, or `autXML`. Documents older than ~90 days may be unavailable. `consChNFe` is rate-limited (**about 20/hour**). Preferred production sync is `distNSU`, with `consChNFe` for the explicit API "read this chave".
- Destinatário often receives a **summary** first; **Ciência da Operação** (manifestação) may be required before the full XML. Plan a `NfeManifestation` step, not a single GET.
- A1 certificate (PKCS#12) is required for SEFAZ. Until it exists: accept XML upload + chave, parse with DFe.NET (`nfeProc.CarregarDeArquivoXml` / string), and keep `ISefazDocumentFetcher` as a stub that returns `CertificateNotConfigured`.
- DFe.NET on Linux: load **that company's** A1 with `X509Certificate2` from a mounted secret path resolved by CNPJ. Never bake `.pfx` into the image. Never use a single process-wide certificate.

---

## 3.6 Companies, users, and the first tenant

The product is multi-company from day one. There is no "default company" fallback in handlers.

### Identity model

| Type | One job |
| --- | --- |
| `Company` | Legal name, unique CNPJ, status (`Active` / `Disabled`) |
| `User` | Email, credentials, `IsPlatformSuperUser` |
| `UserCompany` | Links a user to a company with `UserProfile` / `CompanyRole` |
| `CompanyCertificate` | Active A1 for SEFAZ for that company only |
| `CompanyMarketplaceConfig` | This company × this marketplace (app/partner credentials) |
| `CompanyMarketplaceParameter` | Company-level config key/value (cached in Redis) |
| `users_detail` | Vendor profile **common to all** marketplace subaccounts |
| `user_detail_marketplace` | That vendor's subaccount on one marketplace (key/value params, `link_status`) |
| `user_company_marketplace` | Company user's selected channels (uses company app tokens) |
| `ICompanyContext` | Resolved once per HTTP request / queue message |

`CompanyRole` / `UserProfile`: `CompanyAdmin`, `Operator`, `Viewer`, **`Vendor`**.

**Login personas (US-01, US-02, US-08–US-12):**

| Persona | Profile | Sees | Creates |
| --- | --- | --- | --- |
| Admin | `IsPlatformSuperUser` | All companies, all users, all sales | Companies, company users, vendors on a selected company |
| Company | `CompanyAdmin` | All users of **this CNPJ**, **all of their sales** | Vendors linked to **this** company only |
| Vendor user | `Vendor` | Own marketplace links + **own sales** | Nothing (users/companies) |

Login is one screen (`POST /auth/login`). `GET /me` returns the level so the Metronic shell hides menus. Depth: [USER_STORIES.md](./USER_STORIES.md).

Vendors sell. Each vendor is **linked to selected marketplaces** via `user_detail_marketplace` (`PendingConnect` until OAuth links the remote shop user) and **belongs to the company by CNPJ**. Company users operate **company apps** via `user_company_marketplace` (not a personal shop). Sales attach to `vendor_user_id`.

A company created by admin is only **ready to operate** when legal + selected marketplace apps + A1/series are in place (`ready_to_list`, `ready_to_sync_sales`, `ready_to_invoice`). See US-03.

Authorization:

- Every user **must** be linked to at least one company **except** the platform super user, who may have zero memberships and still operate.
- A user may belong to several companies. They pick the active one (`X-Company-Id` or `company_id` claim).
- If a non-super user omits company context and has exactly one membership, use that company. If they have many, require an explicit company id (`400`).
- Queries, uniqueness, Redis keys, streams, and certificate lookup all use that `CompanyId`.
- Cross-company reads are forbidden except for the super user listing companies or acting with an explicit `X-Company-Id`.

### Platform super user

| Field | Value |
| --- | --- |
| Email | `admin@vilmomkt.com` |
| Role | Platform super user (`IsPlatformSuperUser = true`) |
| Visibility | All companies, all NF-e, all inventory, all marketplace accounts |
| Seed | Created on first migrate/bootstrap; password from gitignored secret, not from source |

Do not hard-code the email in application services. Seed it once; authorize via the flag. The email is the documented bootstrap identity.

### First company (seed)

Source: RFB cartão CNPJ **CNPJ EMPRESA NOVA** + A1 PKCS#12 for this CNPJ. Full record: [FirstCompany.md](./FirstCompany.md) and [seed/first-company.json](./seed/first-company.json).

| Field | Value |
| --- | --- |
| Nome fantasia (UI) | **VILMO COMERCIO, REPRESENTACOES E INFORMATICA** |
| Razão social (legal / NF-e / A1) | **A. VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA** |
| CNPJ | `68431371000161` |
| Porte / natureza | ME · 206-2 Sociedade Empresária Limitada |
| Situação | ATIVA (opened 2026-08-04) |
| CNAE principal | 47.81-4-00 vestuário e acessórios |
| Address | R VITOR KONDER, 223, SALA 1108, CENTRO, FLORIANÓPOLIS/SC, CEP **88015400** |
| Contact | andre.vilmo@gmail.com · (51) 8022-7183 |
| A1 | e-CNPJ A1, CN `…TECNOLOGIA LTDA:68431371000161`, SAN email `admin@vilmomkt.com`, valid 2026-08-13 → 2027-08-13. **File + password only in gitignored `.secrets/`** |

On implement: seed this company from `MD/seed/first-company.json`, mount `.secrets/certs/68431371000161.pfx` into `vilmo-nfe` as `/certs/68431371000161.pfx`, and optionally link `admin@vilmomkt.com` as `CompanyAdmin` **in addition to** the platform flag so the same login works without sending `X-Company-Id` when only this company exists. IE is not on the cartão; collect it before production emit if SC requires it.

### Per-company A1 (SEFAZ)

`ICompanyCertificateStore.Get(CompanyId)` returns the PFX + password for DFe.NET. `ISefazDocumentFetcher` takes `CompanyId` + `ChaveAcesso`. Company 68431371000161's cert is never used for another CNPJ.

**Do not commit `.pfx` files or certificate passwords.** The operator already has a local A1 for CNPJ `68431371000161`. When coding starts, map it into the `vilmo-nfe` container as a read-only file named by CNPJ, for example:

```
# gitignored .env — see .env.example
COMPANY_68431371000161_A1_HOST_PATH=.secrets/certs/68431371000161.pfx
COMPANY_68431371000161_A1_PASSWORD_FILE=.secrets/certs/68431371000161.pfx.pass
```

Compose mounts the host file to `/certs/68431371000161.pfx` inside `vilmo-nfe`. The database row stores `company_id`, CNPJ, container path, and the password **encrypted**, not plaintext in appsettings.

The host file is the operator A1 for this CNPJ (PKCS#12). Keep it at gitignored `.secrets/certs/68431371000161.pfx`. The original download was named with the razão social and the password in the filename — **never commit that name or the password**. Docker Compose override (also gitignored) points at `.secrets/certs/`. Original PFX uses RC2-40-CBC; OpenSSL 3 / Linux needs `-legacy` or a re-exported AES PFX.

If a company has no cert yet: ingest via XML upload still works; DistDFe returns `CertificateNotConfigured` for that company only.

### Company × many marketplaces (config table + Redis cache)

One company sells on several channels at once. Mercado Livre credentials for company A are not Shopee's, and they are not company B's. **All of those parameters live in PostgreSQL.** The generic executor never reads tenant secrets from `appsettings` or Docker env (env is only for Postgres/Redis URLs, public callback base URL, and bootstrap admin password).

| Table | Columns (intent) |
| --- | --- |
| `marketplace` | string `code` PK (not a C# enum), `auth_protocol_code`, `base_url`, `is_active` |
| `marketplace_parameter_definition` | allowed keys per code, scope (`company` / `vendor`), `is_secret` |
| `marketplace_operation_binding` | canonical operation → HTTP call + JSON mapping |
| `marketplace_webhook_binding` | how to parse event id / shop id from the payload |
| `company_marketplace_config` | `company_id`, `marketplace_code`, `is_enabled` |
| `company_marketplace_parameter` | `config_id`, `parameter_key`, `parameter_value`, `is_secret` |

Uniques: `marketplace.code`; `(company_id, marketplace_code)`; `(config_id, parameter_key)`.

Launch **seed data** (SQL fixtures, not C# projects): `MercadoLivre`, `Shopee`, `Shein`, `Magalu`. Typical keys remain data:

| `marketplace.code` | Typical parameter keys |
| --- | --- |
| `MercadoLivre` | `ClientId`, `ClientSecret`, `AccessToken`, `RefreshToken`, `UserId`, `SiteId` (`MLB`) |
| `Shopee` | `PartnerId`, `PartnerKey`, `ShopId`, `AccessToken`, `RefreshToken` |
| `Shein` | `AppId`, `AppSecret`, `OpenKeyId`, `SecretKey` |
| `Magalu` | `ClientId`, `ClientSecret`, `AccessToken`, `RefreshToken`, `Scope` |

Amazon later is another `code` + keys + bindings. No enum, no new project.

Values with `is_secret = true` are encrypted at rest in Postgres.

**Redis cache (config almost never changes):**

- Key: `marketplace-config:{companyId}:{marketplaceCode}`
- Value: resolved DTO the connector needs (including decrypted secrets in memory only after load)
- Fill: cache-aside on read (`GET` → miss → Postgres → `SET`)
- Also cache `marketplace-definition:{code}` (bindings + parameter definitions); `DEL` when a super user updates the catalog
- Invalidate: `DEL` that key after any INSERT/UPDATE/DELETE of config or parameters
- No short TTL required; TTL is optional (e.g. 24h) as a safety net if a delete is missed
- Do not use Redis as the writer. Postgres commits first.

`ICompanyMarketplaceConfigReader.Get(companyId, marketplaceCode)` and `IMarketplaceDefinitionReader.Get(marketplaceCode)` are the only way the executor loads settings. Missing config → `MarketplaceNotConfigured` for that company+channel; other codes of the same company keep working. Unknown `marketplace_code` → `404`/`400`, never a compile-time enum miss.

OAuth/token refresh **writes** new `AccessToken` / `RefreshToken` parameter rows (or updates them), then invalidates the Redis key. That is the exception to "config does not change": tokens rotate; shop ids and partner ids do not.

Company-level rows are **app/partner credentials** (one per company per marketplace). They are not the vendor shop. Vendor shops are `user_detail_marketplace` subaccounts.

### Vendors, `users_detail`, and subaccounts

Many companies. Each company has many users with profile **`Vendor`**. Each marketplace has **many subaccounts** (one per vendor). A vendor publishes the same product to many marketplaces **using their subaccount on each channel**.

| Table | Holds |
| --- | --- |
| `users_detail` | Common vendor data reused on every marketplace: legal name, document (CPF/CNPJ), phone, address, display name, default description, photos references. One row per vendor. Unique `(company_id, user_id)`. Only when profile is `Vendor`. |
| `user_detail_marketplace` | Marketplace-specific subaccount as **parameter key/values** (`ShopId`, `AccessToken`, `Nickname`, `SellerId`, …, `is_secret` where needed). Unique `(company_id, user_id, marketplace_code)`. |

**Create vendor (must be idempotent per company; US-10 admin, US-11 company):**

Admin opens **Novo vendedor**, **selects an existing company** (searchable list: fantasia + CNPJ — not a CNPJ text field), then selected `marketplaceCodes` (subset of **that** company's enabled codes, or `"*"` for all enabled). Empty company or empty list is `400`. Company user skips the picker; company is the logged-in CNPJ.

```
POST /companies/{companyId}/vendors  + Idempotency-Key
        │
        ▼
User + UserCompany (profile Vendor)
        │
        ▼
users_detail  unique (company_id, user_id)
        │
        ▼
for each selected marketplace:
    user_detail_marketplace  unique (company_id, user_id, marketplace_code)
    link_status = PendingConnect  until OAuth binds the remote shop/user
```

This is the “related marketplace user”: production channels do not allow silent seller signup; we persist the subaccount and **connect** the official shop user. Retry of the same key does **not** create extra subaccounts.

**Create company user (admin, US-09):** `POST /companies/{id}/users` with selected `marketplaceCodes` → `user_company_marketplace` rows. They use **company** app tokens, not a vendor shop.

When a new marketplace is enabled on the company later, insert missing `user_detail_marketplace` rows for every existing vendor (skip if the unique key already exists).

Vendor subaccount params: Redis cache-aside `vendor-marketplace:{companyId}:{userId}:{marketplaceCode}`, `DEL` on write.

**Publish advertisement:**

- Body includes `sku` (or product id) and optional `marketplaceCodes`.
- **Omitted or empty `marketplaceCodes` = all** enabled company marketplaces where this vendor has a subaccount.
- Explicit list = only those channels (must belong to the vendor; unknown codes → `400`).
- Enqueue one command per selected marketplace: `{ companyId, vendorUserId, sku, marketplaceCode }`.
- Connector uses company app credentials **plus** that vendor's `user_detail_marketplace` parameters, executed through the **generic** binding for that `marketplace_code`.
- Listing unique `(company_id, vendor_user_id, sku, marketplace_code)` so a retry does not double-publish.

### Sales: common table + marketplace-specific attributes (US-04, US-05)

Orders are not stored as raw JSON. One **common sale** per remote order, plus EAV rows for fields that exist only on that channel.

| Table | One job |
| --- | --- |
| `sales` | Canonical order: company, vendor, `marketplace_code`, remote id, **`SaleStatus`**, money, buyer/recipient address (CEP 8 digits), tracking, FKs to NF-e and label |
| `sale_items` | Lines: sku, qty, prices, NCM/CFOP hints |
| `sale_marketplace_attributes` | `(sale_id, field_name, field_value)` — e.g. ML `shipment_id`, Shopee `package_number`. Names from `marketplace_sale_field_definition` |
| `marketplace_sale_status_map` | Remote status/substatus → canonical `SaleStatus` (data, not a C# switch) |

Unique import key: `(company_id, marketplace_code, remote_order_id)`. Webhooks upsert; they never create a second sale. Vendor scope: `vendor_user_id` from the matching `user_detail_marketplace` shop/seller id.

Canonical statuses (UI in PT): `PendingPayment` (Aguardando pagamento) → `Paid` (Pago) → `Invoicing` → `PreparingForDispatch` (Preparando para envio) → `LabelPrinted` → `Shipped` → `Delivered`, plus `InvoiceRejected`, `Cancelled`, `Returning`, `Returned`.

Marketplace “ready_to_ship / invoice_pending” stays **`Paid` until our NF-e is authorized**. The emit button is what moves the sale to Preparando para envio. Exception: attribute `invoiced_by_marketplace=true` (fulfillment).

### Outbound NF-e from a paid sale (US-06)

Inbound NF-e (chave / XML / DistDFe) still builds **inventory**. A **paid marketplace sale** additionally **emits** a model-55 NF-e de saída with [DFe.NET](https://github.com/ZeusAutomacao/DFe.NET):

1. Guard: status `Paid` or `InvoiceRejected`; dest CEP 8 digits; company A1 present; items have NCM/tax profile.
2. `POST /sales/{saleId}/nfe` → status `Invoicing` → `vilmo-nfe` builds `NFe.Classes.NFe` from the **common sale** (emit = **company CNPJ**, dest = buyer/recipient, det = `sale_items`).
3. `ServicosNFe.NFeAutorizacao(lote, IndicadorSincronizacao.Sincrono, nfeList)`.
4. Authorized (`cStat` 100/150) → store XML + chave, confirm outbound stock movement, status **`PreparingForDispatch`**. Then enqueue `UploadInvoice` to the channel (Shopee `upload_invoice_doc`, ML XML when shipment allows). Upload failure does not roll back the NF-e (chip “XML pendente no marketplace”).
5. Rejected → `InvoiceRejected`; button stays visible.

Emitente is always the **company CNPJ** (A1). The vendor is the seller of the order, not a second emitente.

### Shipping label (US-07)

When status is `PreparingForDispatch` or `LabelPrinted`, UI shows **Imprimir etiqueta para envio**.

- PDF page **100×150 mm** (default) or **138×106 mm** (13.8×10.6 cm). Not an A4 sheet with a sticker drawing.
- **Recipient:** full name or razão social; street, number, complement; neighborhood; city + UF; CEP 8 digits.
- **Sender:** company legal/trade name; full origin address; origin CEP; **CNPJ** (or CPF if PF — Vilmo tenants are CNPJ).
- **Postal:** carrier/service name, tracking when real, barcode of that tracking (never a fake barcode). NF-e chave in small type.
- Marketplace logistics: print the channel PDF if `FetchShipmentLabel` returns one; otherwise compose our layout from common sale fields.
- Placement copy in the UI: largest side of the box; do not cover the barcode; do not wrap edges/folds.
- Persist `shipment_labels`; first print sets `LabelPrinted`. Reprint allowed.

v1 prints via browser/PDF driver. Correios SIGEP/PLP API can follow; do not invent tracking numbers.

Full field lists, state machine, and API table: [USER_STORIES.md](./USER_STORIES.md).

---

## 4. Best approach mapped to SOLID and clean code

| Principle | How it shows up |
| --- | --- |
| SRP | One class executes HTTP bindings; another signs HMAC; another parses NF-e `det/prod`. |
| OCP | New marketplace = new **table rows**. New **auth protocol** (rare) = new `IAuthProtocol` class. |
| LSP | All protocols and the executor return `Result<T>`; none leak HTTP SDK exceptions. |
| ISP | Small interfaces (`IAuthProtocol`, `IMarketplaceOperationExecutor`), not `IMarketplace`. |
| DIP | Core depends on `IMarketplaceDefinitionReader` and `IAuthProtocol`, not `MercadoLivreClient`. |
| Names | `HmacSha256AuthProtocol`, `ChaveAcesso`, `CompanyId`, `VendorUserId`, `users_detail`. No `Manager`. |
| Small functions | HTTP controllers only validate + enqueue. |
| No magic | `marketplace.code` strings from the table, `AuthProtocolCode.HmacSha256`, `UserProfile.Vendor`, `SaleStatus.Paid`, `NfeStatus.Authorized`. |
| KISS | Three app containers, Redis Streams, modular monolith solution. |

---

## 5. Solution layout (to create when implementing)

```
VilmoMarketingEngine.sln
src/
  Vilmo.Identity.Domain/          # Company, User, UserCompany, Vendor, users_detail
  Vilmo.Identity.Application/
  Vilmo.BuildingBlocks/          # Result, Idempotency (company-scoped), Redis Streams, time
  Vilmo.Catalog.Domain/
  Vilmo.Catalog.Application/
  Vilmo.Inventory.Domain/
  Vilmo.Inventory.Application/
  Vilmo.Marketplace.Engine/       # generic executor, auth protocols, JSON mappings
  Vilmo.Marketplace.Seed/         # SQL/JSON fixtures for ML, Shopee, SHEIN, Magalu (data only)
  Vilmo.Sales.Domain/            # Sale, SaleStatus, attributes, shipment label
  Vilmo.Sales.Application/
  Vilmo.Nfe.Domain/               # ChaveAcesso, NfeDocument, CfopPolicy, outbound emit mapping
  Vilmo.Nfe.Application/
  Vilmo.Nfe.Zeus/                 # ISefazDocumentFetcher → DFe.NET
  Vilmo.Api/                      # ASP.NET Core
  Vilmo.Web/                      # Metronic HTML (layout-1 + demo1 page slice)
  Vilmo.Worker/                   # marketplace + inventory consumers
  Vilmo.Nfe.Worker/               # SEFAZ ingest + outbound NFeAutorizacao / XML parse / movements
tests/
  *.Unit / *.Contract
deploy/
  docker-compose.yml
  gateway/Dockerfile
  gateway/nginx.conf
  stubs/Dockerfile
  stubs/echo.conf
  gateway-smoke.sh
  api.Dockerfile
  worker.Dockerfile
  nfe.Dockerfile
  web.Dockerfile
```

Clean architecture per bounded context. Marketplace projects reference Contracts + HTTP; they do not reference Inventory domain types directly — they consume integration commands (`PublishStock`, `ImportOrder`).

---

## 6. Docker Compose (target)

Compose file: `deploy/docker-compose.yml`. **Only `vilmo-gateway` publishes a host port** (`80`, override with `GATEWAY_PORT`). Postgres, Redis, and app HTTP ports stay on the `vilmo-internal` network.

```
services:
  vilmo-gateway:  # host :80 — nginx slugs /web /api /nfe /worker
  postgres:       # internal 5432
  redis:          # internal 6379
  vilmo-api:      # internal 80 — public slug /api
  vilmo-web:      # internal 80 — public slug /web
  vilmo-worker:   # internal 80 — public slug /worker
  vilmo-nfe:      # internal 80 — public slug /nfe; certs /certs/{cnpj}.pfx
```

| Public path | Upstream |
| --- | --- |
| `/web/` | `vilmo-web` |
| `/api/` | `vilmo-api` (slug stripped, so `/api/auth/login` → `/auth/login`) |
| `/nfe/` | `vilmo-nfe` |
| `/worker/` | `vilmo-worker` |
| `/webhooks/{code}` | `vilmo-api` (marketplace ACK, no extra slug) |
| `/oauth/{code}/callback` | `vilmo-api` |

HTTP app images are echo stubs until the .NET Dockerfiles exist. Gateway config: `deploy/gateway/nginx.conf`.

API env: connection strings, public base URL for OAuth/webhooks, bootstrap super-user password. Per-company marketplace credentials are **table rows**, not env.

NFe env: `Nfe__Environment=Homologation|Production`, `Nfe__CertificatesDirectory=/certs`. Password per CNPJ from secrets / encrypted `company_certificates` rows. No global `Nfe__Cnpj`.

No `.pfx` in git. Compose mounts a host certs directory that is gitignored. First company file inside the container: `/certs/68431371000161.pfx`.

### Default UI (Metronic 9.5.0 HTML)

Full investigation and screen map: [UI.md](./UI.md).

- Source kit on `master`: `template-metronic/themeforest-p1yvR6ry-metronic-responsive-admin-dashboard-template/metronic-v9.5.0/`.
- Runtime uses **HTML only**: starter **layout-1** + **demo1** page patterns (sign-in branded, members datatable, settings, integrations).
- Inventory / products / orders **information architecture** comes from the React concept `store-inventory`, rebuilt as HTML tables — do not run the Vite/Next apps.
- `vilmo-web` copies a **slice** of assets + mapped pages. It does not ship all 10 demos or the React packages.
- Sidebar: Dashboard, NF-e / Inventory, Products, Advertisements, **Sales**, Vendors, Marketplaces, Settings (+ Companies for super user). Vendor sidebar: Dashboard, My sales, My advertisements, My marketplaces, Profile. Sale detail: **Emitir NF-e** when `Paid`; **Imprimir etiqueta para envio** when `PreparingForDispatch`.

---

## 7. Public API (first slice)

All mutating business routes require `Idempotency-Key` **and** a company context.

Auth: bearer session/JWT with `user_id`, `is_platform_super_user`, and memberships. Active company: claim or `X-Company-Id`.

| Method | Path | Behavior |
| --- | --- | --- |
| `POST` | `/auth/login` | Issue token (US-01). Super user may omit company; others need membership. |
| `GET` | `/me` | Level, memberships, company readiness. |
| `GET` | `/companies` | Super user: all. Others: memberships only. |
| `POST` | `/companies` | Admin creates a company (CNPJ unique) — US-03 legal step. |
| `POST` | `/companies/{companyId}/users` | Admin: company user + `user_company_marketplace` for **selected** codes (US-09). |
| `POST` | `/companies/{companyId}/vendors` | Admin or Company (own id): vendor + related marketplace users on **selected** codes (US-10, US-11). Idempotent. |
| `GET` | `/companies/{companyId}/vendors/{userId}` | Vendor + common detail + subaccounts (secrets omitted). |
| `PUT` | `/companies/{companyId}/vendors/{userId}/detail` | Update `users_detail` (common fields). |
| `PUT` | `/companies/{companyId}/vendors/{userId}/marketplaces/{code}` | Upsert `user_detail_marketplace` parameters; `DEL` Redis vendor cache. |
| `POST` | `/companies/{companyId}/certificate` | Upload/replace that company's A1 (multipart + password). Super user or `CompanyAdmin`. |
| `GET` | `/companies/{companyId}/marketplaces` | List this company's marketplace configs (`is_enabled`, shop id). Secrets omitted. |
| `PUT` | `/companies/{companyId}/marketplaces/{code}` | Upsert config + parameters for one channel. Writes Postgres, then `DEL` Redis cache. |
| `GET` | `/marketplaces` | List catalog (`marketplace` table). Super user sees inactive too. |
| `POST` | `/marketplaces` | Super user: insert a new `code` + protocol + bindings. No deploy. Idempotent on `code`. |
| `PUT` | `/marketplaces/{code}` | Super user: update definition, parameter keys, operation bindings; `DEL marketplace-definition:{code}`. |
| `POST` | `/nfe/chaves/{chaveAcesso}/ingest` | Validate chave, enqueue fetch with **this company's** cert. Idempotency = `(companyId, chave)`. |
| `POST` | `/nfe/xml` | Upload XML fallback. Chave from XML must match; emit/dest CNPJ must be compatible with the company CNPJ. |
| `GET` | `/nfe/chaves/{chaveAcesso}` | Ingestion status for **this company only**. |
| `GET` | `/products` / `GET /products/{sku}` | Catalog of the active company. |
| `GET` | `/inventory/{sku}` | On-hand, reserved, available for the active company. |
| `POST` | `/marketplaces/{code}/connect` | Start OAuth/HMAC for **this company's** shop; `{code}` is the table PK, not an enum. |
| `GET` | `/oauth/{code}/callback` | Store tokens as parameters; invalidate Redis. Same route for every future code. |
| `POST` | `/webhooks/{code}` | Verify using that code's `marketplace_webhook_binding`, resolve company **and vendor**, enqueue, 200. |
| `POST` | `/advertisements` | Vendor publishes a product. `marketplaceCodes` optional; **default all**. One listing per selected marketplace using that vendor's subaccount. |
| `POST` | `/listings` | Same as `/advertisements` (alias). |
| `POST` | `/inventory/{sku}/publish` | Fan-out this vendor's stock on selected marketplaces (default all). |
| `GET` | `/sales` | Company/Admin: all sales of the active CNPJ. Vendor: own sales only. Filters: `status`, `marketplace_code`. |
| `GET` | `/sales/{saleId}` | Common sale + items + EAV attributes (masked). `404` if outside visibility. |
| `POST` | `/sales/{saleId}/sync` | Enqueue FetchOrder. Admin/Company. |
| `POST` | `/sales/{saleId}/nfe` | **Emitir NF-e** (Paid / InvoiceRejected). DFe.NET; then `PreparingForDispatch`. |
| `GET` | `/sales/{saleId}/nfe` | Outbound XML/chave/status. |
| `POST` | `/sales/{saleId}/label` | **Imprimir etiqueta para envio**. Body `{ "format": "Mm100x150" \| "Mm138x106" }`. |
| `GET` | `/sales/{saleId}/label.pdf` | Label PDF. |

Webhook routes are allowed **without** `Idempotency-Key` and **without** a user JWT; they use marketplace event ids and resolve `CompanyId` + vendor from `user_detail_marketplace` (shop/seller parameter). If the subaccount is unknown, ACK 200 and drop (or park) — do not attach to a random company or vendor.

Row-level rule: `WHERE company_id = @activeCompanyId` on every business query. Vendors add `AND vendor_user_id = @me` on sales, listings, and labels. Super user still must set an active company for writes; list-all is only for `GET /companies` and admin diagnostics.

---

## 8. NF-e → inventory (the actual stock engine)

```
API: chave de acesso (company context already resolved)
        │
        ▼
Validate ChaveAcesso (44 + DV)
        │
        ▼
Redis SET NX idempotency:{companyId}:{chaveOrHeader}
INSERT nfe_documents unique (company_id, chave_acesso)
        │
        ▼
Stream: nfe.ingest.requested  { companyId, chave }
        │
        ▼
vilmo-nfe
        │
        ├── Load A1 via ICompanyCertificateStore.Get(companyId)
        ├── Cert absent → wait for XML upload (CertificateNotConfigured for this company)
        ├── Cert present → DistDFe consChNFe signed with that company's A1
        └── Parse nfeProc with DFe.NET
                │
                ▼
        For each det/prod:
          match Product by (companyId, EAN) → (companyId, cProd+emit CNPJ)
          classify CFOP + emit/dest CNPJ vs Company.Cnpj
                │
                ├── Inbound purchase / return   → +quantity for this company
                ├── Outbound sale / return      → −quantity for this company
                └── Ignore (transfer, etc.) until a rule exists
                │
                ▼
        InventoryMovement (immutable, company-scoped)
        Recompute InventoryBalance for that company
        Outbox: stock.publish.requested { companyId, sku }
```

**Do not** add quantity for every NF-e. A sale NF-e would inflate stock. Classification is a `CfopMovementPolicy` with explicit enums, not `if (cfop.StartsWith("5"))` scattered in parsers.

**Do not** apply movements to another company even if the XML CNPJ looks familiar. The authenticated/active `CompanyId` plus a CNPJ match check is required.

Reservation: marketplace sales of that company decrement **available** via `reserved` when status becomes `Paid`, not on-hand, until the **outbound NF-e** (US-06) confirms the movement.

---

## 9. Idempotency design (concrete)

| Layer | Store | Key | TTL |
| --- | --- | --- | --- |
| HTTP | Redis | `idempotency:{companyId}:{clientKey}` | 24h |
| NF-e ingest | PostgreSQL unique | `(company_id, chave_acesso)` | forever |
| Movement | PostgreSQL unique | `(company_id, chave, n_item, kind)` | forever |
| Webhook | PostgreSQL unique | `(company_id, marketplace, event_id)` | forever |
| Stream | processed table | `(company_id, message_id)` | 7 days |
| Stock push | command id + remote version | `(company_id, listing_id, command_id)` | 24h Redis + unique command |
| Create vendor | PostgreSQL unique | `(company_id, user_id)` on `users_detail` | forever |
| Vendor subaccount | PostgreSQL unique | `(company_id, user_id, marketplace_code)` on `user_detail_marketplace` | forever |
| Company user channel | PostgreSQL unique | `(company_id, user_id, marketplace_code)` on `user_company_marketplace` | forever |
| Publish advertisement | PostgreSQL unique | `(company_id, vendor_user_id, sku, marketplace_code)` | forever |
| Import sale | PostgreSQL unique | `(company_id, marketplace_code, remote_order_id)` | forever |
| Sale attribute | PostgreSQL unique | `(sale_id, field_name)` | forever |
| Emit outbound NF-e | PostgreSQL unique | `(company_id, sale_id)` on outbound `nfe_documents` | forever |
| Emit NF-e lock | Redis | `lock:nfe-emit:{companyId}:{saleId}` | seconds |
| Shipment label | PostgreSQL unique | `(sale_id, format)` until invalidated | forever |
| Token refresh lock | Redis | `lock:token-refresh:{companyId}:{userId}:{marketplaceCode}` | seconds |
| Marketplace config cache | Redis | `marketplace-config:{companyId}:{marketplaceCode}` | until write (optional 24h TTL) |
| Marketplace definition cache | Redis | `marketplace-definition:{code}` | until super-user update |
| Vendor subaccount cache | Redis | `vendor-marketplace:{companyId}:{userId}:{marketplaceCode}` | until write (optional 24h TTL) |

Two companies may reuse the same client `Idempotency-Key`. That is correct. A global Redis key without `companyId` is a bug.

Mercado Livre `x-version`: on `409`, re-GET stock, retry once with the new version, still under the same `(companyId, commandId)`.

Shopee/SHEIN HMAC failures are not retried blindly; they are `Failed` with a distinct error code.

---

## 10. Auth and secrets

- Users authenticate against our API (not against Mercado Livre). Then they operate inside a company.
- Seed `admin@vilmomkt.com` as platform super user. Regular users are created and **linked** via `UserCompany`.
- Marketplace tokens, partner keys, and A1 passwords in PostgreSQL (envelope encryption) — company rows in `company_marketplace_parameter`; vendor subaccount secrets in `user_detail_marketplace` with `is_secret`. Never appsettings committed, never `.pfx` in git.
- Hot path reads company config from Redis (`marketplace-config:{companyId}:{code}`) and vendor subaccounts from `vendor-marketplace:{companyId}:{userId}:{code}`; Postgres remains the source of truth.
- Token refresh is a worker job per **vendor subaccount**, with a lock `lock:token-refresh:{companyId}:{userId}:{marketplaceCode}`. After refresh, update the parameter row and `DEL` the cache key.
- HMAC signing goes through `HmacSha256AuthProtocol` (used by Shopee and SHEIN seed data). Partner keys stay in parameter rows.
- SHEIN `secretKey` is a vendor/company parameter (`is_secret`), treated like a long-lived token until re-auth.
- A1 password for CNPJ `68431371000161` lives only in gitignored `.env` / `.secrets/` (see [FirstCompany.md](./FirstCompany.md)). Never in git.
- AWS Console (to publish this project later): root email `admin@vilmomkt.com`. Password is **not** in git (gitignored `.secrets/`). MFA is required; when AWS login is needed, **stop and ask the operator to enter the MFA code**. Do not attempt MFA bypass.

---

## 11. Implementation sequence (when coding starts)

Do not build four C# marketplace projects. Build the **generic engine** first, then seed the four launch definitions as data.

1. **Foundation** — solution, Docker Compose (postgres + redis + empty API), BuildingBlocks (Result, company-scoped idempotency, streams), health checks.
2. **Identity + tenancy** — `Company`, `User`, `UserCompany`, JWT/`X-Company-Id`, seed `admin@vilmomkt.com` + company CNPJ `68431371000161`. Login US-01, home by level US-02. Admin creates companies (US-03, readiness flags), company users (US-09, `user_company_marketplace`), vendors on **selected** marketplaces (US-10, `PendingConnect` until OAuth). Company users create vendors for their CNPJ (US-11). Vendor sees only own sales (US-12). Row filters by `company_id`.
3. **Metronic UI shell** — `vilmo-web` from HTML starter layout-1 + demo1 sign-in and members datatable, proxied to the API. Company switcher for super user. Role-based sidebar. See [UI.md](./UI.md).
4. **Catalog + inventory domain** — Product, identifiers, movements, balances, uniqueness all include `company_id`.
5. **NF-e ingest module** — `ChaveAcesso`, XML parse via DFe.NET, CFOP policy vs `Company.Cnpj`, ingest API, XML upload path. `ICompanyCertificateStore` + Zeus fetcher for companies that have an A1 (first tenant included). HTML ingest form (chave + Dropzone XML).
6. **Marketplace engine** — `IAuthProtocol` pack (`OAuth2AuthorizationCode`, `HmacSha256`, `BearerToken`, `ApiKeyHeader`), generic HTTP executor, JSON mappings, definition cache. Commands carry `CompanyId`, `VendorUserId`, and string `marketplace_code`. Advertisement publish: `marketplaceCodes` default all.
7. **Seed four channels** — SQL/JSON fixtures for Mercado Livre, Magalu, Shopee, SHEIN (SHEIN may be `is_active = false` until Open Platform docs). Include `marketplace_sale_field_definition` and `marketplace_sale_status_map`. No per-brand class.
8. **Sales sync** — `sales` + `sale_items` + `sale_marketplace_attributes`. Webhook → FetchOrder → upsert. Canonical `SaleStatus`. List/detail UI scoped by role.
9. **Outbound NF-e** — `POST /sales/{id}/nfe`, `NFeAutorizacao`, status `PreparingForDispatch`, upload XML to channel. Button **Emitir NF-e** on Paid.
10. **Shipping label** — PDF 100×150 (or 138×106), required sender/recipient/postal fields, button **Imprimir etiqueta para envio**.
11. **Admin: register marketplace** — `POST /marketplaces` so Amazon (or any code) is added at runtime. Backfill vendor subaccounts when a company enables the new code. Status map + EAV field definitions are rows, not a rebuild.
12. **Hardening** — Polly per host, 429 budgets, contract tests against fixtures, structured logs, no secrets in logs, tenancy tests (company A cannot read company B; vendor A cannot read vendor B sales). Adding a fifth channel in tests is an INSERT, not a new project.

Each step stays shippable. Step 5 already gives "company user reads chave / XML → that company's inventory". Step 8 gives "vendor sees only his sales". Step 9–10 give the paid → NF-e → label path.

---

## 12. Testing strategy

- Unit: `ChaveAcesso` DV, CFOP policy, idempotency state machine (`companyId` in the Redis key), translators, authorization (admin vs company vs vendor), `SaleStatus` map, CEP 8 digits, label page size.
- Contract: generic executor against recorded HTTP fixtures keyed by `marketplace_code` (no live ML/Shopee in CI). Sale normalizer fixtures → `sales` + EAV rows.
- Integration: Testcontainers for Postgres + Redis; two companies; ingest a sample `procNFe` XML into A and assert B's inventory is empty; same `Idempotency-Key` on A and B both succeed; company A Shopee `PartnerId` does not leak into company B; config read hits Redis on the second call; `PUT` config deletes the cache key; creating a vendor twice with the same key does not duplicate `user_detail_marketplace`; publish with omitted `marketplaceCodes` fans out to all vendor subaccounts; **inserting a fifth `marketplace` row** (no code change) lets a company enable it and provision vendor subaccounts; vendor A `GET /sales` does not include vendor B; stub `INfeAuthorizer` emit moves `Paid` → `PreparingForDispatch`; second emit `409`; label PDF 100×150 contains sender CNPJ and recipient CEP.
- SEFAZ: optional manual homologation with CNPJ `68431371000161`'s A1; never call production SEFAZ from CI; never check a real `.pfx` into the repo.

---

## 13. Risks and constraints (do not ignore later)

- SHEIN docs are gated; estimates for product/stock field mapping are incomplete until approval.
- DistDFe `consChNFe` is the wrong primary sync at volume; add NSU polling before production traffic.
- Mercado Livre will disable notifications if the API does work in the request thread.
- Shopee Brazil invoice upload is a separate pipeline from inbound inventory NF-e; it runs **after** our outbound authorization (US-06).
- Missing `company_id` on a unique index or Redis key will mix tenants. Treat that as a release blocker. Missing `vendor_user_id` on sales lets one vendor see another — also a release blocker.
- DFe.NET SOAP clients historically assume Windows cert stores; Linux A1 load must be proven in homologation **per certificate**, starting with CNPJ `68431371000161`. Emission (`NFeAutorizacao`) on Linux must be proven in homologation the same way.
- Creating a vendor without enabled company marketplaces yields `users_detail` and zero subaccounts; enabling a marketplace later must backfill `user_detail_marketplace` for every vendor.
- Copying the entire Metronic tree into the web image will bloat deploys and mix 10 duplicate demos. Copy only layout-1 + demo1 mapped pages + `dist/assets`.
- A new marketplace whose HTTP API cannot be expressed as URL + JSON templates (binary protocols, proprietary SDKs) still needs an engine extension. That is the exception; OAuth2 + HMAC + JSON covers the four launch channels and typical Amazon SP-API-style REST.
- Marketplace logistics PDFs may not be 10×15; if so, still send that official label to the printer (carrier scanning) and keep our layout for `SellerCorreios`.
- Incomplete recipient address (CEP ≠ 8 digits) must block emit and label, not SEFAZ round-trips.

---

## 14. Out of scope for the first build

- NFCe (model 65), CT-e, MDF-e.
- Full Correios SIGEP/PLP posting ticket in v1 (label **layout** is in scope; purchasing a real PLP tracking code can follow).
- Auto-print to a named IPP/ZPL printer without the browser.
- Pricing intelligence / ads.
- Multi-tenant **billing / SaaS metering** (multi-**company** data isolation is in scope).
- One C# project per marketplace, or a `MarketplaceCode` enum that requires a rebuild to add Amazon.
- Metronic React / Next.js apps as the production UI (HTML is the default).
- RabbitMQ, Kubernetes, Kafka.

Outbound **NF-e de saída** from a paid sale (DFe.NET `NFeAutorizacao`) **is in scope** (US-06). Inbound ingest remains in scope.

---

## 15. What "done" looks like for the first vertical

Docker Compose up → open `vilmo-web` Metronic sign-in (`demo1` branded):

1. **Admin** logs in (US-01) → sees all companies (US-02) → **creates a company** with legal + selected marketplaces + A1 (US-03) so list/sync/invoice flags can turn green.
2. Admin **creates a company user** on selected channels (US-09) and/or a **vendor** on selected marketplaces (US-10, `PendingConnect` → OAuth `Linked`).
3. That **company user** logs in, **creates more vendors** for the same CNPJ, and sees **all their sales** (US-11).
4. **Vendor** logs in and sees **only** his sales and status (US-12).
5. Ingest NF-e (chave or XML) using that company's A1 → inventory **only** for that company.
6. Vendor publishes an advertisement on **linked** channels.
7. Webhook/import creates a **common sale** + EAV attributes; canonical status **Pago**.
8. On that sale, **Emitir nota fiscal eletrônica** → DFe.NET authorizes → status **Preparando para envio**. Retry of the same key does not emit twice.
9. **Imprimir etiqueta para envio** → PDF 10×15 (or 13.8×10.6) with sender CNPJ/address, recipient name/address/CEP. Status **Etiqueta impressa**.
10. A second company cannot see those products or sales. Vendor B cannot open vendor A's sale (`404`).
