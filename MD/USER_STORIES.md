# User stories — roles, sales sync, NF-e de saída, etiqueta Correios

This document is the **depth plan** for identity, sales, outbound invoice, and shipping labels. It extends [PLAN.md](./PLAN.md) and [MarketPlaceEngine.MD](./MarketPlaceEngine.MD). No application code in this revision.

Actors (login roles). Names in the UI stay Portuguese; codes in the API stay English.

| Login role (user wording) | System profile | Visibility |
| --- | --- | --- |
| **Admin** | `IsPlatformSuperUser` (`admin@vilmomkt.com`) | **See all**: every company (CNPJ), every user, every sale, every NF-e, every label |
| **Company** | `CompanyAdmin` on `UserCompany` | **See all regarding his users**: every vendor/staff of **that CNPJ**, their subaccounts, their sales, their invoices and labels |
| **Vendor user** | `UserProfile.Vendor` | **See all related to his sales**: own profile, own marketplace links, own sales, own NF-e and labels. Nothing of other vendors in the same company |

`Operator` and `Viewer` stay as staff profiles (warehouse / read-only). They are not a third login persona in these stories; they inherit company-scoped data with fewer write buttons.

---

## US-01 — Log in as Admin and see everything

**As** the platform admin  
**I want** to sign in once and operate every company  
**So that** I can support, audit, and configure the whole engine without a second login.

### Acceptance

- Sign-in: `POST /auth/login` with `admin@vilmomkt.com`. JWT has `is_platform_super_user = true`.
- Default landing: company list (`GET /companies`). Header **company switcher** sets `X-Company-Id` for subsequent calls.
- With no `X-Company-Id`: `GET /companies`, `GET /marketplaces`, diagnostics. Any sale/inventory/vendor list without a company → `400 CompanyRequired`.
- With `X-Company-Id`: admin sees **that** company's users, vendors, sales, NF-e, labels — including vendors the company admin created.
- Admin can open **any** sale by id (`GET /sales/{saleId}`) even if it belongs to another CNPJ; the payload still includes `companyId` + `cnpj`.
- Admin cannot skip tenancy on writes: mutating routes still require `X-Company-Id` and `Idempotency-Key`.
- Password lives in gitignored secrets. Email is seeded, not hard-coded in handlers.

### UI

Metronic branded sign-in. After login: Companies (teams) + switcher. All sidebar items visible. Badge **Admin**.

---

## US-02 — Log in as Company and see all of that CNPJ

**As** a company user (`CompanyAdmin`)  
**I want** to see every user and every sale that belongs to my company CNPJ  
**So that** I run the store without seeing other legal entities.

### Acceptance

- User is linked with `UserCompany(companyId, CompanyAdmin)`. Company row has unique `Cnpj` (digits only, 14).
- JWT carries memberships. If the user has **one** company, that CNPJ is the active context (header optional). If many, require `X-Company-Id` among memberships; otherwise `400`.
- Lists are filtered `WHERE company_id = @activeCompanyId`:
  - users / vendors of this CNPJ
  - marketplace configs and vendor subaccounts
  - **all sales** of this company (every vendor)
  - NF-e ingest + NF-e de saída of this company
  - labels of this company
- Attempt to read another company's `saleId` → `404` (not `403` with a leaked id). Same for vendors and NF-e.
- Company admin may create vendors, enable marketplaces, upload A1, emit NF-e and print labels **for any sale of this CNPJ**.
- Company admin does **not** see `GET /companies` as a global catalog (only own memberships). Cannot `POST /marketplaces` (catalog). Cannot seed `IsPlatformSuperUser`.

### Binding vendor → company by CNPJ

A vendor **belongs** to a company, not to a marketplace. The company's CNPJ is the legal owner of the A1, of the NF-e emitente, and of the sender block on the label.

```
Company (unique CNPJ)
  └── UserCompany (CompanyAdmin | Operator | Viewer | Vendor)
        └── if Vendor: users_detail + user_detail_marketplace(code…)
              └── sales (vendor_user_id)
```

Creating a vendor always sets `users_detail.company_id` to that CNPJ's company. A vendor cannot be moved to another CNPJ without a new user+detail (different tenant). Same email on two CNPJs is two vendor rows.

---

## US-03 — Log in as Vendor user: marketplaces + own sales only

**As** a vendor user  
**I want** to be linked to the marketplaces my company enabled, under that company's CNPJ, and see only my sales  
**So that** I fulfill my own orders without seeing other sellers.

### Acceptance

- Profile is `Vendor`. Row in `users_detail` unique `(company_id, user_id)`: legal name, CPF/CNPJ of the vendor (may differ from the company CNPJ), phone, address.
- One `user_detail_marketplace` per enabled `marketplace.code` unique `(company_id, user_id, marketplace_code)`: shop id, tokens, nickname.
- Login lands on **My sales** (`GET /sales?vendorUserId=me`). Default filter is implicit; passing another vendor id → `403`.
- Vendor **can**: view own listings, own sales, emit NF-e on **own paid** sales (company A1 still signs — emitente is the **company CNPJ**), print labels for own sales, update own `users_detail` (non-document fields).
- Vendor **cannot**: list other vendors, list other vendors' sales, change company marketplace app credentials, upload A1, create companies, enable a new marketplace catalog row.
- Webhook-imported orders attach to the vendor whose `user_detail_marketplace` matches the remote shop/seller id. If unmatched: ACK 200, park the event, **do not** assign to a random vendor.

### UI

Sidebar for vendor: Dashboard (own KPIs), My sales, My advertisements, My marketplaces, Profile. No Companies, no Vendors admin, no Marketplace catalog, no A1.

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
        ├── Authorized → save XML/chave, inventory outbound confirm,
        │                status → PreparingForDispatch
        │                outbox: marketplace.upload_invoice { saleId }
        └── Rejected / timeout → InvoiceRejected (sale stays payable for retry)
```

After authorization the worker **uploads the XML** to the channel when the binding exists:

- Shopee: `v2.order.upload_invoice_doc` (`file_type=4`), wait ~5 min after SEFAZ (already in PLAN).
- Mercado Livre: invoice/XML when shipment is `ready_to_ship` + `invoice_pending` (`need_invoice_xml`).
- Magalu / SHEIN: operation binding `UploadInvoice` if the seed has it; else store XML and show “enviar XML” later.

Upload failure does **not** roll back the NF-e. Sale stays `PreparingForDispatch`; UI shows a warning chip **XML pendente no marketplace**.

Idempotency: unique `(company_id, sale_id)` on outbound `nfe_documents` of kind `OutboundSale`. Retry of the same HTTP key replays the stored result. A second emit on an authorized sale → `409 AlreadyInvoiced`.

Inventory: reservation happened at `Paid`. Authorized NF-e posts the immutable outbound `InventoryMovement` (CFOP sale) for this company only.

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
| List companies | all | memberships only | memberships only | — |
| Users of company | yes | yes | no | no |
| Vendor subaccounts | yes | yes | own only | no |
| Sales list | all in selected company / all companies in admin search | all in company | own `vendor_user_id` | no |
| Emit NF-e | yes | yes | own sale | no |
| Print label | yes | yes | own sale | no |
| A1 / marketplace app config | yes | yes | no | no |
| Marketplace catalog (`POST /marketplaces`) | yes | no | no | no |

Every query: `company_id` from context. Vendor queries add `vendor_user_id = me`. Super user still sends `X-Company-Id` for lists inside a CNPJ.

---

## API additions (sales / NF-e / label)

All mutating calls: JWT + company context + `Idempotency-Key` (except webhooks).

| Method | Path | Who | Behavior |
| --- | --- | --- | --- |
| `GET` | `/sales` | Admin/Company: company filter. Vendor: forced own | Filter `status`, `marketplace_code`, date |
| `GET` | `/sales/{saleId}` | Owner per matrix | Common sale + items + attributes (secrets masked) + `remote_status` |
| `POST` | `/sales/{saleId}/sync` | Admin/Company | Enqueue FetchOrder (repair) |
| `POST` | `/sales/{saleId}/nfe` | Paid/InvoiceRejected | Emit via DFe.NET (US-06) |
| `GET` | `/sales/{saleId}/nfe` | | XML/chave/status of outbound NF-e |
| `POST` | `/sales/{saleId}/label` | PreparingForDispatch/LabelPrinted | Generate PDF (US-07) |
| `GET` | `/sales/{saleId}/label.pdf` | | Application/pdf |

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
| Sidebar | Vendor vs Company vs Admin | US-01–03 |

---

## Testing these stories (when coding)

- Unit: CEP 8 digits, status map, state machine (cannot emit from `PendingPayment`; cannot skip NF-e to `PreparingForDispatch` unless `invoiced_by_marketplace`).
- Tenancy: vendor A cannot `GET` vendor B sale (404). Company A cannot see company B sale. Admin with `X-Company-Id` of A sees A's sales only in that list; admin search-all is a separate route.
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
