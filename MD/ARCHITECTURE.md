# Architecture — services, resources, sequences

Planning diagrams for the in-house engine. No containers are running yet; this is the target Compose topology. Contract: [MarketPlaceEngine.MD](./MarketPlaceEngine.MD). Plan: [PLAN.md](./PLAN.md). Stories: [USER_STORIES.md](./USER_STORIES.md).

Rendered diagrams (Mermaid sources live next to each PNG under [`architecture/`](./architecture/)):

| Image | View |
| --- | --- |
| [arch_01_services.png](./architecture/arch_01_services.png) | Deployables, stores, secrets, externals |
| [arch_08_data_model.png](./architecture/arch_08_data_model.png) | Postgres resources (ER) |
| [arch_09_redis_resources.png](./architecture/arch_09_redis_resources.png) | Redis keys, locks, streams |
| [arch_10_sale_status.png](./architecture/arch_10_sale_status.png) | Canonical `SaleStatus` state machine |
| [arch_05_seq_login.png](./architecture/arch_05_seq_login.png) | Login (US-01) |
| [arch_04_seq_create_vendor.png](./architecture/arch_04_seq_create_vendor.png) | Admin creates vendor on existing company (US-10) |
| [arch_02_seq_webhook_sale.png](./architecture/arch_02_seq_webhook_sale.png) | Webhook to canonical sale (US-04, US-05) |
| [arch_06_seq_nfe_ingest.png](./architecture/arch_06_seq_nfe_ingest.png) | Inbound NF-e to inventory |
| [arch_03_seq_nfe.png](./architecture/arch_03_seq_nfe.png) | Emit NF-e then label (US-06, US-07) |
| [arch_07_seq_publish.png](./architecture/arch_07_seq_publish.png) | Publish advertisement |

---

## 1. Services and resources

![Compose topology](./architecture/arch_01_services.png)

KISS: split only where failure domains differ. There is **no** container per marketplace.

```mermaid
flowchart TB
  subgraph clients ["Clients"]
    browser["Browser Admin / Empresa / Vendedor"]
    mpHook["Marketplace webhooks"]
    oauthUser["Seller OAuth browser"]
  end

  subgraph edge ["Edge"]
    gw["vilmo-gateway :80 slugs /web /api /nfe /worker"]
    web["vilmo-web internal :80"]
    api["vilmo-api internal :80"]
  end

  subgraph apps ["App processes"]
    worker["vilmo-worker generic HTTP executor"]
    nfe["vilmo-nfe DFe.NET A1 per CNPJ"]
  end

  subgraph data ["Data stores"]
    pg[("PostgreSQL source of truth")]
    redis[("Redis cache SET NX Streams")]
  end

  subgraph secrets ["Host secrets gitignored"]
    pfx["A1 PFX /certs/cnpj.pfx"]
  end

  subgraph ext ["External systems"]
    ml["Mercado Livre"]
    shopee["Shopee Open API BR"]
    shein["SHEIN Open Platform"]
    magalu["Magalu IDM plus APIs"]
    sefaz["SEFAZ DistDFe NFeAutorizacao"]
  end

  browser -->|"/web"| gw
  gw -->|"/web"| web
  gw -->|"/api"| api
  gw -->|"/nfe"| nfe
  gw -->|"/worker"| worker
  mpHook -->|"POST /webhooks/code ACK 200"| gw
  oauthUser -->|"GET /oauth/code/callback"| gw

  api --> pg
  api --> redis
  worker --> pg
  worker --> redis
  nfe --> pg
  nfe --> redis
  nfe --> pfx

  worker -.-> ml
  worker -.-> shopee
  worker -.-> shein
  worker -.-> magalu
  nfe -.-> sefaz
```

### Deployables

| Service | Host port | Internal | Responsibility | Must not |
| --- | --- | --- | --- | --- |
| `vilmo-gateway` | **80** (only public) | 80 | Nginx. Path slugs to HTTP services. | Publish 8080/8081/5432/6379 |
| `vilmo-web` | none | 80 | Metronic HTML at slug `/web` | Store marketplace secrets in the browser |
| `vilmo-api` | none | 80 | Auth, CRUD, OAuth, webhook **ACK only**. Slug `/api` | Call marketplace GET inside the webhook thread (ML 500 ms) |
| `vilmo-worker` | none | 80 | FetchOrder, publish, UploadInvoice, token refresh. Slug `/worker` | Own fiscal SOAP; listen on the host |
| `vilmo-nfe` | none | 80 | DistDFe ingest, `NFeAutorizacao`. Slug `/nfe` | Use another company's A1; listen on the host |
| `postgres` | none | 5432 | Companies, users, catalog, inventory, sales, bindings, certificates metadata | — |
| `redis` | none | 6379 | Idempotency, definition/config/vendor cache, Streams | Source of truth for money/stock |

Public slugs (see `deploy/docker-compose.yml`): `/web`, `/api`, `/nfe`, `/worker`. Marketplace callbacks stay at the root: `/webhooks/{code}`, `/oauth/{code}/callback` → `vilmo-api`. `/` redirects to `/web/`. Compose today uses HTTP echo stubs until the .NET images exist.

### Redis resources

![Redis keys and streams](./architecture/arch_09_redis_resources.png)

| Key / stream | Use |
| --- | --- |
| `idempotency:{companyId}:{key}` | HTTP lock 24h |
| `marketplace-definition:{code}` | Bindings cache |
| `marketplace-config:{companyId}:{code}` | Company app params |
| `vendor-marketplace:{companyId}:{userId}:{code}` | Vendor shop params |
| `lock:token-refresh:{companyId}:{userId}:{code}` | Refresh mutex |
| `lock:nfe-emit:{companyId}:{saleId}` | Emit mutex |
| Stream `sale.import` | Webhook → FetchOrder |
| Stream `nfe.ingest.requested` | Chave / XML ingest |
| Stream `nfe.emit.requested` | Outbound NF-e |
| Stream `stock.publish.requested` | Fan-out stock |
| Stream `marketplace.upload_invoice` | After authorized XML |

### Postgres (main tables)

![Data model](./architecture/arch_08_data_model.png)

Identity: `company`, `user`, `user_company`, `company_certificate`, `users_detail`, `user_detail_marketplace`, `user_company_marketplace`.

Marketplace data: `marketplace`, `marketplace_parameter_definition`, `marketplace_operation_binding`, `marketplace_webhook_binding`, `company_marketplace_config`, `company_marketplace_parameter`, `marketplace_sale_field_definition`, `marketplace_sale_status_map`.

Catalog: `product`, `product_identifier`, `inventory_balance`, `inventory_movement`, `listing`.

Sales: `sales`, `sale_items`, `sale_marketplace_attributes`, `nfe_documents`, `shipment_labels`.

Every business unique index includes `company_id` (except catalog of `marketplace.code`).

### External systems

| System | Protocol | Used for |
| --- | --- | --- |
| Mercado Livre | OAuth2 + Bearer REST | Items, orders, shipments, NF-e XML, labels |
| Shopee BR | HMAC-SHA256 + shop token | Stock, orders, `upload_invoice_doc` |
| SHEIN | HMAC headers | Orders/stock when bindings exist |
| Magalu | OAuth ID Magalu | Portfolio, orders |
| SEFAZ | SOAP via DFe.NET | DistDFe + `NFeAutorizacao` |
| Printer | Browser PDF | 100×150 mm label (no IPP in v1) |

Generic executor loads **table rows** (cached in Redis). Adding Amazon = INSERT, not a new service.

---

## 2. Sequence — login (US-01)

![Login](./architecture/arch_05_seq_login.png)

```mermaid
sequenceDiagram
  participant Browser
  participant Web as vilmo-web
  participant Api as vilmo-api
  participant Pg as postgres

  Browser->>Web: GET /
  Web-->>Browser: Metronic sign-in
  Browser->>Web: email + password
  Web->>Api: POST /auth/login
  Api->>Pg: load user memberships flag
  Pg-->>Api: user plus companies
  Api-->>Web: JWT plus level
  Web-->>Browser: Admin Empresas / Company Dashboard / Vendor Minhas vendas
```

Later calls: `Authorization: Bearer` + `X-Company-Id` (admin picks a company; others only memberships).

---

## 3. Sequence — create vendor on an existing company (US-10)

![Create vendor](./architecture/arch_04_seq_create_vendor.png)

```mermaid
sequenceDiagram
  participant Admin
  participant Web as vilmo-web
  participant Api as vilmo-api
  participant Pg as postgres
  participant Redis
  participant MP as Marketplace OAuth

  Admin->>Web: Novo vendedor
  Web->>Api: GET /companies
  Api->>Pg: list companies
  Pg-->>Api: fantasia plus CNPJ
  Api-->>Web: select options
  Admin->>Web: pick company plus marketplaces plus submit
  Web->>Api: POST /companies/id/vendors plus Idempotency-Key
  Api->>Redis: SET NX idempotency companyId key
  Api->>Pg: User UserCompany users_detail user_detail_marketplace PendingConnect
  Api-->>Web: 201 plus connect URLs
  Admin->>MP: authorize shop
  MP->>Api: GET /oauth/code/callback
  Api->>Pg: ShopId tokens link_status Linked
  Api->>Redis: DEL vendor-marketplace cache
```

Company user: skip `GET /companies`; Empresa is read-only.

---

## 4. Sequence — webhook to canonical sale (US-04, US-05)

![Webhook sale](./architecture/arch_02_seq_webhook_sale.png)

```mermaid
sequenceDiagram
  participant MP as Marketplace
  participant Api as vilmo-api
  participant Redis
  participant Worker as vilmo-worker
  participant Pg as postgres
  participant MPAPI as Marketplace HTTP

  MP->>Api: POST /webhooks/code
  Api->>Api: verify HMAC or signature via webhook binding
  Api->>Redis: XADD sale.import companyId eventId
  Api-->>MP: 200
  Worker->>Redis: XREADGROUP
  Worker->>Pg: shop or seller to user_detail_marketplace
  Worker->>MPAPI: FetchOrder generic binding
  MPAPI-->>Worker: order JSON
  Worker->>Pg: UPSERT sales items attributes map SaleStatus
```

Unknown shop: ACK already sent; park event; never attach to a random vendor.

---

## 5. Sequence — emit NF-e then label (US-06, US-07)

![NF-e](./architecture/arch_03_seq_nfe.png)

```mermaid
sequenceDiagram
  participant User
  participant Web as vilmo-web
  participant Api as vilmo-api
  participant Redis
  participant Pg as postgres
  participant Nfe as vilmo-nfe
  participant Sefaz as SEFAZ
  participant Worker as vilmo-worker
  participant MPAPI as Marketplace HTTP

  User->>Web: Emitir NF-e on Paid sale
  Web->>Api: POST /sales/id/nfe plus Idempotency-Key
  Api->>Redis: lock nfe-emit
  Api->>Pg: status Invoicing
  Api->>Redis: XADD nfe.emit.requested
  Api-->>Web: 202
  Nfe->>Redis: consume
  Nfe->>Pg: sale items plus company A1
  Nfe->>Sefaz: NFeAutorizacao DFe.NET
  Sefaz-->>Nfe: cStat 100
  Nfe->>Pg: XML chave status PreparingForDispatch
  Nfe->>Redis: XADD upload_invoice
  Worker->>MPAPI: UploadInvoice XML
  User->>Web: Imprimir etiqueta 10x15
  Web->>Api: POST /sales/id/label
  Api->>Pg: shipment_labels PDF status LabelPrinted
  Api-->>Web: label.pdf
```

Reject: `InvoiceRejected`; emit button stays. Upload failure does not roll back the NF-e.

---

## 6. Sequence — inbound NF-e to inventory

![Inbound NF-e ingest](./architecture/arch_06_seq_nfe_ingest.png)

```mermaid
sequenceDiagram
  participant User
  participant Api as vilmo-api
  participant Redis
  participant Nfe as vilmo-nfe
  participant Sefaz as SEFAZ
  participant Pg as postgres

  User->>Api: POST /nfe/chaves/chave/ingest
  Api->>Redis: SET NX idempotency companyId chave
  Api->>Pg: INSERT nfe_documents unique company chave
  Api->>Redis: XADD nfe.ingest.requested
  Nfe->>Pg: load A1 for companyId
  Nfe->>Sefaz: DistDFe consChNFe
  Sefaz-->>Nfe: nfeProc XML
  Nfe->>Pg: CFOP policy InventoryMovement InventoryBalance
```

XML upload skips DistDFe when there is no A1.

---

## 7. Sequence — publish advertisement (default all linked shops)

![Publish listing](./architecture/arch_07_seq_publish.png)

```mermaid
sequenceDiagram
  participant Vendor
  participant Api as vilmo-api
  participant Redis
  participant Worker as vilmo-worker
  participant Pg as postgres
  participant MPAPI as Marketplace HTTP

  Vendor->>Api: POST /advertisements sku plus optional codes
  Api->>Pg: unique company vendor sku code
  Api->>Redis: XADD publish.listing per selected Linked subaccount
  Worker->>Pg: company app plus vendor params
  Worker->>MPAPI: PublishListing binding
  MPAPI-->>Worker: remoteListingId
  Worker->>Pg: listing row
```

---

## 8. Request path (every mutating business call)

```
Browser
  -> vilmo-gateway :80  (/web /api /nfe /worker)
  -> vilmo-web or vilmo-api (Docker network only)
       JWT + X-Company-Id + Idempotency-Key
       Redis SET NX
       Postgres write
       Redis Stream
       202/200
  -> worker or nfe (async)
       generic executor or DFe.NET
       Postgres
```

Webhooks skip JWT and Idempotency-Key; they use marketplace event ids and resolve company + vendor from `user_detail_marketplace`.

---

## 9. Canonical sale status

![SaleStatus](./architecture/arch_10_sale_status.png)

Marketplace “invoice pending / ready to ship” stays **Paid** until **our** NF-e is authorized (`cStat 100`), unless `invoiced_by_marketplace` is true. `InvoiceRejected` keeps **Emitir NF-e**. Upload of XML to the channel must not roll back an authorized NF-e.
