# Marketplace Engine — Opção A: Construção Própria (In-House)

This document is the architectural contract for Vilmo's marketplace layer. New marketplaces are added by **writing a new adapter**, never by editing existing adapters or the core domain.

## Decision

**Opção A — In-House.** We own the canonical catalog, inventory, orders, and the anti-corruption layer that talks to each marketplace. We do **not** route through a third-party aggregator (API2Cart, Plugg.to, Tiny, Bling as the system of record).

Why this option:

- Inventory is born from Brazilian NF-e, not from a marketplace. An aggregator would invert that ownership.
- Each marketplace has incompatible auth, catalog, and stock models. A thin in-house adapter is cheaper to keep correct than a black-box mapping we cannot change.
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
| `CompanyMarketplaceConfig` | One marketplace enabled for one company | `(companyId, marketplaceCode)` |
| `MarketplaceParameter` | Config key/value for that connection (client id, shop id, …) | `(configId, parameterKey)` |

Rules:

- Every mutating API call runs inside a **company context**.
- Regular users see and change **only** companies they belong to.
- One platform super user (`admin@vilmomkt.com`) may see and act on **all** companies. That privilege is a `IsPlatformSuperUser` flag seeded for that email, not a string compare scattered in handlers.
- A user without membership and without super-user flag cannot call business APIs.
- Super user selects the target company with `X-Company-Id` (or an equivalent claim). Other users may send that header only if they are a member; otherwise `403`.

`CompanyRole` values: `CompanyAdmin`, `Operator`, `Viewer`. No magic numbers.

## Canonical model (system of record)

Marketplaces never own product identity. The engine owns, **always scoped by `CompanyId`**:

| Concept | Meaning | Identity |
| --- | --- | --- |
| `Product` | Sellable item in that company's catalog | `(companyId, sku)` |
| `ProductIdentifier` | EAN, `cProd` from NF-e, NCM, seller SKU | `(companyId, identifierKind, value)` |
| `InventoryBalance` | Quantity that company can sell, per warehouse | `(companyId, sku, warehouse)` |
| `InventoryMovement` | Immutable stock event | `(companyId, chaveAcesso, itemIndex, movementKind)` |
| `Offer` | Price + allocated stock for one channel | `(companyId, sku, marketplace, account)` |
| `Listing` | Remote publication | `(companyId, marketplace listing id)` |
| `SalesOrder` | Normalized order | `(companyId, internal order id)` + remote id |
| `ConnectedAccount` | Authorized seller shop **of that company** (same grain as `CompanyMarketplaceConfig`) | `(companyId, marketplace, remoteShopId)` |

A company has **many marketplaces**. Credentials and shop parameters live in tables, not in `appsettings` or environment variables. See **Company marketplace configuration** below.

Mapping rule: **inbound** marketplace payloads become canonical objects. **Outbound** commands are translated from canonical objects. Adapters never persist marketplace JSON as the source of truth. Workers always carry `CompanyId` on the message; a consumer must not infer company from a global default.

## Open/Closed contract

Core depends on abstractions. A new marketplace is a new class in a new project.

```
IMarketplaceConnector          // one marketplace, one implementation
IMarketplaceAuthorization      // OAuth / HMAC / partner-key flows
IMarketplaceCatalogTranslator  // Product/Offer ↔ remote listing
IMarketplaceStockPublisher     // push InventoryBalance to remote
IMarketplaceOrderImporter      // remote order → SalesOrder
IMarketplaceWebhookNormalizer  // remote event → MarketplaceEvent
IMarketplaceInvoiceUploader    // optional; Brazil NF-e XML where required
```

Registration is discovery, not a switch:

```
IMarketplaceConnectorRegistry.Resolve(MarketplaceCode)
```

`MarketplaceCode` is an enum (`MercadoLivre`, `Shopee`, `Shein`, `Magalu`). Adding Amazon later means a new enum value, a new project, and a DI registration. Existing connectors stay closed.

Interface segregation: connectors that cannot upload invoices implement only the interfaces they support. Do not force SHEIN to implement `IMarketplaceInvoiceUploader` if that API is unused.

Liskov: every connector must honor the same success/failure semantics (`Result<T>`), the same idempotency contract, and the same timeout/retry policy surface. A connector must not throw marketplace-specific exceptions through the core.

## Connector responsibilities (single job each)

| Type | One job |
| --- | --- |
| `*AuthorizationClient` | Exchange codes, refresh tokens, sign requests |
| `*CatalogTranslator` | Map fields and category attributes |
| `*StockPublisher` | Push quantity (and version headers when required) |
| `*OrderImporter` | Fetch and normalize one order |
| `*WebhookNormalizer` | Parse + verify signature, emit `MarketplaceEvent` |
| `*HttpGateway` | Raw HTTP to that marketplace only |

Do not create a `MercadoLivreManager`. Compose the types above.

## Company marketplace configuration (Postgres + Redis)

Each company can connect to Mercado Livre, Shopee, SHEIN, Magalu, and later channels **at the same time**. Parameters differ per company and per marketplace (client id, partner key, shop id, `site_id`, tokens). They are **not** global.

PostgreSQL is the source of truth. The data almost never changes after setup, so **reads go through Redis**.

| Table | One job |
| --- | --- |
| `marketplace` | Catalog of channels (`MercadoLivre`, `Shopee`, `Shein`, `Magalu`) + auth kind |
| `company_marketplace_config` | This company uses this marketplace (`is_enabled`, remote shop id) |
| `company_marketplace_parameter` | One parameter row: key, value, `is_secret` |

Unique: `(company_id, marketplace_code)` on config; `(config_id, parameter_key)` on parameters.

`parameter_key` is an explicit enum **owned by the adapter** (`ShopeeParameterKey.PartnerId`, `MercadoLivreParameterKey.ClientId`), not a free-text bag in the domain core. New marketplace = new keys in that adapter, no ALTER on the other three.

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

Connectors receive `CompanyMarketplaceConfig` from this reader. They must not read `IConfiguration["Shopee:PartnerId"]`.

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
| Stock movement | `(company_id, chave_acesso, n_item, movement_kind)` |
| Connect shop | `(company_id, marketplace_code, remote_shop_id)` |
| Upsert listing | `(company_id, connected_account_id, sku)` |
| Import order | `(company_id, marketplace_code, remote_order_id)` |
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
         Marketplace connector (that company's shop tokens)
```

Webhooks never call marketplace APIs inside the HTTP request. They only validate, enqueue, and return.

## Adding a marketplace (checklist)

1. Create `src/Vilmo.Marketplace.<Name>/`.
2. Implement the interfaces the channel actually supports.
3. Register in DI: `services.AddMarketplace<Name>Connector()`.
4. Add `MarketplaceCode.<Name>`.
5. Add auth callback route `/oauth/<name>/callback` (or HMAC partner flow).
6. Add webhook route `/webhooks/<name>` that only enqueues.
7. Add contract tests with recorded HTTP fixtures (no live calls in CI).
8. Insert allowed `parameter_key` values for that adapter (no change to other adapters' keys).
9. Document rate limits, token TTL, and Brazil-specific rules in that project's README.

No existing connector file should change.

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
