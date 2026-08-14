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
| Not used at runtime | Next.js landings, React demos, React starter, nine extra HTML demos, store-client **checkout/cart/wishlist** (B2C storefront, not seller admin). |

Runtime app (`src/Vilmo.Web` when implementing): copy **assets** (`css`, `js`, `media`, `vendors`) + **one layout** + **only the pages we map**. Talk to `vilmo-api` with `Authorization` + `X-Company-Id` + `Idempotency-Key` on writes.

Do not serve the 10GB-class template tree from Docker. `vilmo-web` is a small static/Razor site.

## Screen map (Vilmo domain → Metronic file)

| Vilmo screen | Metronic source to clone/adapt | Notes |
| --- | --- | --- |
| Sign in | `demo1/authentication/branded/sign-in.html` | POST `/auth/login`. Super user may omit company. |
| Sign up / invite | `demo1/authentication/branded/sign-up.html` | Staff/vendor invite, not public self-serve. |
| Reset password | `demo1/authentication/branded/reset-password/*` | |
| 404 / 500 | `demo1/authentication/error-404.html`, `error-500.html` | |
| Dashboard | starter `layout-1/index.html` + demo1 `index.html` widgets | Company-scoped KPIs: stock, open orders, listings. |
| Companies (super user) | `demo1/account/members/teams.html` | `GET /companies`. |
| Company switcher | header / teams dropdown in demo1 | Sets `X-Company-Id`. |
| Vendors list | `demo1/account/members/team-members-datatable.html` | `GET /companies/{id}/vendors`. |
| Create vendor | members starter + settings form | `POST /vendors` (idempotent). Creates `users_detail` + subaccounts. |
| Vendor detail (common) | `demo1/account/home/user-profile.html` + `settings-sidebar.html` | `users_detail`. |
| Vendor marketplace subaccount | `demo1/account/api-keys.html` + settings form | `user_detail_marketplace` key/values. Secrets masked. |
| Roles / permissions | `demo1/account/members/roles.html`, `permissions-toggle.html` | `UserProfile` / `CompanyRole`. |
| Company marketplace config | `demo1/account/integrations.html` | `company_marketplace_config` + parameters. |
| A1 certificate | settings form + Dropzone | `POST /companies/{id}/certificate`. |
| Products | store-inventory **Product List / Details / Create** (HTML tables from demo1 members datatable + store-client `product-details.html` for the detail chrome) | Company catalog. |
| Inventory | store-inventory **All Stock / Current / Inbound / Outbound** | NF-e movements. Inbound = purchase CFOP; outbound = sale CFOP. |
| Ingest NF-e | custom form on layout-1 (chave 44 + XML Dropzone) | `POST /nfe/chaves/{chave}/ingest`, `POST /nfe/xml`. |
| Advertisements | product list + “publish” modal with marketplace checkboxes | Default **all** channels; optional `marketplaceCodes`. |
| Orders | store-inventory **Order List / Details** | Webhook-imported, mapped to vendor subaccount. |
| Account security | `demo1/account/security/overview.html` | Password, sessions. |

Sidebar (replace Metronic demo links with these, nothing else): Dashboard, NF-e / Inventory, Products, Advertisements, Orders, Vendors, Marketplaces, Settings. Super user also sees Companies.

Vendor users see only their advertisements, their `users_detail`, and their subaccounts. `CompanyAdmin` sees the company. Super user sees all after picking a company.

## How the HTML is wired to the API

- Layout is static Metronic. Page JS calls `vilmo-api` (same Docker network; browser uses a public `/api` reverse proxy).
- No marketplace secrets in the browser. Tokens stay server-side; UI shows `is_enabled`, shop nickname, masked keys.
- DataTables for vendors, products, stock, orders. ApexCharts only if a dashboard chart is needed (not required for v1).
- TinyMCE only if product description needs rich text; otherwise a textarea.

## Docker

```
vilmo-web:   # Metronic HTML UI, e.g. :8081
vilmo-api:   # already planned
```

Nginx or YARP in `vilmo-web` (or a gateway) serves `/` from Metronic and proxies `/api` to `vilmo-api`.

## What not to do

- Do not run all 10 HTML demos or the React Vite/Next apps in production.
- Do not use `store-client` checkout as the seller console.
- Do not put `node_modules` or the full `template-metronic` tree inside the `vilmo-web` image.
- Do not duplicate Metronic into git twice; reference `template-metronic/` and copy the slice at build time.
