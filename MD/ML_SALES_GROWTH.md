# Mercado Livre sales growth plan

Commercial plan to grow sales **inside Mercado Livre**, using Mercado Ads first and Google / other tools where they actually pay. Worked example: the stainless-steel finger protector listing below.

This is an **operating plan**, not an engineering spec. Product Ads still go through the seller panel (or later a Vilmo Ads adapter). Listing publish, photos, Kit BOM, and NF-e stay in Vilmo. Do **not** call Mercado Ads or Google Ads from ERP / WMS / PWA logic; any future automation uses the marketplace gateway + adapter layer in [MarketPlaceEngine.MD](./MarketPlaceEngine.MD).

---

## 1. Worked example

| Field | Value |
| --- | --- |
| Listing | [Protetor de dedos aço inoxidável — corte legumes / faca seguro](https://www.mercadolivre.com.br/protetor-dedos-aco-inoxidavel-corte-legumes-faca-seguro/up/MLBU1969589861?pdp_filters=item_id%3AMLB3191936628&wid=MLB3191936628) |
| User Product (catalog family) | `MLBU1969589861` |
| Seller offer (item) | `MLB3191936628` |
| Category shape | Low-ticket kitchen gadget, many copycat offers, catalog / User Product page |
| Ads fingerprint | URL already carries `matt_tool=38524122` (Mercado Ads click tracker). Treat this SKU as **already in the ads ecosystem**, not a blank slate. |

**What this SKU is commercially:** a commodity safety accessory. Shoppers compare price, freight, and seller reputation on a **shared catalog page**, not a unique brand story. Paid clicks amplify that offer. They do not invent demand.

**Implication:** ads without a contribution-margin spreadsheet will burn cash. The same SKU as a **kit** (protector + peeler + board, or a 2-pack) often beats solo Product Ads because ticket and margin can clear ML’s sub-R$79 fee cliff.

---

## 2. Decision order (do not skip)

```text
1. Unit economics (Simulador de Custos + COGS + packaging)
2. Listing quality (photos, attributes, title, shipping, reputation)
3. Win or stay competitive on the catalog offer (price + ME2 + stock)
4. Product Ads (automatic → custom, ROAS Objetivo)
5. Google as keyword research; paid Google only if TACOS still works
6. Raise ticket (kits / packs) and copy the playbook to Shopee / Magalu
```

Mercado Ads **multiplies conversion you already have**. A weak photo, incomplete ficha, yellow reputation, or ME2 not adopted will raise CPC and kill ROAS even with a high budget.

---

## 3. Unit economics (gate before any spend)

Fill this from **Vendas → Simulador de custos** on the live offer, not from a blog table. 2026 MLB fees move by category, listing type (Clássico vs Premium), and a **variable per-unit logistics charge** on cheap items (historically a fixed fee under R$79; from Mar 2026 many accounts see a weight/size table instead). Kitchen gadgets in Casa typically sit around **12–14% Clássico** and **17–19% Premium**, plus that per-unit charge.

### 3.1 Contribution after marketplace, before ads

```text
Net after ML = Price
  − COGS (product + inbound to your warehouse)
  − Packaging
  − MLB commission % (Clássico or Premium)
  − Per-unit logistics / cheap-item fee (Simulador)
  − ME2 / Full / free-shipping cost you actually pay
  − Tax on the sale (if not already in COGS)
```

If **Net after ML ≤ 0**, do not advertise. Fix price, listing type, freight, or SKU mix first.

### 3.2 Break-even ROAS (the only ROAS that matters)

```text
Contribution margin % = Net after ML / Price
Break-even ROAS     = 1 / contribution margin %
Break-even ACOS     = contribution margin %
```

Example (illustrative only — replace with Simulador numbers):

| Line | Example |
| --- | --- |
| Price | R$ 14,90 |
| COGS + pack | R$ 4,50 |
| Commission 13% Clássico | R$ 1,94 |
| Cheap-item / logistics fee | R$ 5,50 |
| ME2 cost you absorb | R$ 0,00 (buyer pays) or more |
| **Net after ML** | **~R$ 2,96** |
| Contribution | ~20% |
| Break-even ROAS | **~5x** (ACOS 20%) |

On this class of SKU the fee often **eats more than the commission**. That is why a 2-pack or kit at R$ 29–39 can be more advertisable than a R$ 12 single.

**Target ROAS Objetivo** in Product Ads must sit **above** break-even, not on it:

| Intent | Typical ROAS Objetivo band | Meaning |
| --- | --- | --- |
| Volume / catalog attack | Just above break-even (e.g. 4–6x if margin allows) | Accept thinner ads profit to win share |
| Balanced growth | ~5–10x | Default after 14–28 days of data |
| Harvest / profit | >10x | Only when organic rank is already strong |

Panel “suggested ROAS” is a category average. Use your spreadsheet.

### 3.3 Clássico vs Premium on this SKU

For a sub-R$30 gadget, **Clássico usually keeps more profit**. Premium’s extra ~5 pp commission plus 12x interest-free is built for higher tickets. Test Premium only if conversion lift (and catalog win rate) more than pays the extra fee — measure 14 days, not overnight.

Full (fulfillment) is an organic-rank lever, not a default. Enable Full only if inbound + storage still leave contribution after the rank lift. Many gadget sellers stay on **Mercado Envios (ME2) local stock** plus Ads.

---

## 4. Layer A — listing quality (organic + ads share this)

Product Ads has **no separate creative**. The sponsored card is title + first photo + price + shipping from the item. Fix the listing before the campaign.

### 4.1 Catalog / User Product

This URL is a **User Product** (`/up/MLBU…`) with `wid=MLB3191936628` selecting your offer. Shoppers land on a family page and pick an offer.

- Do **not** clone extra items for the same catalog product (splits history, confuses Ads).
- Compete on **price, shipping promise, stock, reputation**, not on a unique title the catalog overwrites.
- Keep stock continuous. Stockouts reset sales velocity and Ads learning.

### 4.2 Title and attributes (index = targeting)

Product Ads has **no negative keywords**. Search matching comes from **title + ficha técnica + category**.

Buyer language for this SKU (use in title / attributes; confirm with Trends + ML search autocomplete):

- `protetor de dedos`, `protetor de dedo para cortar`, `guarda dedos`
- `proteção corte legumes`, `seguro faca`, `cortar cebola`
- `aço inoxidável`, `kitchen finger guard`

Title pattern (when the item still owns its title, not fully catalog-locked):

```text
[Produto] + [material] + [uso] + [diferencial]
Protetor de Dedos Aço Inoxidável para Corte de Legumes Faca Seguro
```

Do **not** keep rewriting a live title that already sells. Title edits reset relevance. If the current title is wrong, publish a **new** well-indexed item and migrate Ads, then pause the old one.

Fill **100% of required attributes**. Incomplete ficha = lost filters = lost impression share even with budget.

### 4.3 Photos (Vilmo already uploads these)

- Photo 1: pure white background, product sharp, ≥1200 px.
- Then: on-hand cutting vegetables, size vs knife, stainless finish, pack contents.
- No watermarks, no “FRETE GRÁTIS” text on the image (hurts quality score / can trip policy).
- Max 12 pictures; Vilmo `Imagens ML` is the source Mercado Livre fetches as `pictures.source`.

### 4.4 Operations that Ads cannot fix

| Signal | Why it matters |
| --- | --- |
| Reputation yellow+ (green better) | Product Ads eligibility; ad-rank |
| ME2 adopted (`me2_adoption_mandatory`) | Listing can stay active; shipping in the card |
| Answer questions fast (TMR) | Conversion and rank |
| Cancel / claim rate low | Ad-score and account health |
| No overdue Ads invoice | Campaigns pause |

Until ME2 and photos are clean, **do not scale budget**.

---

## 5. Layer B — Mercado Ads (primary paid channel)

Official intro: [Introdução ao Mercado Ads](https://developers.mercadolivre.com.br/pt_br/introducao-ao-mercado-ads). Seller UI: **Vendas → Publicidade** (some accounts: **Marketing → Publicidade**).

### 5.1 Which product to buy

| Product | Use on this SKU? |
| --- | --- |
| **Product Ads (PADS)** | **Yes. Default.** CPC, “Patrocinado” in search, category lists, and **competitor PDPs**. No extra creative. |
| **Brand Ads (BADS)** | Later, only with Minha Página + 3+ items and real brand search. Finger-guard generics rarely justify posição 0. |
| **Display (Minha Página auto)** | **No.** Unsegmented CPM; typical TACOS trap. |
| **Display (account manager)** | Only if ML assigns a salesperson and you have brand budget. Out of scope for one gadget. |

### 5.2 Eligibility (official)

- Yellow reputation or better (Decola active can waive some sales/reputation rules).
- Account ≥ ~15 days; minimum sales (often 1 for company, 10 for individual — confirm in panel).
- At least one **active catalog-eligible listing**.
- No overdue Ads / payment verification; no counterfeit restriction.

### 5.3 First campaign for `MLB3191936628`

1. Open **Product Ads** → create campaign, **automatic** mode.
2. Include this item (and only SKUs with **similar contribution margin**). Do not mix a 50% margin kit with a 8% gadget in the same ROAS target.
3. Set **ROAS Objetivo** above break-even (section 3.2). Range in API/panel: **1x–35x**.
4. Daily budget: enough for **learning**, not a token R$ 5. Rule of thumb: enough to buy **~10–20 clicks/day** at category CPC (kitchen gadgets often well under R$ 1/click; confirm in panel). If **% impressões perdidas por orçamento** stays high after a week, raise budget before touching ROAS.
5. **Do not edit** title, main photo, ROAS, or budget daily. Learning is **≥14 days**; a cleaner read is **28 days**. Attribution window is **14 days after click**.

After learning:

- High **lost impression share by ad rank** → listing quality / price / reputation, not more budget.
- High **lost impression share by budget** → raise daily cap if TACOS is healthy.
- ROAS above target and TACOS rising → you are buying sales that would have been organic; **tighten ROAS Objetivo** (higher number = less aggressive).
- ROAS below break-even after 28 days → pause this SKU in Ads; fix offer or move spend to a kit.

### 5.4 Automatic vs custom

| Mode | When |
| --- | --- |
| Automatic | First 14–28 days; few SKUs; discovery of which queries convert |
| Custom | Several SKUs; split **gadgets** vs **kits** vs **seasonal**; different ROAS per group |

API strategy labels `VISIBILITY` / `INCREASE` / `PROFITABILITY` exist on Product Ads API; the seller panel usually only exposes **ROAS Objetivo**. Same math.

### 5.5 Metrics you actually manage

From the Ads panel:

- Clicks, impressions, CTR
- % impressões ganhas
- % perdidas por **orçamento** vs **classificação**
- Spend, attributed revenue, **ROAS** (and ACOS = 1/ROAS)

**You must calculate outside the panel:**

```text
TACOS = Ads spend / (organic + paid GMV) for the same SKU and period
```

- Healthy scale: Ads GMV up, **TACOS stable or falling**, total GMV up.
- Trap: Ads ROAS looks fine while **total** units are flat (cannibalizing organic).

Export Ads metrics + Vilmo/ML sales for the item id weekly.

### 5.6 Product Ads API (later, Vilmo adapter)

When automating, use current Product Ads resources only (legacy item/campaign URLs are being retired; confirm [developers.mercadolivre.com.br](https://developers.mercadolivre.com.br) before coding). Pattern:

```http
GET /advertising/advertisers?product_id=PADS
Authorization: Bearer {token}
Api-Version: 1
```

Then campaign / ads / metrics under `/advertising/advertisers/{id}/product_ads/...`.

OAuth is the **same seller token** already used to publish items. Store tokens in Redis with TTL; refresh in the worker. Feature-flag Ads sync per company. **Idempotency** on campaign create keys: `(companyId, advertiserId, campaignExternalId)`.

Do not build this until the commercial loop in the panel is profitable for 28 days.

---

## 6. Layer C — Google tools (research first, paid second)

Google is **not** a substitute for Product Ads on Mercado Livre. In-marketplace demand is won in ML’s auction. Google is for **query research**, **seasonality**, and **optional** extra demand.

### 6.1 Use now (free / cheap)

| Tool | How to use for this SKU |
| --- | --- |
| [Google Trends](https://trends.google.com.br/trends/explore?geo=BR&q=protetor%20de%20dedos,guarda%20dedos,protetor%20corte) | Brasil, 12 months + 5 years. Compare `protetor de dedos` vs `guarda dedos` vs `protetor corte legumes`. Note Christmas / June festa / “refeição saudável” spikes. Put winning phrases in **title/attributes**, not in a new Ads keyword list (PADS has none). Switch Trends type to **Google Shopping** to see shopping intent vs informational. |
| [Keyword Planner](https://ads.google.com/home/lib_zulu/keyword-planner/) | Volume and suggested CPC for the same phrases. High Google CPC + low ML ticket ⇒ **do not** buy Google Search to the ML PDP. Low volume phrases still help ML SEO. |
| ML search bar autocomplete | Type `protetor` / `guarda dedos` / `cortar legumes` as a buyer. Those strings beat Google when they differ. |
| Google Lens / Shopping (manual) | See who ranks for the physical product; copy **photo style**, not logos. |

### 6.2 Google Ads pointing at the ML PDP

Technically allowed: Search ads can land on `mercadolivre.com.br/...`. Commercially weak for this SKU:

- No conversion tag / GA4 on the PDP → smart bidding is blind.
- No remarketing list you own.
- Google shows **one advertiser per domain** per query; you fight ML’s own ads and other sellers on `mercadolivre.com.br`.
- Ticket ~R$15 cannot pay Google Search CPC in many kitchen queries.

**Rule:** only a small branded Search test (your seller name + product) if Keyword Planner CPC × expected conversion still beats break-even. Pause if you cannot reconcile clicks to `MLB3191936628` orders within 14 days.

### 6.3 Performance Max / Merchant Center

PMax and free Shopping listings need a **Merchant Center feed on a site you control** (price, GTIN, availability matching the landing page). They do **not** reliably promote a third-party ML URL as Shopping inventory.

Use PMax when Vilmo (or Nuvemshop/Shopify) has a **owned checkout** and the same SKU. Feed custom labels by **margin**, not by vanity. Until that store exists, skip PMax.

### 6.4 YouTube / Demand Gen / Performance creative

Useful only after a **kit or brand** can support a R$0,10–0,30 view and a landing page you measure. A 15-second “how not to cut your finger” clip can feed **ML video on the listing** (organic conversion) even if you never buy YouTube.

---

## 7. Layer D — other demand (after ML Ads is stable)

| Channel | Role |
| --- | --- |
| **Shopee / Magalu** | Same SKU, same photos. Extra GMV without Google CPC. Vilmo already plans multi-marketplace publish. |
| **Meta Ads** | Catalog sales to an owned store, or traffic to ML only as a measured test. Advantage+ on a R$15 gadget usually loses to Product Ads. |
| **WhatsApp / status** | Reorder and kit upsell after ML sale (customer service, not cold ads). |
| **MadeiraMadeira / niches** | Same product already appears on home/DIY sites; only if contribution after their fee is better than ML TACOS. |

Do not split a tiny budget across four paid channels. **Win Product Ads + catalog offer first.**

---

## 8. Raise the ticket (highest-leverage commercial move)

Solo finger guards are a **fee-taxed** SKU. Vilmo Kit BOM / kit advertisements exist to publish a **bundle** as one listing.

Examples that usually advertise better:

- 2-pack or 3-pack of the same protector
- Kit: protetor + descascador + tábua pequena
- “Segurança na cozinha” kit with higher perceived value and price **≥ R$ 79** if you want to leave the cheap-item fee band (run Simulador; free shipping rules also change at that threshold)

Keep the original `MLB3191936628` as a **catalog / long-tail** offer; put **Ads budget on the kit** once photos and ficha are complete.

---

## 9. 90-day operating cadence

### Days 1–7 — foundation

- Pull Simulador de Custos for `MLB3191936628`; fill section 3.
- Confirm ME2, photos, attributes, stock, reputation, no Ads invoice debt.
- Trends + Keyword Planner + ML autocomplete → freeze title/attribute language.
- If net after ML is negative: change price, Clássico/Premium, or kit. No Ads.

### Days 8–21 — first Product Ads

- Automatic campaign, this SKU (or kit-only if singles cannot clear break-even).
- ROAS Objetivo above break-even; budget for learning.
- No creative/title thrash.

### Days 22–45 — read the account, not the ad

- Compute TACOS vs Ads ROAS.
- Split custom campaign if a kit is live.
- Fix **ad-rank** problems in the listing; fix **budget** problems in the cap.
- Optional: tiny Google Search brand test only if CPC math works.

### Days 46–90 — scale what works

- Raise budget on campaigns with TACOS in range and rising **total** units.
- Pause SKUs below break-even ROAS.
- Replicate listing + Ads playbook to Shopee/Magalu.
- Only then consider Brand Ads / owned-store PMax.

---

## 10. Success criteria (skeptical bar)

The plan is working when **all** of these are true for a 28-day window vs the prior 28 days:

1. Total units of `MLB3191936628` (and kits) **up**, not only Ads-attributed units.
2. Ads ROAS **≥ break-even ROAS** from the spreadsheet.
3. TACOS **not exploding** (set a cap, e.g. TACOS ≤ half of contribution margin).
4. Lost impression share by **classification** trending down (quality), or explained by a conscious price position.
5. Reputation and claims **not worse** (paid traffic onto a bad operation burns the account).

If Ads ROAS looks great and total units are flat, you bought your own organic sales. Tighten ROAS Objetivo or pause.

---

## 11. Vilmo follow-ups (product, not this document)

When the seller-panel loop is profitable, a later increment can add a **Mercado Ads adapter** (not a core ERP call):

- `listAdvertisers(productId=PADS)`
- `upsertCampaign(CampaignDTO)` / `setRoasTarget` / `setDailyBudget`
- `fetchMetrics(itemId, dateRange)` into company-scoped tables
- Feature flag per merchant; webhook/queue for spend alerts; Redis token cache

Until then, operators use **Vendas → Publicidade** and paste weekly metrics next to Vilmo sales for the same `remoteListingId`.

---

## Official and seller references

- [Introdução ao Mercado Ads](https://developers.mercadolivre.com.br/pt_br/introducao-ao-mercado-ads)
- [Requisitos Product Ads (Central de vendedores)](https://vendedores.mercadolivre.com.br/aprender/nota/quais-requisitos-devo-cumprir-para-usar-o-product-ads)
- Seller path: Vendas → Publicidade → Product Ads
- Simulador de custos: inside the listing / seller central (source of truth for 2026 fees)
- [Google Trends](https://trends.google.com.br) · [Keyword Planner](https://ads.google.com/home/lib_zulu/keyword-planner/)
- Architecture: [MarketPlaceEngine.MD](./MarketPlaceEngine.MD) · marketplace publish: [PLAN.md](./PLAN.md)
