# UI — Metronic 9.5.0 as default HTML templates

Investigation of `template-metronic/.../metronic-v9.5.0/` on `master`. This is the **default UI** for Vilmo. No UI code is implemented in this revision.

KeenThemes Metronic **v9.5.0**, Tailwind CSS 4, KTUI. ThemeForest package id `themeforest-p1yvR6ry`. License: one license per deployment; SaaS with paid end users needs an extended license. Keep the purchased tree in `template-metronic/` as the reference kit. **Do not copy all packages into the runtime app.**

## What is in the folder

| Package | Role | Approx. files |
| --- | --- | --- |
| `metronic-tailwind-html-demos` | Full HTML: 10 demos (`demo1`–`demo10`), ~1175 HTML pages, auth, account, store-client | 2278 |
| `metronic-tailwind-html-starter-kit` | HTML shell: **17 layouts** (`layout-1` … `layout-17`, plus `layout-1/dark-sidebar`) | 1116 |
| `metronic-tailwind-react-demos` | Same demos in React (Vite + Next.js, JS + TS) | 8021 |
| `metronic-tailwind-react-starter-kit` | React shells (Vite + Next.js) | 7840 |
| `metronic-tailwind-react-concepts` | Workflow apps, including **`store-inventory`** | 7271 |
| `metronic-tailwind-nextjs-landings` | Marketing/SaaS landing (`saas`) | 133 |

Stack of the HTML kits: Tailwind 4, webpack, KTUI, ApexCharts, DataTables, Dropzone, TinyMCE, FullCalendar, Keenicons. Raw pages live in `dist/html`. Source JS/CSS in `src/` (`app`, `core`, `css`, `vendors`). Composer: `html-composer.html`.

## Decision (KISS + HTML as requested)

Use **HTML**, not React/Next.js, so the UI stays a thin client of the .NET API.

| Use | Path |
| --- | --- |
| Default shell | `metronic-tailwind-html-starter-kit/dist/html/layout-1/` (light sidebar). Dark option: `layout-1/dark-sidebar` or demo1 `dashboards/dark-sidebar.html`. |
| Page patterns | `metronic-tailwind-html-demos/dist/html/demo1/` only. Ignore demo2–demo10 (same pages, different chrome). |
| Screen IA for inventory/products/orders | React concept `store-inventory` **as a menu map**, re-implemented with HTML + DataTables, not by running that Vite app. |
| Auth chrome | `demo1/authentication/branded/sign-in.html` (and reset-password). Classic variants are unused. |
| Not used at runtime | Next.js landings **as a Node app**, React demos, React starter, nine extra HTML demos, store-client **checkout/cart/wishlist** (B2C storefront, not seller admin). **SaaSify look** is the public homepage visual system, shipped as `deploy/site/` static HTML (US-17). |

Runtime app (`src/Vilmo.Web` when implementing): copy **assets** (`css`, `js`, `media`, `vendors`) + **one layout** + **only the pages we map**. Talk to `vilmo-api` with `Authorization` + `X-Company-Id` + `Idempotency-Key` on writes.

**Wireframes (planning mockups):** [WIREFRAMES.md](./WIREFRAMES.md) — **public commercial index**, login, three role shells, company wizard, **marketplace connection fields per company**, create vendor, sales, NF-e button, Correios 10×15 label, **Estoque + preço**, **Ingerir NF-e**, **Vilmo NF-e iOS/Android app**.

Do not serve the 10GB-class template tree from Docker. `vilmo-web` is a small static/Razor site.

## Screen map (Vilmo domain → Metronic file)

| Screen | Metronic source to clone/adapt | Notes |
| --- | --- | --- |
| **Public commercial index** (`/` pt-BR, `/en/` en) | **SaaSify** (`metronic-tailwind-nextjs-landings/saas`) as the **visual system**, rebuilt as **static HTML** in `deploy/site/` (not a Next.js server) | **US-17.** Not behind `/web`. Sticky header: **PT \| EN** then **Entrar / Sign in** → `/web/`. Full crawlable copy in **Portuguese and English**. **#company** = CNPJ **68.431.371/0001-61**. Contact **`admin@vilmomkt.com`**. Cookie bar (LGPD). Footer: Privacidade, Termos, Cookies. Wireframe [wf_00_index_spa.png](./wireframes/wf_00_index_spa.png). |
| Sign in | `demo1/authentication/branded/sign-in.html` | `POST /auth/login` (US-01). URL **`/web/`**. One screen for Admin / Company / Vendor. `noindex`. |
| Sign up / invite | `demo1/authentication/branded/sign-up.html` | Invite token from US-08–US-10. Not public self-serve. |
| Reset password | `demo1/authentication/branded/reset-password/*` | |
| 404 / 500 | `demo1/authentication/error-404.html`, `error-500.html` | |
| Dashboard | starter `layout-1/index.html` + demo1 `index.html` widgets | US-02 widgets by level. Header badge **Admin** / **Empresa** / **Vendedor**. |
| Companies (admin) | `demo1/account/members/teams.html` | Create wizard US-03: legal, A1, selected marketplaces, readiness lights. |
| Create company user | members form + integrations checkboxes | Admin US-09. Codes must be company-enabled. |
| Company switcher | header / teams dropdown in demo1 | Sets `X-Company-Id`. Admin: all CNPJs. |
| Vendors list | `demo1/account/members/team-members-datatable.html` | Company/Admin. Link status per channel. Hidden from Vendor. |
| Create vendor | members form; **Empresa select** (admin) or locked company (company user); marketplace checkboxes of **that** company | US-10 / US-11. Never a free-text company CNPJ. |
| Vendor detail (common) | `demo1/account/home/user-profile.html` + `settings-sidebar.html` | `users_detail`. Vendor: own profile only. |
| Vendor marketplace subaccount | `demo1/account/api-keys.html` + settings form | `user_detail_marketplace`. Secrets masked. Connect if pending. |
| Roles / permissions | `demo1/account/members/roles.html`, `permissions-toggle.html` | Admin / Company / Vendor. |
| Company marketplace config | `demo1/account/integrations.html` + settings form per `code` | **US-15.** Admin + Company. **Editable** connection fields (ClientId, PartnerKey, …) from `marketplace_parameter_definition`. Secrets masked. Connect/Reconectar. Vendor: hidden. |
| Register marketplace (super user) | integrations + settings form | `POST /marketplaces` — new `code` at runtime, no deploy. Company screen then shows that code’s fields. |
| A1 certificate | settings form + Dropzone | Required for `ready_to_invoice`. |
| Products | store-inventory **Product List / Details / Create** (HTML tables from demo1 members datatable + store-client `product-details.html` for the detail chrome) | Post items when `ready_to_list`. |
| Inventory | store-inventory **All Stock / Current** | **US-13.** Admin + Company only. **Saldo** + inline **preço de venda**. Vendor: hidden. |
| Ingest NF-e | custom form on layout-1 + camera overlay | **US-14.** Admin: Empresa/CNPJ select + chave 44. **Ler código (câmera):** `getUserMedia` + Code 128 / QR → 44 digits. Company: CNPJ **locked**. Optional XML Dropzone. Vendor: hidden. |
| **Vilmo NF-e app** | .NET MAUI (not Metronic) | **US-16.** Separate iOS + Android. Scan DANFE, CNPJ history (encrypted, last filled default), encrypted POST. |
| Advertisements | product list + “publish” modal with marketplace checkboxes | Default **linked** channels; optional `marketplaceCodes`. Vendor: own listings. |
| **Sales list** | store-inventory **Order List** (DataTables) | Company: all vendors. Vendor: own (US-12). Status in PT. |
| **Sale detail** | store-inventory **Order Details** | Common fields + accordion **Dados do marketplace** (EAV). Chips: canonical + raw. |
| **Emitir NF-e** | confirm modal on sale detail | Visible when `Paid` or `InvoiceRejected` and company `ready_to_invoice`. |
| **Imprimir etiqueta para envio** | sale detail + print iframe | Visible when `PreparingForDispatch` or `LabelPrinted`. Size 10×15 cm (default) or 13.8×10.6 cm. |
| Account security | `demo1/account/security/overview.html` | Password, sessions. |

Sidebar by role (replace Metronic demo links with these, nothing else):

| Role | Items |
| --- | --- |
| **Admin** | Companies, Users, Dashboard, **Estoque**, **Ingerir NF-e**, Products, Advertisements, **Sales**, Vendors, **Marketplaces** (company connections), Settings |
| **Company** | Dashboard, **Estoque**, **Ingerir NF-e**, Products, Advertisements, **Sales**, Vendors, **Marketplaces** (this CNPJ’s connections), Settings |
| **Vendor** | Dashboard, **My sales**, My advertisements, My marketplaces, Profile |

Company users create vendors for their CNPJ and see **all** those sales. Vendors see only their own. Admin sees all after picking a company.

Sale detail buttons follow [USER_STORIES.md](./USER_STORIES.md) US-06 and US-07. Do not keep a separate “Orders” table or menu — **Sales** is the name.

## How the HTML is wired to the API

- Layout is static Metronic. Page JS calls `vilmo-api` (same Docker network; browser uses a public `/api` reverse proxy).
- No marketplace secrets in the browser. Tokens stay server-side; UI shows `is_enabled`, shop nickname, masked keys.
- DataTables for vendors, products, stock, **sales**. ApexCharts only if a dashboard chart is needed (not required for v1).
- TinyMCE only if product description needs rich text; otherwise a textarea.
- Label print: iframe + `window.print()` with `@page` size matching 100×150 mm (or 138×106). No A4 wrapper.
- **Ingerir NF-e camera:** `getUserMedia` + `BarcodeDetector` (fallback ZXing in the `vilmo-web` slice only). Decode in the tab. POST only the 44-digit chave. Stop tracks on close.

## Docker

```
vilmo-web:   # Metronic HTML UI, e.g. :8081
vilmo-api:   # already planned
```

Nginx **gateway** serves **`/`** (pt-BR) and **`/en/`** (en) from `deploy/site/` (SaaSify-look commercial index, US-17, static HTML). **`/web/`** is the Metronic app (`vilmo-web`) and sends `X-Robots-Tag: noindex`. **`/api`** proxies to `vilmo-api`. Do not 302 `/` to `/web/`.

## What not to do

- Do not run all 10 HTML demos or the React Vite/Next apps in production.
- Do not use `store-client` checkout as the seller console.
- Do not put `node_modules` or the full `template-metronic` tree inside the `vilmo-web` image.
- Do not duplicate Metronic into git twice; reference `template-metronic/` and copy the slice at build time.
- Do not upload webcam frames or photos of the DANFE to `vilmo-api`; only the parsed chave.
- Do not implement the warehouse scanner as a Metronic PWA instead of US-16; the native app is the encrypted-floor path.
