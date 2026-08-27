# How to create CNPJ accounts for each marketplace

Operator guide for first company **VILMO COMERCIO, REPRESENTACOES E INFORMATICA** (nome fantasia) / **A. VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA** (razão social), CNPJ `68431371000161`. Seed details: [FirstCompany.md](./FirstCompany.md). This is not application code.

There are always **two layers**:

| Layer | What it is | Where it lives in Vilmo |
| --- | --- | --- |
| **Seller shop** | The CNPJ store that sells on the marketplace | `user_detail_marketplace` (vendor subaccount) and/or company shop |
| **Developer / Open Platform app** | Credentials so *our* API can talk to that marketplace | `company_marketplace_parameter` (`ClientId`, `PartnerId`, secrets) |

Create the **seller CNPJ account first**, then the **developer app**, then authorize the shop. Use the **same CNPJ** and the **same legal representative** on every channel. Brazil (ML, Magalu) will reject apps if holder data does not match the account.

Have on hand before starting:

- CNPJ card (Comprovante de Inscrição)
- Contrato social or CCMEI (MEI)
- RG/CNH of the legal representative + selfie if asked
- Proof of business address
- **Conta bancária PJ** in the same CNPJ
- Inscrição estadual when the UF requires it
- Certificate **A1** (already planned for SEFAZ) for NF-e on Magalu/Shopee/SHEIN sales

---

## 1. Mercado Livre

Official:

- Seller: https://www.mercadolivre.com.br (create **conta empresa**)
- Developers: https://developers.mercadolivre.com.br
- Create app: https://developers.mercadolivre.com.br/en_us/python/register-your-application
- OAuth: https://developers.mercadolivre.com.br/pt_br/publicacao-de-produtos/obtencao-do-access-token

### 1.1 Seller account (CNPJ)

1. Open https://www.mercadolivre.com.br → **Crie a sua conta** → **conta empresa / para o seu negócio**.
2. Enter **CNPJ**, razão social, business email, password.
3. Validate CNPJ and complete holder data (must match Receita). In Brazil you cannot create a DevCenter app until holder data is validated.
4. Upload ID of the person who may act for the CNPJ, plus company documents if asked.
5. Configure fiscal data (IE, address) and a **PJ bank account**.
6. Optional: if an old CPF shop exists, use **Meus dados → alterar titularidade** (reputation is kept). Prefer a **new CNPJ shop** for Vilmo if there is no history to keep.

Test users: after the app exists, create test users from DevCenter so you do not sell on the real CNPJ while integrating.

### 1.2 Developer application (Vilmo)

Log in to DevCenter with the **CNPJ owner** account (not a personal CPF).

1. https://developers.mercadolivre.com.br → **Minhas aplicações** → **Criar nova aplicação**.
2. Unique name, description (≤ 150 chars), logo.
3. **Redirect URIs**: HTTPS only, e.g. `https://<vilmo-public>/oauth/MercadoLivre/callback` (must match exactly).
4. Scopes: **read** + **write**. For background sync add **offline_access** (refresh token).
5. Enable **Receive notifications**. Callback: `https://<vilmo-public>/webhooks/MercadoLivre` (must answer HTTP 200 in 500 ms).
6. Topics at least: `orders_v2`, `items`, `shipments`, `stock_locations`.
7. Save. Copy **APP ID (ClientId)** and **Client Secret** into `company_marketplace_parameter` (`is_secret` on the secret). Never commit them.

Authorize the seller:

1. Browser: `https://auth.mercadolivre.com.br/authorization?response_type=code&client_id=<APP_ID>&redirect_uri=<URI>&state=<companyId>`.
2. Log in as the **CNPJ seller**.
3. Vilmo reads `state` as the company id and exchanges `code` at `POST https://api.mercadolibre.com/oauth/token`. The redirect URI must stay exactly `https://vilmomkt.com/oauth/MercadoLivre/callback` (no extra query in the registered URI).
4. Store `AccessToken`, `RefreshToken`, `UserId`. Site for Brazil: `SiteId=MLB`.

---

## 2. Shopee (Brazil)

Official:

- Seller: https://shopee.com.br (Central do Vendedor) — **Loja Empresarial (CNPJ)**
- Open Platform: https://open.shopee.com
- Register developer: https://open.shopee.com/developer-guide/12
- Brazil API host: `https://openplatform.shopee.com.br/api/v2/`
- NF-e on orders: https://open.shopee.com/developer-guide/382

### 2.1 Seller account (CNPJ)

1. Open Central do Vendedor → register **Loja Empresarial (CNPJ)** (not Loja Pessoal/CPF).
2. Phone/email, verification code, password.
3. Company data, store address, **PJ bank account**.
4. Send documents if asked. Wait for validation.
5. Brazil allows multiple shops per CNPJ (platform cap; keep categories distinct). Each extra shop is another Vilmo vendor subaccount.

**Open API gate (Brazil):** Registered business documents **and at least 1 order in the last 30 days**. Make a real (or allowed test) sale before applying to Open Platform, or the seller-type application is rejected.

CNPJ sellers must upload **NF-e XML** on invoice-pending orders (`v2.order.upload_invoice_doc`, `file_type=4`). Wait ~5 minutes after SEFAZ authorization (SERPRO).

### 2.2 Open Platform (Vilmo)

Vilmo as **our own shops** → account type **Shopee Seller → Registered Business Seller**.  
Vilmo as **software for many sellers** → **Third-party Partner Platform (ISV)** (needs a live HTTPS product + trial login; ~10 business days).

For the first company (own CNPJ shops), use **Registered Business Seller**.

1. https://open.shopee.com → **Sign up** with a **stable email** (cannot change).
2. Confirm code, set password.
3. Console → App Management → App List → **Shopee Seller** → log in with the **CNPJ seller** shop → type **Registered Business Seller**.
4. Wait for approval (~3 business days for seller type).
5. Create an App. Copy **partner_id** and **partner_key**.
6. Brazil live calls: `https://openplatform.shopee.com.br`.
7. Push callback: `https://<vilmo-public>/webhooks/Shopee` (verify HMAC).
8. Authorization: generate the shop auth URL, seller accepts, exchange code at `POST .../api/v2/auth/token/get`.
9. Store `PartnerId`, `PartnerKey` (company), `ShopId`, `AccessToken`, `RefreshToken` (subaccount). Access token ~4 hours; refresh ~30 days.

Individual Seller on Open Platform is **closed for Brazil**. Do not pick it.

---

## 3. SHEIN (Brazil)

Official:

- Seller portal: https://seller-br.shein.com/
- Open Platform: https://open.sheincorp.com/en (PT: https://open.sheincorp.com/pt)
- Signature notes: https://open.sheincorp.com/documents/system/passwdrule
- API host: `https://openapi.sheincorp.com`

SHEIN Brazil **requires CNPJ** for new sellers (CPF is not accepted for new shops). Full OpenAPI docs are **behind login** after the app is approved.

### 3.1 Seller account (CNPJ)

1. Open https://seller-br.shein.com/ → seller registration.
2. Business nature: **CNPJ**.
3. Company data, categories, volumes, bank PJ.
4. Upload typically: CNPJ card, contrato social/CCMEI, ID front/back + selfie of the owner, photos of office/warehouse, sometimes last-year GMV / other marketplace proof.
5. Wait for review (often several business days). Login may arrive by SMS.
6. Activate the shop, configure logistics (SHEIN fulfill vs seller fulfill).

### 3.2 Open Platform (Vilmo)

Cooperation process on the portal:

1. Open Platform **account application**
2. **Create application**
3. **Application review**
4. **Seller authorization**
5. **Solution integration** (product / order / webhook)

Until review passes, seed SHEIN in Vilmo as `is_active = false`.

After approval:

1. App **APP_ID** / **APP_Secret** (company parameters).
2. Seller auth → `get-by-token` (signed with APP credentials) → `openKeyId` + `secretKey` for that merchant.
3. Later calls: HMAC headers `x-lt-openKeyId`, `x-lt-timestamp`, `x-lt-signature`.
4. Store vendor keys in `user_detail_marketplace` (`is_secret` on `secretKey`).
5. Webhook URL: `https://<vilmo-public>/webhooks/Shein`.

Contact if the portal is stuck: `openapi@shein.com`.

---

## 4. Magalu (Magazine Luiza)

Official:

- Seller: https://universo.magalu.com/ — **Cadastrar minha loja**
- Portal Magalu / App **Mundo Seller** after approval
- Developers: https://developers.magalu.com/
- Create app: https://developers.magalu.com/docs/first-steps/create-an-application/
- OAuth: https://developers.magalu.com/docs/first-steps/create-an-application/authentication-authorization
- ID Magalu CLI: https://github.com/luizalabs/id-magalu-cli

**Magalu does not accept CPF shops.** CNPJ must be **active for more than three months**, same titularidade on CNPJ/bank/docs, and you must **emit NF-e**.

### 4.1 Seller account (CNPJ)

1. https://universo.magalu.com/ → **Cadastrar minha loja**.
2. Choose self-service or an integrator (Vilmo is the integrator later; first shop can be self-service).
3. Store data, legal contacts, financial data.
4. Upload documents. Wait for approval.
5. Operate in **Portal Magalu** / **Mundo Seller**. Login for API consent must be the **store (PJ) admin**, not a PF login.

### 4.2 Developer application (Vilmo)

1. Install **ID Magalu CLI** (`idm`) from https://github.com/luizalabs/id-magalu-cli.
2. `idm login` as the company that **owns Vilmo**.
3. Create a client, for example:

```text
idm client create \
  --name 'vilmo-marketing-engine' \
  --description 'Vilmo marketplace engine' \
  --redirect-uris 'https://<vilmo-public>/oauth/Magalu/callback' \
  --terms-of-use 'https://<vilmo-public>/terms' \
  --privacy-term 'https://<vilmo-public>/privacy' \
  --scopes-default 'open:portfolio-skus-seller:read open:portfolio-skus-seller:write open:portfolio-prices-seller:read open:portfolio-prices-seller:write open:portfolio-stocks-seller:read open:portfolio-stocks-seller:write'
```

Add order scopes when orders go live (`open:order-order:read` and write as documented).

4. Save `client_id` / `client_secret` as company parameters.
5. Seller clicks Magalu consent (or custom URL). They must use **credenciais da loja (PJ)**.
6. Exchange code: `POST https://id.magalu.com/oauth/token` (`grant_type=authorization_code`).
7. Access ~7200 s; persist `RefreshToken`. Webhooks: `https://<vilmo-public>/webhooks/Magalu`.

---

## 5. What to store in Vilmo after each signup

`marketplace.code` is a string (no enum). After each channel is live:

| Parameter | Mercado Livre | Shopee | SHEIN | Magalu |
| --- | --- | --- | --- | --- |
| Company (app) | `ClientId`, `ClientSecret` | `PartnerId`, `PartnerKey` | `AppId`, `AppSecret` | `ClientId`, `ClientSecret` |
| Vendor subaccount | `UserId`, `AccessToken`, `RefreshToken`, `SiteId=MLB` | `ShopId`, `AccessToken`, `RefreshToken` | `OpenKeyId`, `SecretKey` | `AccessToken`, `RefreshToken`, `Scope` |

Redirect/webhook URLs must use the same `{code}`: `MercadoLivre`, `Shopee`, `Shein`, `Magalu`.

Company (app) keys are **edited per company** on **Marketplaces da empresa** (US-15 in [USER_STORIES.md](./USER_STORIES.md)). They are not global env vars.

---

## 6. Suggested order for CNPJ `68431371000161`

1. **Mercado Livre** seller + DevCenter app (fastest OAuth sandbox/test users).
2. **Magalu** seller (wait if CNPJ is newer than 3 months) + `idm client create`.
3. **Shopee** CNPJ shop → at least one order in 30 days → Open Platform Registered Business Seller.
4. **SHEIN** seller-br + Open Platform application (review gated).

Do not share Client Secret / Partner Key / APP_Secret in git or chat. Put them in `company_marketplace_parameter` with `is_secret = true`.
