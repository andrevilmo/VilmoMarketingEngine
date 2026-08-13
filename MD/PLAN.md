# Plan — Marketplace API + NF-e Inventory (no implementation in this revision)

This is the build plan only. No application code, Docker images, or certificates are created until a later task explicitly asks to implement.

Referenced architecture: [MarketPlaceEngine.MD](./MarketPlaceEngine.MD) — **Opção A (in-house)** + **idempotency**.

---

## 1. Goal

A .NET API, run entirely from Docker Compose, that:

1. Serves **N companies**. Every business record belongs to a `CompanyId`. Users belong to companies. One platform super user sees all.
2. Owns a **per-company** product inventory derived from Brazilian **NF-e** (ingest by **chave de acesso**).
3. Talks to SEFAZ through [ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET). **Each company has its own A1 certificate** (CNPJ-bound). XML upload remains a fallback.
4. Publishes catalog, price, and stock, and imports orders, for **Mercado Livre**, **Shopee**, **SHEIN**, and **Magalu**, per company shop.
5. Accepts a fifth marketplace later by adding a class, not by editing the four existing ones.

Idempotency is always `(CompanyId, key)`. See [MarketPlaceEngine.MD](./MarketPlaceEngine.MD).

---

## 2. Why this shape (and not a pile of microservices)

KISS: four HTTP clients plus one fiscal SOAP client do not justify eight deployable services.

**Split only where failure domains differ.**

| Deployable | Why it exists |
| --- | --- |
| `vilmo-api` | Public HTTP: commands, OAuth callbacks, webhook ACK. Must answer fast. |
| `vilmo-worker` | Slow marketplace I/O, retries, stock fan-out. |
| `vilmo-nfe` | DFe.NET, **per-company** A1 certificates, SEFAZ rate limits, SOAP timeouts. Isolated so a SEFAZ outage does not take the API down. |
| `postgres` | Source of truth. |
| `redis` | Idempotency keys, **marketplace-config cache**, **Redis Streams** as the queue. |

One process per marketplace would duplicate auth, idempotency, and outbox. That violates SRP at the *system* level. Marketplace differences live in **adapter projects**, not in extra containers.

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

Facts the adapter must encode:

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

Facts the adapter must encode:

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

Facts the adapter must encode:

- Access is **application + review**. Full OpenAPI and webhook schemas sit behind login. First implementation step is an approved Open Platform account, not code.
- Auth: seller authorization → `get-by-token` using **APP_ID / APP_Secret** to mint `openKeyId` + `secretKey`. Later calls sign with HMAC-SHA256 over `openKeyId`, timestamp, URL path, and a random key; headers `x-lt-openKeyId`, `x-lt-timestamp`, `x-lt-signature`.
- Documented solution areas: product publish, SHEIN-fulfill orders, seller-fulfill orders (tracking), stock-preparation orders, webhooks.
- Until the gated catalog is available, the SHEIN adapter is a **skeleton** that implements the same interfaces and fails closed with `MarketplaceCapability.Unavailable`, so Magalu/ML/Shopee are not blocked.

### 3.4 Magalu (Magazine Luiza)

| Topic | Source |
| --- | --- |
| Portal | https://developers.magalu.com |
| Create application | https://developers.magalu.com/docs/first-steps/create-an-application/ |
| OAuth | https://developers.magalu.com/docs/first-steps/create-an-application/authentication-authorization |
| Products / portfolio | https://developers.magalu.com/docs/apis/products/overview |
| API catalog | https://developers.magalu.com/docs/apis/ |

Facts the adapter must encode:

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
| `UserCompany` | Links a user to a company with `CompanyRole` |
| `CompanyCertificate` | Active A1 for SEFAZ for that company only |
| `CompanyMarketplaceConfig` | This company × this marketplace (enabled, shop id) |
| `CompanyMarketplaceParameter` | Config key/value for that connection (cached in Redis) |
| `ICompanyContext` | Resolved once per HTTP request / queue message |

`CompanyRole`: `CompanyAdmin`, `Operator`, `Viewer`.

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

| Field | Value |
| --- | --- |
| Legal name | A VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA |
| CNPJ | `68431371000161` |
| A1 | PKCS#12 issued for that CNPJ |

On implement: seed this company, attach the A1 from secrets, and optionally link `admin@vilmomkt.com` as `CompanyAdmin` **in addition to** the platform flag so the same login works without sending `X-Company-Id` when only this company exists.

### Per-company A1 (SEFAZ)

`ICompanyCertificateStore.Get(CompanyId)` returns the PFX + password for DFe.NET. `ISefazDocumentFetcher` takes `CompanyId` + `ChaveAcesso`. Company 68431371000161's cert is never used for another CNPJ.

**Do not commit `.pfx` files or certificate passwords.** The operator already has a local A1 for CNPJ `68431371000161`. When coding starts, map it into the `vilmo-nfe` container as a read-only file named by CNPJ, for example:

```
# gitignored .env (example names only)
COMPANY_68431371000161_A1_HOST_PATH=<absolute-path-to-pfx-on-the-host>
COMPANY_68431371000161_A1_PASSWORD=<pfx-password>
```

Compose mounts the host file to `/certs/68431371000161.pfx` inside `vilmo-nfe`. The database row stores `company_id`, CNPJ, container path, and the password **encrypted**, not plaintext in appsettings.

The host file currently lives under the operator's Downloads folder and is named with the legal name and CNPJ. Docker Compose override (also gitignored) points at that file. **Rotate the PFX password if it was ever pasted into chat or a commit.**

If a company has no cert yet: ingest via XML upload still works; DistDFe returns `CertificateNotConfigured` for that company only.

### Company × many marketplaces (config table + Redis cache)

One company sells on several channels at once. Mercado Livre credentials for company A are not Shopee's, and they are not company B's. **All of those parameters live in PostgreSQL.** Adapters never read tenant secrets from `appsettings` or Docker env (env is only for Postgres/Redis URLs, public callback base URL, and bootstrap admin password).

| Table | Columns (intent) |
| --- | --- |
| `marketplace` | `code` (`MercadoLivre`, `Shopee`, `Shein`, `Magalu`), `auth_kind`, `is_active` |
| `company_marketplace_config` | `company_id`, `marketplace_code`, `is_enabled`, `remote_shop_id` |
| `company_marketplace_parameter` | `config_id`, `parameter_key`, `parameter_value`, `is_secret` |

Uniques: `(company_id, marketplace_code)` and `(config_id, parameter_key)`.

Parameter keys are explicit per adapter (no magic strings in core):

| Marketplace | Typical keys |
| --- | --- |
| Mercado Livre | `ClientId`, `ClientSecret`, `AccessToken`, `RefreshToken`, `UserId`, `SiteId` (`MLB`) |
| Shopee | `PartnerId`, `PartnerKey`, `ShopId`, `AccessToken`, `RefreshToken` |
| SHEIN | `AppId`, `AppSecret`, `OpenKeyId`, `SecretKey` |
| Magalu | `ClientId`, `ClientSecret`, `AccessToken`, `RefreshToken`, `Scope` |

Values with `is_secret = true` are encrypted at rest in Postgres.

**Redis cache (config almost never changes):**

- Key: `marketplace-config:{companyId}:{marketplaceCode}`
- Value: resolved DTO the connector needs (including decrypted secrets in memory only after load)
- Fill: cache-aside on read (`GET` → miss → Postgres → `SET`)
- Invalidate: `DEL` that key after any INSERT/UPDATE/DELETE of config or parameters
- No short TTL required; TTL is optional (e.g. 24h) as a safety net if a delete is missed
- Do not use Redis as the writer. Postgres commits first.

`ICompanyMarketplaceConfigReader.Get(companyId, marketplaceCode)` is the only way connectors load settings. Missing config → `MarketplaceNotConfigured` for that company+channel; other marketplaces of the same company keep working.

OAuth/token refresh **writes** new `AccessToken` / `RefreshToken` parameter rows (or updates them), then invalidates the Redis key. That is the exception to "config does not change": tokens rotate; shop ids and partner ids do not.

---

## 4. Best approach mapped to SOLID and clean code

| Principle | How it shows up |
| --- | --- |
| SRP | One class publishes stock; another signs Shopee; another parses NF-e `det/prod`. |
| OCP | New marketplace = new project + DI registration. |
| LSP | All connectors return `Result<T>`; none leak SDK exceptions. |
| ISP | Small interfaces (`IMarketplaceStockPublisher`), not `IMarketplace`. |
| DIP | Core depends on `ISefazDocumentFetcher`, `ICompanyCertificateStore`, and `ICompanyMarketplaceConfigReader`, not `ServicosNFe`, a global PFX path, or `IConfiguration` tenant secrets. |
| Names | `ShopeeStockPublisher`, `ChaveAcesso`, `CompanyId`, `InventoryReceipt`. No `Manager`. |
| Small functions | HTTP controllers only validate + enqueue. |
| No magic | `MarketplaceCode.Shopee`, `NfeStatus.Authorized`, `MovementKind.InboundPurchase`. |
| KISS | Three app containers, Redis Streams, modular monolith solution. |

---

## 5. Solution layout (to create when implementing)

```
VilmoMarketingEngine.sln
src/
  Vilmo.Identity.Domain/          # Company, User, UserCompany, CompanyRole
  Vilmo.Identity.Application/
  Vilmo.BuildingBlocks/          # Result, Idempotency (company-scoped), Redis Streams, time
  Vilmo.Catalog.Domain/
  Vilmo.Catalog.Application/
  Vilmo.Inventory.Domain/
  Vilmo.Inventory.Application/
  Vilmo.Marketplace.Contracts/    # interfaces + MarketplaceCode + parameter key types
  Vilmo.Marketplace.MercadoLivre/
  Vilmo.Marketplace.Shopee/
  Vilmo.Marketplace.Shein/
  Vilmo.Marketplace.Magalu/
  Vilmo.Nfe.Domain/               # ChaveAcesso, NfeDocument, CfopPolicy
  Vilmo.Nfe.Application/
  Vilmo.Nfe.Zeus/                 # ISefazDocumentFetcher → DFe.NET
  Vilmo.Api/                      # ASP.NET Core
  Vilmo.Worker/                   # marketplace + inventory consumers
  Vilmo.Nfe.Worker/               # SEFAZ / XML parse / movements
tests/
  *.Unit / *.Contract
deploy/
  docker-compose.yml
  docker-compose.override.yml
  api.Dockerfile
  worker.Dockerfile
  nfe.Dockerfile
```

Clean architecture per bounded context. Marketplace projects reference Contracts + HTTP; they do not reference Inventory domain types directly — they consume integration commands (`PublishStock`, `ImportOrder`).

---

## 6. Docker Compose (target)

```
services:
  postgres:     # catalog, inventory, orders, outbox, company marketplace config
  redis:        # streams + idempotency + marketplace-config cache
  vilmo-api:    # :8080
  vilmo-worker:
  vilmo-nfe:    # certs: /certs/{cnpj}.pfx (read-only, one file per company)
```

API env: connection strings, public base URL for OAuth/webhooks, bootstrap super-user password. Per-company marketplace credentials are **table rows**, not env.

NFe env: `Nfe__Environment=Homologation|Production`, `Nfe__CertificatesDirectory=/certs`. Password per CNPJ from secrets / encrypted `company_certificates` rows. No global `Nfe__Cnpj`.

No `.pfx` in git. Compose mounts a host certs directory that is gitignored. First company file inside the container: `/certs/68431371000161.pfx`.

---

## 7. Public API (first slice)

All mutating business routes require `Idempotency-Key` **and** a company context.

Auth: bearer session/JWT with `user_id`, `is_platform_super_user`, and memberships. Active company: claim or `X-Company-Id`.

| Method | Path | Behavior |
| --- | --- | --- |
| `POST` | `/auth/login` | Issue token. Super user may omit company; others need membership. |
| `GET` | `/companies` | Super user: all. Others: memberships only. |
| `POST` | `/companies` | Super user creates a company (CNPJ unique). |
| `POST` | `/companies/{companyId}/users` | Link a user to that company with a `CompanyRole`. Super user or `CompanyAdmin`. |
| `POST` | `/companies/{companyId}/certificate` | Upload/replace that company's A1 (multipart + password). Super user or `CompanyAdmin`. |
| `GET` | `/companies/{companyId}/marketplaces` | List this company's marketplace configs (`is_enabled`, shop id). Secrets omitted. |
| `PUT` | `/companies/{companyId}/marketplaces/{code}` | Upsert config + parameters for one channel. Writes Postgres, then `DEL` Redis cache. |
| `POST` | `/nfe/chaves/{chaveAcesso}/ingest` | Validate chave, enqueue fetch with **this company's** cert. Idempotency = `(companyId, chave)`. |
| `POST` | `/nfe/xml` | Upload XML fallback. Chave from XML must match; emit/dest CNPJ must be compatible with the company CNPJ. |
| `GET` | `/nfe/chaves/{chaveAcesso}` | Ingestion status for **this company only**. |
| `GET` | `/products` / `GET /products/{sku}` | Catalog of the active company. |
| `GET` | `/inventory/{sku}` | On-hand, reserved, available for the active company. |
| `POST` | `/marketplaces/{code}/connect` | OAuth for **this company's** shop, using **this company's** table parameters. |
| `GET` | `/oauth/{code}/callback` | Store tokens as parameters on that company's marketplace config; invalidate Redis. |
| `POST` | `/webhooks/{code}` | Verify, resolve company from shop/seller id, enqueue, 200. |
| `POST` | `/listings` | Publish canonical product of this company. |
| `POST` | `/inventory/{sku}/publish` | Fan-out this company's stock. |

Webhook routes are allowed **without** `Idempotency-Key` and **without** a user JWT; they use marketplace event ids and resolve `CompanyId` from `company_marketplace_config.remote_shop_id`. If the shop is unknown, ACK 200 and drop (or park) — do not attach to a random company.

Row-level rule: `WHERE company_id = @activeCompanyId` on every business query. Super user still must set an active company for writes; list-all is only for `GET /companies` and admin diagnostics.

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

Reservation: marketplace orders of that company decrement **available** via `reserved`, not on-hand, until shipment/NF-e de saída confirms.

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
| Token refresh lock | Redis | `lock:token-refresh:{companyId}:{marketplaceCode}` | seconds |
| Marketplace config cache | Redis | `marketplace-config:{companyId}:{marketplaceCode}` | until write (optional 24h TTL) |

Two companies may reuse the same client `Idempotency-Key`. That is correct. A global Redis key without `companyId` is a bug.

Mercado Livre `x-version`: on `409`, re-GET stock, retry once with the new version, still under the same `(companyId, commandId)`.

Shopee/SHEIN HMAC failures are not retried blindly; they are `Failed` with a distinct error code.

---

## 10. Auth and secrets

- Users authenticate against our API (not against Mercado Livre). Then they operate inside a company.
- Seed `admin@vilmomkt.com` as platform super user. Regular users are created and **linked** via `UserCompany`.
- Marketplace tokens, partner keys, and A1 passwords in PostgreSQL (envelope encryption) — `company_marketplace_parameter` with `is_secret`. Never appsettings committed, never `.pfx` in git.
- Hot path reads marketplace config from Redis (`marketplace-config:{companyId}:{code}`); Postgres remains the source of truth.
- Token refresh is a worker job per company marketplace config, with a lock `lock:token-refresh:{companyId}:{marketplaceCode}`. After refresh, update the parameter row and `DEL` the cache key.
- Shopee partner key used only inside `ShopeeRequestSigner`.
- SHEIN `secretKey` treated like a refresh token (long-lived until re-auth).
- A1 password for CNPJ `68431371000161` lives only in gitignored `.env` / secret store.

---

## 11. Implementation sequence (when coding starts)

Do not build all four marketplaces in parallel on day one. The engine and NF-e path must exist first, or adapters will invent their own catalog.

1. **Foundation** — solution, Docker Compose (postgres + redis + empty API), BuildingBlocks (Result, company-scoped idempotency, streams), health checks.
2. **Identity + tenancy** — `Company`, `User`, `UserCompany`, JWT/`X-Company-Id`, seed `admin@vilmomkt.com` + company CNPJ `68431371000161`. Row filters by `company_id`.
3. **Catalog + inventory domain** — Product, identifiers, movements, balances, uniqueness all include `company_id`.
4. **NF-e module** — `ChaveAcesso`, XML parse via DFe.NET, CFOP policy vs `Company.Cnpj`, ingest API, XML upload path. `ICompanyCertificateStore` + Zeus fetcher for companies that have an A1 (first tenant included).
5. **Marketplace contracts + worker** — registry, outbox, commands always carry `CompanyId`. `company_marketplace_config` + `company_marketplace_parameter` tables, `ICompanyMarketplaceConfigReader` with Redis cache-aside.
6. **Mercado Livre adapter** — OAuth using that company's table parameters, items, stock (User Product + x-version), `orders_v2` webhook ACK; shop mapped to company.
7. **Magalu adapter** — ID Magalu OAuth, SKU / price / stock as three calls, webhooks.
8. **Shopee adapter** — HMAC signer, Brazil host, stock, push, invoice upload hook (uses that company's stored NF-e XML).
9. **SHEIN adapter** — signer + skeleton; fill endpoints after Open Platform approval.
10. **Hardening** — Polly per host, 429 budgets, contract tests, structured logs, no secrets in logs, tenancy tests (company A cannot read company B).

Each step stays shippable. Step 4 already gives "company user reads chave / XML → that company's inventory".

---

## 12. Testing strategy

- Unit: `ChaveAcesso` DV, CFOP policy, idempotency state machine (`companyId` in the Redis key), translators, authorization (super user vs member).
- Contract: adapters against recorded HTTP (no live ML/Shopee in CI).
- Integration: Testcontainers for Postgres + Redis; two companies; ingest a sample `procNFe` XML into A and assert B's inventory is empty; same `Idempotency-Key` on A and B both succeed; company A Shopee `PartnerId` does not leak into company B; config read hits Redis on the second call; `PUT` config deletes the cache key.
- SEFAZ: optional manual homologation with CNPJ `68431371000161`'s A1; never call production SEFAZ from CI; never check a real `.pfx` into the repo.

---

## 13. Risks and constraints (do not ignore later)

- SHEIN docs are gated; estimates for product/stock field mapping are incomplete until approval.
- DistDFe `consChNFe` is the wrong primary sync at volume; add NSU polling before production traffic.
- Mercado Livre will disable notifications if the API does work in the request thread.
- Shopee Brazil invoice upload is a separate pipeline from inbound inventory NF-e.
- Missing `company_id` on a unique index or Redis key will mix tenants. Treat that as a release blocker.
- DFe.NET SOAP clients historically assume Windows cert stores; Linux A1 load must be proven in homologation **per certificate**, starting with CNPJ `68431371000161`.
- Magalu seller login must be the store (PJ) or scopes fail in ways that look like bugs.

---

## 14. Out of scope for the first build

- Emission of NF-e (we ingest and, later, upload XML to Shopee; we do not become an issuer in v1).
- Pricing intelligence / ads.
- Multi-tenant **billing / SaaS metering** (multi-**company** data isolation is in scope).
- Amazon, Americanas, TikTok Shop (the engine is ready; adapters are not).
- RabbitMQ, Kubernetes, Kafka.

---

## 15. What "done" looks like for the first vertical

Docker Compose up → login as `admin@vilmomkt.com` (sees all companies) → active company CNPJ `68431371000161` → `POST /nfe/xml` or ingest by chave using that company's A1 → product rows + inventory **only** for that company → a second company cannot read those products even with the same `Idempotency-Key` → connect one Mercado Livre test shop **to that company** → `POST /inventory/{sku}/publish` updates remote stock → a test purchase webhook reserves stock without double-counting on retry.
