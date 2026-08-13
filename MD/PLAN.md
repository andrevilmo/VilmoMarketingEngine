# Plan — Marketplace API + NF-e Inventory (no implementation in this revision)

This is the build plan only. No application code, Docker images, or certificates are created until a later task explicitly asks to implement.

Referenced architecture: [MarketPlaceEngine.MD](./MarketPlaceEngine.MD) — **Opção A (in-house)** + **idempotency**.

---

## 1. Goal

A .NET API, run entirely from Docker Compose, that:

1. Owns a product inventory derived from Brazilian **NF-e** (ingest by **chave de acesso**).
2. Talks to SEFAZ through [ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET). Certificate **A1** is a later step; the design must accept it without rewriting the fiscal module.
3. Publishes catalog, price, and stock, and imports orders, for **Mercado Livre**, **Shopee**, **SHEIN**, and **Magalu**.
4. Accepts a fifth marketplace later by adding a class, not by editing the four existing ones.

---

## 2. Why this shape (and not a pile of microservices)

KISS: four HTTP clients plus one fiscal SOAP client do not justify eight deployable services.

**Split only where failure domains differ.**

| Deployable | Why it exists |
| --- | --- |
| `vilmo-api` | Public HTTP: commands, OAuth callbacks, webhook ACK. Must answer fast. |
| `vilmo-worker` | Slow marketplace I/O, retries, stock fan-out. |
| `vilmo-nfe` | DFe.NET, A1 certificate, SEFAZ rate limits, SOAP timeouts. Isolated so a SEFAZ outage does not take the API down. |
| `postgres` | Source of truth. |
| `redis` | Idempotency keys, token cache, **Redis Streams** as the queue. |

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
- DFe.NET on Linux: load A1 with `X509Certificate2` from a mounted secret. Never bake `.pfx` into the image.

---

## 4. Best approach mapped to SOLID and clean code

| Principle | How it shows up |
| --- | --- |
| SRP | One class publishes stock; another signs Shopee; another parses NF-e `det/prod`. |
| OCP | New marketplace = new project + DI registration. |
| LSP | All connectors return `Result<T>`; none leak SDK exceptions. |
| ISP | Small interfaces (`IMarketplaceStockPublisher`), not `IMarketplace`. |
| DIP | Core depends on `ISefazDocumentFetcher`, not `ServicosNFe`. |
| Names | `ShopeeStockPublisher`, `ChaveAcesso`, `InventoryReceipt`. No `Manager`. |
| Small functions | HTTP controllers only validate + enqueue. |
| No magic | `MarketplaceCode.Shopee`, `NfeStatus.Authorized`, `MovementKind.InboundPurchase`. |
| KISS | Three app containers, Redis Streams, modular monolith solution. |

---

## 5. Solution layout (to create when implementing)

```
VilmoMarketingEngine.sln
src/
  Vilmo.BuildingBlocks/          # Result, Idempotency, Redis Streams, time
  Vilmo.Catalog.Domain/
  Vilmo.Catalog.Application/
  Vilmo.Inventory.Domain/
  Vilmo.Inventory.Application/
  Vilmo.Marketplace.Contracts/    # interfaces + MarketplaceCode
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
  postgres:     # catalog, inventory, orders, outbox, idempotency audit
  redis:        # streams + idempotency + token cache
  vilmo-api:    # :8080
  vilmo-worker:
  vilmo-nfe:    # cert volume later: /certs/a1.pfx (read-only)
```

API env: connection strings, marketplace app credentials, public base URL for OAuth/webhooks.

NFe env: `Nfe__CertificatePath`, `Nfe__CertificatePassword`, `Nfe__Environment=Homologation|Production`, `Nfe__Cnpj`. Empty path → XML-upload-only mode.

No `.pfx` in git. Compose mounts a local secret directory that is gitignored.

---

## 7. Public API (first slice)

All mutating routes require `Idempotency-Key`.

| Method | Path | Behavior |
| --- | --- | --- |
| `POST` | `/nfe/chaves/{chaveAcesso}/ingest` | Validate chave, enqueue fetch/parse. Natural idempotency key = chave. |
| `POST` | `/nfe/xml` | Upload XML when A1 is not configured (or as fallback). Chave extracted from XML must match. |
| `GET` | `/nfe/chaves/{chaveAcesso}` | Ingestion status + parsed header. |
| `GET` | `/products` / `GET /products/{sku}` | Catalog. |
| `GET` | `/inventory/{sku}` | On-hand, reserved, available. |
| `POST` | `/marketplaces/{code}/connect` | Start OAuth or return partner auth URL. |
| `GET` | `/oauth/{code}/callback` | Store tokens (encrypted). |
| `POST` | `/webhooks/{code}` | Verify, enqueue, 200. |
| `POST` | `/listings` | Publish canonical product to a connected account. |
| `POST` | `/inventory/{sku}/publish` | Fan-out stock to selected marketplaces. |

Webhook routes are allowed **without** `Idempotency-Key`; they use marketplace event ids.

---

## 8. NF-e → inventory (the actual stock engine)

```
API: chave de acesso
        │
        ▼
Validate ChaveAcesso (44 + DV)
        │
        ▼
Redis idempotency + INSERT nfe_documents (unique chave)
        │
        ▼
Stream: nfe.ingest.requested
        │
        ▼
vilmo-nfe
        │
        ├── A1 absent → wait for XML upload (or fail with CertificateNotConfigured)
        ├── A1 present → DistDFe consChNFe (and manifestação if only resNFe)
        └── Parse nfeProc with DFe.NET
                │
                ▼
        For each det/prod:
          match Product by EAN → cProd+emit CNPJ → create unmatched Product
          classify CFOP + emit/dest CNPJ vs our CNPJ
                │
                ├── Inbound purchase / return   → +quantity
                ├── Outbound sale / return      → −quantity
                └── Ignore (transfer, etc.) until a rule exists
                │
                ▼
        InventoryMovement (immutable)
        Recompute InventoryBalance
        Outbox: stock.publish.requested
```

**Do not** add quantity for every NF-e. A sale NF-e would inflate stock. Classification is a `CfopMovementPolicy` with explicit enums, not `if (cfop.StartsWith("5"))` scattered in parsers.

Reservation: marketplace orders decrement **available** via `reserved`, not on-hand, until shipment/NF-e de saída confirms.

---

## 9. Idempotency design (concrete)

| Layer | Store | Key | TTL |
| --- | --- | --- | --- |
| HTTP | Redis | `idempotency:{tenant}:{key}` | 24h |
| NF-e ingest | PostgreSQL unique | `chave_acesso` | forever |
| Movement | PostgreSQL unique | `(chave, n_item, kind)` | forever |
| Webhook | PostgreSQL unique | `(marketplace, event_id)` | forever |
| Stream | consumer group + processed table | `message_id` | 7 days |
| Stock push | command id + remote version | `(listing_id, command_id)` | 24h Redis + unique command |

Mercado Livre `x-version`: on `409`, re-GET stock, retry once with the new version, still under the same command id.

Shopee/SHEIN HMAC failures are not retried blindly; they are `Failed` with a distinct error code.

---

## 10. Auth and secrets

- Marketplace tokens and A1 password in PostgreSQL (envelope encryption) or Docker secrets — never appsettings committed.
- Token refresh is a worker job per `ConnectedAccount`, with a lock in Redis so two workers do not refresh the same shop.
- Shopee partner key used only inside `ShopeeRequestSigner`.
- SHEIN `secretKey` treated like a refresh token (long-lived until re-auth).

---

## 11. Implementation sequence (when coding starts)

Do not build all four marketplaces in parallel on day one. The engine and NF-e path must exist first, or adapters will invent their own catalog.

1. **Foundation** — solution, Docker Compose (postgres + redis + empty API), BuildingBlocks (Result, idempotency, streams), health checks.
2. **Catalog + inventory domain** — Product, identifiers, movements, balances, uniqueness.
3. **NF-e module** — `ChaveAcesso`, XML parse via DFe.NET, CFOP policy, ingest API, XML upload path. Stub SEFAZ fetcher.
4. **Marketplace contracts + worker** — registry, outbox, `PublishStock` / `ImportOrder` commands.
5. **Mercado Livre adapter** — OAuth, items, stock (User Product + x-version), `orders_v2` webhook ACK.
6. **Magalu adapter** — ID Magalu OAuth, SKU / price / stock as three calls, webhooks.
7. **Shopee adapter** — HMAC signer, Brazil host, stock, push, invoice upload hook (uses stored NF-e XML).
8. **SHEIN adapter** — signer + skeleton; fill endpoints after Open Platform approval.
9. **A1 certificate** — implement `ZeusSefazDocumentFetcher` (DistDFe `consChNFe` + NSU poll + manifestação), mount `.pfx`, homologation first.
10. **Hardening** — Polly per host, 429 budgets, contract tests, structured logs, no secrets in logs.

Each step stays shippable. Step 3 already gives "read chave / XML → inventory" without any marketplace.

---

## 12. Testing strategy

- Unit: `ChaveAcesso` DV, CFOP policy, idempotency state machine, translators.
- Contract: adapters against recorded HTTP (no live ML/Shopee in CI).
- Integration: Testcontainers for Postgres + Redis; ingest a sample `procNFe` XML and assert one movement + balance.
- SEFAZ: optional manual homologation profile; never call production SEFAZ from CI.

---

## 13. Risks and constraints (do not ignore later)

- SHEIN docs are gated; estimates for product/stock field mapping are incomplete until approval.
- DistDFe `consChNFe` is the wrong primary sync at volume; add NSU polling before production traffic.
- Mercado Livre will disable notifications if the API does work in the request thread.
- Shopee Brazil invoice upload is a separate pipeline from inbound inventory NF-e.
- DFe.NET SOAP clients historically assume Windows cert stores; Linux A1 load must be proven in homologation.
- Magalu seller login must be the store (PJ) or scopes fail in ways that look like bugs.

---

## 14. Out of scope for the first build

- Emission of NF-e (we ingest and, later, upload XML to Shopee; we do not become an issuer in v1).
- Pricing intelligence / ads.
- Multi-tenant SaaS billing.
- Amazon, Americanas, TikTok Shop (the engine is ready; adapters are not).
- RabbitMQ, Kubernetes, Kafka.

---

## 15. What "done" looks like for the first vertical

Docker Compose up → `POST /nfe/xml` with a sample authorized NF-e → product rows + inventory balance → connect one Mercado Livre test user → `POST /inventory/{sku}/publish` updates remote stock → a test purchase webhook reserves stock without double-counting on retry.
