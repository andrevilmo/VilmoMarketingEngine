# Marketplace Engine — Opção A: Construção Própria (In-House)

This document is the architectural contract for Vilmo's marketplace layer. New marketplaces are added by **writing a new adapter**, never by editing existing adapters or the core domain.

## Decision

**Opção A — In-House.** We own the canonical catalog, inventory, orders, and the anti-corruption layer that talks to each marketplace. We do **not** route through a third-party aggregator (API2Cart, Plugg.to, Tiny, Bling as the system of record).

Why this option:

- Inventory is born from Brazilian NF-e, not from a marketplace. An aggregator would invert that ownership.
- Each marketplace has incompatible auth, catalog, and stock models. A thin in-house adapter is cheaper to keep correct than a black-box mapping we cannot change.
- Idempotency, CFOP-based stock movements, and A1 certificate handling are domain rules, not generic "sync SKU" calls.

What we still reuse: official SDKs/libraries only (DFe.NET for SEFAZ). Marketplace HTTP clients are ours.

## Canonical model (system of record)

Marketplaces never own product identity. The engine owns:

| Concept | Meaning | Identity |
| --- | --- | --- |
| `Product` | Sellable item in our catalog | Internal SKU |
| `ProductIdentifier` | EAN, `cProd` from NF-e, NCM, seller SKU | Typed codes |
| `InventoryBalance` | Quantity we can sell, per warehouse | `(sku, warehouse)` |
| `InventoryMovement` | Immutable stock event | `(chaveAcesso, itemIndex, movementKind)` |
| `Offer` | Price + allocated stock for one channel | `(sku, marketplace, account)` |
| `Listing` | Remote publication | Marketplace listing id |
| `SalesOrder` | Normalized order | Internal order id + remote id |
| `ConnectedAccount` | Authorized seller shop | `(marketplace, remoteShopId)` |

Mapping rule: **inbound** marketplace payloads become canonical objects. **Outbound** commands are translated from canonical objects. Adapters never persist marketplace JSON as the source of truth.

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

## Idempotency (mandatory)

Every write that can be retried must be idempotent. Retries are normal (webhooks, SEFAZ timeouts, 429s).

### HTTP API

- Client sends `Idempotency-Key` (UUIDv4) on every mutating request.
- Store in Redis: `SET key NX EX 86400` with payload fingerprint + status (`Processing` / `Completed` / `Failed`).
- Same key + same fingerprint → replay stored response.
- Same key + different fingerprint → `409 Conflict`.
- Concurrent same key while `Processing` → `409` with retry-after.

### Domain uniqueness (PostgreSQL)

| Operation | Natural key |
| --- | --- |
| Ingest NF-e | `chave_acesso` (44 digits) |
| Stock movement | `(chave_acesso, n_item, movement_kind)` |
| Connect shop | `(marketplace_code, remote_shop_id)` |
| Upsert listing | `(connected_account_id, sku)` |
| Import order | `(marketplace_code, remote_order_id)` |
| Webhook | `(marketplace_code, event_id)` |
| Push stock | `(listing_id, stock_version)` or command id |

Redis is the short-window lock. PostgreSQL unique constraints are the source of truth. Never rely on Redis alone for money or stock.

### Queue consumers

Redis Streams messages carry `MessageId`. Consumers use `XREADGROUP` and persist processed ids. Duplicate delivery is a no-op.

## Event flow

```
Webhook / API command
        │
        ▼
   HTTP 200 fast ACK          ← Mercado Livre requires 200 within 500ms
        │
        ▼
   Redis Stream (command)
        │
        ▼
   Worker (idempotent handler)
        │
        ├── Catalog / Inventory (PostgreSQL)
        └── Outbox row
                │
                ▼
         Redis Stream (integration)
                │
                ▼
         Marketplace connector
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
8. Document rate limits, token TTL, and Brazil-specific rules in that project's README.

No existing connector file should change.

## What this engine does not do

- It does not scrape storefronts.
- It does not treat any marketplace as catalog master.
- It does not update stock inside a webhook handler.
- It does not embed marketplace ids in domain entities as primary keys.
