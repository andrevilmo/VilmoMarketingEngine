#!/usr/bin/env python3
"""Generate PT/EN commercial HTML for vilmo-gateway. Run: python3 deploy/site/gen.py"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
ORIGIN = "https://vilmomkt.com"
CNPJ = "68.431.371/0001-61"
EMAIL = "admin@vilmomkt.com"
PHONE = "(51) 8022-7183"
FANTASIA = "VILMO COMERCIO, REPRESENTACOES E INFORMATICA"
RAZAO = "A. VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA"
ADDRESS = "R VITOR KONDER, 223, SALA 1108, CENTRO, FLORIANÓPOLIS/SC, CEP 88015-400"

NAV_PT = [
    ("#benefits", "Benefícios"),
    ("#companies", "Empresas"),
    ("#vendors", "Vendedores"),
    ("#dropshipping", "Dropshipping"),
    ("#how", "Como funciona"),
    ("#contact", "Contato"),
]
NAV_EN = [
    ("#benefits", "Benefits"),
    ("#companies", "Companies"),
    ("#vendors", "Vendors"),
    ("#dropshipping", "Dropshipping"),
    ("#how", "How it works"),
    ("#contact", "Contact"),
]


def esc(s: str) -> str:
    return (
        s.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def write(rel: str, content: str) -> None:
    path = ROOT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.strip() + "\n", encoding="utf-8")
    print("wrote", rel)


def chrome(lang: str, path_pt: str, path_en: str, is_home: bool) -> tuple[str, str, str]:
    pt = lang == "pt-BR"
    login = "Entrar" if pt else "Sign in"
    menu = "Abrir menu" if pt else "Open menu"
    skip = "Ir para o conteúdo" if pt else "Skip to content"
    nav = NAV_PT if pt else NAV_EN
    home = "/" if pt else "/en/"
    links = []
    for href, label in nav:
        url = href if is_home else home + href
        links.append(f'<a href="{url}">{esc(label)}</a>')
    nav_html = "".join(links)
    pt_cur = ' aria-current="page"' if pt else ""
    en_cur = ' aria-current="page"' if not pt else ""
    header = f'''
<a class="skip" href="#main">{esc(skip)}</a>
<header class="header">
  <div class="wrap header-inner">
    <a class="logo" href="{home}"><span class="logo-mark">V</span> VILMO</a>
    <nav class="nav" aria-label="{"Principal" if pt else "Primary"}">{nav_html}</nav>
    <div class="header-right">
      <nav class="lang" aria-label="{"Idioma" if pt else "Language"}">
        <a href="{path_pt}" hreflang="pt-BR"{pt_cur}>PT</a>
        <span aria-hidden="true">|</span>
        <a href="{path_en}" hreflang="en"{en_cur}>EN</a>
      </nav>
      <a class="btn btn-primary" href="/web/">{esc(login)}</a>
      <button class="menu-btn" type="button" data-menu aria-expanded="false" aria-label="{esc(menu)}">☰</button>
    </div>
  </div>
</header>'''
    cookie_policy = "/cookies/" if pt else "/en/cookies/"
    cookie = f'''
<div id="cookie-bar" class="cookie-bar" role="dialog" aria-label="{"Cookies" if pt else "Cookies"}">
  <div class="wrap cookie-inner">
    <p>{"Usamos cookies necessários para o site funcionar. Opcionais (analytics) ficam desligados até você aceitar." if pt else "We use necessary cookies so the site works. Optional (analytics) cookies stay off until you accept."}</p>
    <div class="cookie-actions">
      <a href="{cookie_policy}">{"Política de cookies" if pt else "Cookie policy"}</a>
      <button class="btn btn-primary" type="button" data-cookie="accept">{"Aceitar todos" if pt else "Accept all"}</button>
      <button class="btn btn-ghost" type="button" data-cookie="reject">{"Recusar opcionais" if pt else "Reject optional"}</button>
      <button class="btn btn-ghost" type="button" data-cookie="prefs">{"Preferências" if pt else "Preferences"}</button>
    </div>
    <div id="cookie-prefs" class="cookie-prefs" hidden>
      <label><input type="checkbox" checked disabled> {"Necessários (sempre ligados)" if pt else "Necessary (always on)"}</label>
      <label><input type="checkbox" id="cookie-optional"> {"Opcionais" if pt else "Optional"}</label>
      <button class="btn btn-indigo" type="button" data-cookie="save-prefs">{"Salvar" if pt else "Save"}</button>
    </div>
  </div>
</div>'''
    privacy = "/privacidade/" if pt else "/en/privacy/"
    terms = "/termos/" if pt else "/en/terms/"
    cookies = cookie_policy
    contact = "/contato/" if pt else "/en/contact/"
    footer = f'''
<footer class="footer">
  <div class="wrap">
    <div class="footer-grid">
      <div>
        <a class="logo" href="{"/" if pt else "/en/"}"><span class="logo-mark">V</span> VILMO</a>
        <p>{esc(FANTASIA)}<br>{esc(RAZAO)}<br>CNPJ {CNPJ}<br>{esc(ADDRESS)}</p>
      </div>
      <div>
        <strong>{"Site" if pt else "Site"}</strong>
        <p><a href="{"/" if pt else "/en/"}">{"Início" if pt else "Home"}</a><br>
        <a href="/web/">{esc(login)}</a><br>
        <a href="{contact}">{"Contato" if pt else "Contact"}</a></p>
      </div>
      <div>
        <strong>{"Legal" if pt else "Legal"}</strong>
        <p><a href="{privacy}">{"Privacidade" if pt else "Privacy"}</a><br>
        <a href="{terms}">{"Termos de uso" if pt else "Terms of use"}</a><br>
        <a href="{cookies}">{"Cookies" if pt else "Cookies"}</a><br>
        <button class="btn btn-ghost" type="button" data-cookie="reopen">{"Gerenciar cookies" if pt else "Manage cookies"}</button></p>
      </div>
    </div>
    <div class="footer-legal">
      <span>CNPJ {CNPJ}</span>
      <a href="mailto:{EMAIL}">{EMAIL}</a>
      <span>{PHONE}</span>
      <span>Florianópolis/SC</span>
    </div>
  </div>
</footer>
{cookie}'''
    return header, footer, skip


def page(
    *,
    lang: str,
    title: str,
    description: str,
    canonical: str,
    path_pt: str,
    path_en: str,
    body: str,
    og_locale: str,
    og_alt: str,
    extra_jsonld: list | None = None,
    is_home: bool = False,
) -> str:
    pt = lang == "pt-BR"
    header, footer, _ = chrome(lang, path_pt, path_en, is_home)
    org = {
        "@context": "https://schema.org",
        "@type": "Organization",
        "name": RAZAO,
        "alternateName": FANTASIA,
        "url": ORIGIN + "/",
        "email": EMAIL,
        "telephone": "+55-51-8022-7183",
        "vatID": CNPJ.replace(".", "").replace("/", "").replace("-", ""),
        "identifier": CNPJ,
        "address": {
            "@type": "PostalAddress",
            "streetAddress": "R VITOR KONDER, 223, SALA 1108",
            "addressLocality": "Florianópolis",
            "addressRegion": "SC",
            "postalCode": "88015-400",
            "addressCountry": "BR",
        },
    }
    graph = [org]
    if extra_jsonld:
        graph.extend(extra_jsonld)
    jsonld = json.dumps({"@context": "https://schema.org", "@graph": graph}, ensure_ascii=False)
    return f'''<!DOCTYPE html>
<html lang="{lang}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{esc(title)}</title>
  <meta name="description" content="{esc(description)}">
  <link rel="canonical" href="{ORIGIN}{canonical}">
  <link rel="alternate" hreflang="pt-BR" href="{ORIGIN}{path_pt}">
  <link rel="alternate" hreflang="en" href="{ORIGIN}{path_en}">
  <link rel="alternate" hreflang="x-default" href="{ORIGIN}{path_pt}">
  <meta property="og:type" content="website">
  <meta property="og:site_name" content="Vilmo">
  <meta property="og:title" content="{esc(title)}">
  <meta property="og:description" content="{esc(description)}">
  <meta property="og:url" content="{ORIGIN}{canonical}">
  <meta property="og:locale" content="{og_locale}">
  <meta property="og:locale:alternate" content="{og_alt}">
  <meta property="og:image" content="{ORIGIN}/img/og.png">
  <meta property="og:image:width" content="1200">
  <meta property="og:image:height" content="630">
  <meta name="twitter:card" content="summary_large_image">
  <link rel="icon" href="/img/favicon.svg" type="image/svg+xml">
  <link rel="stylesheet" href="/css/site.css">
  <script type="application/ld+json">{jsonld}</script>
</head>
<body>
{header}
<main id="main">
{body}
</main>
{footer}
<script src="/js/site.js" defer></script>
</body>
</html>
'''


def home_pt() -> str:
    faqs = [
        (
            "A página inicial é o login?",
            "Não. vilmomkt.com/ é o site comercial. Entrar (canto superior direito) abre /web/, a tela de login do console.",
        ),
        (
            "Quem vê o estoque?",
            "Só Admin e usuário da empresa. O vendedor não abre Estoque, certificado A1 nem os segredos de conexão da empresa.",
        ),
        (
            "Quais marketplaces?",
            "Mercado Livre, Shopee, SHEIN e Magalu. A empresa liga os canais; cada vendedor conecta as lojas dele.",
        ),
        (
            "NF-e de entrada e de saída são a mesma coisa?",
            "Não. A entrada (chave 44 ou XML) aumenta o estoque da empresa. A saída é emitida depois que a venda fica Paga.",
        ),
    ]
    faq_html = "".join(
        f"<details><summary>{esc(q)}</summary><p>{esc(a)}</p></details>" for q, a in faqs
    )
    extra = [
        {
            "@type": "WebSite",
            "name": "Vilmo",
            "url": ORIGIN + "/",
            "inLanguage": "pt-BR",
        },
        {
            "@type": "SoftwareApplication",
            "name": "Vilmo",
            "applicationCategory": "BusinessApplication",
            "operatingSystem": "Web",
            "url": ORIGIN + "/",
            "offers": {"@type": "Offer", "price": "0", "priceCurrency": "BRL"},
        },
        {
            "@type": "FAQPage",
            "mainEntity": [
                {"@type": "Question", "name": q, "acceptedAnswer": {"@type": "Answer", "text": a}}
                for q, a in faqs
            ],
        },
    ]
    body = f'''
<section class="hero" id="benefits">
  <div class="hero-orbs" aria-hidden="true"><div class="orb orb-a"></div><div class="orb orb-b"></div><div class="orb orb-c"></div></div>
  <div class="wrap hero-grid">
    <div>
      <p class="kicker">Marketplace + NF-e</p>
      <h1>Estoque por NF-e. Vendas nos marketplaces.</h1>
      <p class="lede">Uma plataforma para a <strong>empresa CNPJ</strong> ser dona do estoque (nota fiscal de entrada) e os <strong>vendedores</strong> anunciarem no Mercado Livre, Shopee, SHEIN e Magalu — sem ver o inventário da empresa.</p>
      <div class="hero-cta">
        <a class="btn btn-primary" href="#how">Como funciona</a>
        <a class="btn btn-indigo" href="/web/">Entrar</a>
      </div>
      <p class="hero-note">Operado por {esc(RAZAO)} · CNPJ {CNPJ}</p>
    </div>
    <aside class="hero-card" aria-label="Resumo do produto">
      <h3>Console da empresa</h3>
      <div class="fake-ui">
        <div class="fake-row"></div>
        <div class="fake-row w-70"></div>
        <div class="fake-row w-40"></div>
        <div class="fake-row"></div>
      </div>
      <div class="pills">
        <span class="pill">Estoque NF-e</span>
        <span class="pill">Vendedores</span>
        <span class="pill">Pago → NF-e saída</span>
        <span class="pill">Etiqueta 10×15</span>
      </div>
    </aside>
  </div>
</section>
<div class="trust">
  <div class="wrap">
    <p>Canais de lançamento</p>
    <ul class="trust-list">
      <li>Mercado Livre</li><li>Shopee</li><li>SHEIN</li><li>Magalu</li>
    </ul>
  </div>
</div>
<section class="section alt" id="companies">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Empresas</p>
      <h2>O CNPJ dono do estoque e da nota</h2>
      <p>A empresa liga os canais, guarda o certificado A1, define o preço de venda e emite a NF-e de saída. Vários vendedores, um livro de estoque.</p>
    </div>
    <div class="grid-3">
      <article class="card"><div class="icon">1</div><h3>Estoque real por NF-e</h3><p>A chave de 44 dígitos ou o XML de entrada aumenta o saldo da empresa. Não é catálogo de fornecedor terceiro.</p></article>
      <article class="card"><div class="icon">2</div><h3>Preço de venda</h3><p>Admin e usuário da empresa ajustam o preço. Quando a venda fica Paga, o saldo cai pela quantidade.</p></article>
      <article class="card"><div class="icon">3</div><h3>NF-e e etiqueta</h3><p>Depois de Pago: emitir NF-e e imprimir etiqueta 10×15 cm. Um CNPJ, muitos vendedores.</p></article>
    </div>
  </div>
</section>
<section class="section" id="vendors">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Vendedores</p>
      <h2>Loja própria, sem ver o depósito</h2>
      <p>O vendedor gerencia anúncios, vendas e subcontas de marketplace. Não abre estoque, A1 nem segredos da empresa.</p>
    </div>
    <div class="grid-3">
      <article class="card"><h3>Meus anúncios</h3><p>Publica nos canais já ligados à empresa e à subconta dele.</p></article>
      <article class="card"><h3>Minhas vendas</h3><p>Status em português. Só as vendas desse vendedor — não as dos colegas.</p></article>
      <article class="card"><h3>Minhas lojas</h3><p>OAuth / chaves da subconta. Pendente até conectar. Sem acesso ao certificado fiscal.</p></article>
    </div>
  </div>
</section>
<section class="section alt" id="dropshipping">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Dropshipping</p>
      <h2>Empresa no depósito. Vendedores na vitrine.</h2>
      <p>Modelo honesto: estoque da empresa (NF-e) + vários vendedores nos marketplaces. Não é rede de fornecedores terceiros. O saldo canônico impede dois vendedores de venderem o mesmo item além do estoque.</p>
    </div>
    <div class="grid-3">
      <article class="card"><h3>Identidade fiscal</h3><p>A NF-e de saída sai no CNPJ da empresa. O vendedor é a vitrine, não o emitente.</p></article>
      <article class="card"><h3>Sem oversell</h3><p>Pago decrementa o estoque da empresa. Dois anúncios compartilham o mesmo saldo.</p></article>
      <article class="card"><h3>Sincroniza depois do Pago</h3><p>Marketplaces recebem o fluxo depois que o status canônico vira Pago — não no clique do checkout.</p></article>
    </div>
  </div>
</section>
<section class="section" id="how">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Como funciona</p>
      <h2>Quatro passos até a etiqueta</h2>
    </div>
    <div class="steps">
      <article class="step"><div class="step-n">1</div><h3>Empresa conecta</h3><p>Canais + certificado A1. Pronta para listar e faturar.</p></article>
      <article class="step"><div class="step-n">2</div><h3>Vendedores ligam lojas</h3><p>Cada vendedor conecta as subcontas nos marketplaces habilitados.</p></article>
      <article class="step"><div class="step-n">3</div><h3>NF-e enche o estoque</h3><p>Chave 44, XML ou app Vilmo NF-e. Saldo só daquele CNPJ.</p></article>
      <article class="step"><div class="step-n">4</div><h3>Pago → NF-e + etiqueta</h3><p>Estoque cai. Emite a nota. Imprime 10×15 para o Correios.</p></article>
    </div>
  </div>
</section>
<section class="section alt" id="faq">
  <div class="wrap">
    <div class="section-head"><p class="kicker">FAQ</p><h2>Perguntas frequentes</h2></div>
    <div class="faq">{faq_html}</div>
  </div>
</section>
<section class="section" id="company">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Quem opera</p>
      <h2>Empresa responsável pelo site</h2>
    </div>
    <div class="company-block">
      <div class="card">
        <dl>
          <dt>Nome fantasia</dt><dd>{esc(FANTASIA)}</dd>
          <dt>Razão social</dt><dd>{esc(RAZAO)}</dd>
          <dt>CNPJ</dt><dd>{CNPJ}</dd>
          <dt>Endereço</dt><dd>{esc(ADDRESS)}</dd>
          <dt>E-mail</dt><dd><a href="mailto:{EMAIL}">{EMAIL}</a></dd>
          <dt>Telefone</dt><dd>{PHONE}</dd>
        </dl>
      </div>
      <div class="card" id="contact">
        <h3>Contato</h3>
        <p>Dúvidas, privacidade e exercício de direitos LGPD: escreva para <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
        <a class="btn btn-primary" href="mailto:{EMAIL}">Enviar e-mail</a>
      </div>
    </div>
  </div>
</section>
<div class="wrap" style="padding-bottom:4rem">
  <div class="cta-band">
    <div>
      <h2>Pronto para entrar?</h2>
      <p>O console (estoque, vendas, NF-e) fica em /web/. Esta página é só o site comercial.</p>
    </div>
    <a class="btn btn-indigo" href="/web/">Entrar</a>
  </div>
</div>
'''
    return page(
        lang="pt-BR",
        title="Vilmo — estoque por NF-e e vendas nos marketplaces",
        description="Plataforma para a empresa CNPJ dona do estoque (NF-e) e vendedores no Mercado Livre, Shopee, SHEIN e Magalu. CNPJ 68.431.371/0001-61.",
        canonical="/",
        path_pt="/",
        path_en="/en/",
        body=body,
        og_locale="pt_BR",
        og_alt="en_US",
        extra_jsonld=extra,
        is_home=True,
    )


def home_en() -> str:
    faqs = [
        (
            "Is the homepage the login screen?",
            "No. vilmomkt.com/en/ is the commercial site. Sign in (top right) opens /web/, the console login.",
        ),
        (
            "Who can see stock?",
            "Admin and company users only. A vendor cannot open stock, the A1 certificate, or company connection secrets.",
        ),
        (
            "Which marketplaces?",
            "Mercado Livre, Shopee, SHEIN, and Magalu. The company enables channels; each vendor connects their own shops.",
        ),
        (
            "Is inbound NF-e the same as outbound?",
            "No. Inbound (44-digit key or XML) increases company stock. Outbound NF-e is issued after the sale is Paid.",
        ),
    ]
    faq_html = "".join(
        f"<details><summary>{esc(q)}</summary><p>{esc(a)}</p></details>" for q, a in faqs
    )
    extra = [
        {"@type": "WebSite", "name": "Vilmo", "url": ORIGIN + "/en/", "inLanguage": "en"},
        {
            "@type": "SoftwareApplication",
            "name": "Vilmo",
            "applicationCategory": "BusinessApplication",
            "operatingSystem": "Web",
            "url": ORIGIN + "/en/",
            "offers": {"@type": "Offer", "price": "0", "priceCurrency": "BRL"},
        },
        {
            "@type": "FAQPage",
            "mainEntity": [
                {"@type": "Question", "name": q, "acceptedAnswer": {"@type": "Answer", "text": a}}
                for q, a in faqs
            ],
        },
    ]
    body = f'''
<section class="hero" id="benefits">
  <div class="hero-orbs" aria-hidden="true"><div class="orb orb-a"></div><div class="orb orb-b"></div><div class="orb orb-c"></div></div>
  <div class="wrap hero-grid">
    <div>
      <p class="kicker">Marketplace + NF-e</p>
      <h1>Stock from NF-e. Sales on marketplaces.</h1>
      <p class="lede">One platform so a <strong>CNPJ company</strong> owns inventory (inbound electronic invoice) and <strong>vendors</strong> sell on Mercado Livre, Shopee, SHEIN, and Magalu — without seeing company stock.</p>
      <div class="hero-cta">
        <a class="btn btn-primary" href="#how">How it works</a>
        <a class="btn btn-indigo" href="/web/">Sign in</a>
      </div>
      <p class="hero-note">Operated by {esc(RAZAO)} · CNPJ {CNPJ}</p>
    </div>
    <aside class="hero-card" aria-label="Product snapshot">
      <h3>Company console</h3>
      <div class="fake-ui">
        <div class="fake-row"></div>
        <div class="fake-row w-70"></div>
        <div class="fake-row w-40"></div>
        <div class="fake-row"></div>
      </div>
      <div class="pills">
        <span class="pill">NF-e stock</span>
        <span class="pill">Vendors</span>
        <span class="pill">Paid → outbound NF-e</span>
        <span class="pill">10×15 label</span>
      </div>
    </aside>
  </div>
</section>
<div class="trust">
  <div class="wrap">
    <p>Launch channels</p>
    <ul class="trust-list">
      <li>Mercado Livre</li><li>Shopee</li><li>SHEIN</li><li>Magalu</li>
    </ul>
  </div>
</div>
<section class="section alt" id="companies">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Companies</p>
      <h2>The CNPJ that owns stock and invoices</h2>
      <p>The company connects channels, keeps the A1 certificate, sets sale price, and issues outbound NF-e. Many vendors, one stock book.</p>
    </div>
    <div class="grid-3">
      <article class="card"><div class="icon">1</div><h3>Real stock from NF-e</h3><p>A 44-digit access key or inbound XML increases company on-hand. Not a third-party supplier catalog.</p></article>
      <article class="card"><div class="icon">2</div><h3>Sale price</h3><p>Admin and company users set the price. When a sale becomes Paid, on-hand drops by qty.</p></article>
      <article class="card"><div class="icon">3</div><h3>NF-e and label</h3><p>After Paid: issue NF-e and print a 10×15 cm label. One CNPJ, many vendors.</p></article>
    </div>
  </div>
</section>
<section class="section" id="vendors">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Vendors</p>
      <h2>Your shop. Not the warehouse.</h2>
      <p>Vendors manage ads, sales, and marketplace subaccounts. They cannot open stock, A1, or company secrets.</p>
    </div>
    <div class="grid-3">
      <article class="card"><h3>My ads</h3><p>Publish on channels already enabled for the company and that vendor subaccount.</p></article>
      <article class="card"><h3>My sales</h3><p>Status in Portuguese in the app. Only that vendor’s sales — not a colleague’s.</p></article>
      <article class="card"><h3>My shops</h3><p>OAuth / subaccount keys. Pending until connected. No fiscal certificate access.</p></article>
    </div>
  </div>
</section>
<section class="section alt" id="dropshipping">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Dropshipping</p>
      <h2>Company in the warehouse. Vendors in the storefront.</h2>
      <p>Honest model: company-owned stock (NF-e) plus multiple marketplace vendors. Not a third-party supplier network. Canonical on-hand stops two vendors from overselling the same SKU.</p>
    </div>
    <div class="grid-3">
      <article class="card"><h3>Fiscal identity</h3><p>Outbound NF-e is issued under the company’s CNPJ. The vendor is the storefront, not the issuer.</p></article>
      <article class="card"><h3>No oversell</h3><p>Paid decrements company stock. Two ads share the same on-hand.</p></article>
      <article class="card"><h3>Sync after Paid</h3><p>Marketplaces follow the flow after canonical status is Paid — not on the checkout click.</p></article>
    </div>
  </div>
</section>
<section class="section" id="how">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">How it works</p>
      <h2>Four steps to the shipping label</h2>
    </div>
    <div class="steps">
      <article class="step"><div class="step-n">1</div><h3>Company connects</h3><p>Channels + A1 certificate. Ready to list and invoice.</p></article>
      <article class="step"><div class="step-n">2</div><h3>Vendors link shops</h3><p>Each vendor connects subaccounts on enabled marketplaces.</p></article>
      <article class="step"><div class="step-n">3</div><h3>NF-e fills stock</h3><p>44-digit key, XML, or the Vilmo NF-e app. Balances only for that CNPJ.</p></article>
      <article class="step"><div class="step-n">4</div><h3>Paid → NF-e + label</h3><p>Stock drops. Issue the invoice. Print 10×15 for Correios.</p></article>
    </div>
  </div>
</section>
<section class="section alt" id="faq">
  <div class="wrap">
    <div class="section-head"><p class="kicker">FAQ</p><h2>Frequently asked questions</h2></div>
    <div class="faq">{faq_html}</div>
  </div>
</section>
<section class="section" id="company">
  <div class="wrap">
    <div class="section-head">
      <p class="kicker">Who runs this</p>
      <h2>Company responsible for the site</h2>
    </div>
    <div class="company-block">
      <div class="card">
        <dl>
          <dt>Trade name</dt><dd>{esc(FANTASIA)}</dd>
          <dt>Legal name</dt><dd>{esc(RAZAO)}</dd>
          <dt>CNPJ</dt><dd>{CNPJ}</dd>
          <dt>Address</dt><dd>{esc(ADDRESS)}</dd>
          <dt>Email</dt><dd><a href="mailto:{EMAIL}">{EMAIL}</a></dd>
          <dt>Phone</dt><dd>{PHONE}</dd>
        </dl>
      </div>
      <div class="card" id="contact">
        <h3>Contact</h3>
        <p>Product questions, privacy, and LGPD rights: email <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
        <a class="btn btn-primary" href="mailto:{EMAIL}">Send email</a>
      </div>
    </div>
  </div>
</section>
<div class="wrap" style="padding-bottom:4rem">
  <div class="cta-band">
    <div>
      <h2>Ready to sign in?</h2>
      <p>The console (stock, sales, NF-e) lives at /web/. This page is the commercial site only.</p>
    </div>
    <a class="btn btn-indigo" href="/web/">Sign in</a>
  </div>
</div>
'''
    return page(
        lang="en",
        title="Vilmo — NF-e stock and marketplace sales",
        description="A platform so a CNPJ company owns inventory (NF-e) and vendors sell on Mercado Livre, Shopee, SHEIN, and Magalu. CNPJ 68.431.371/0001-61.",
        canonical="/en/",
        path_pt="/",
        path_en="/en/",
        body=body,
        og_locale="en_US",
        og_alt="pt_BR",
        extra_jsonld=extra,
        is_home=True,
    )


def legal_shell(lang: str, title: str, inner: str, canonical: str, path_pt: str, path_en: str, desc: str) -> str:
    pt = lang == "pt-BR"
    body = f'<section class="legal"><div class="wrap legal-doc"><h1>{esc(title)}</h1>{inner}</div></section>'
    return page(
        lang=lang,
        title=title + (" · Vilmo" if "Vilmo" not in title else ""),
        description=desc,
        canonical=canonical,
        path_pt=path_pt,
        path_en=path_en,
        body=body,
        og_locale="pt_BR" if pt else "en_US",
        og_alt="en_US" if pt else "pt_BR",
    )


def privacy_pt() -> str:
    inner = f"""
<p>Controlador: <strong>{esc(RAZAO)}</strong>, CNPJ {CNPJ}, {esc(ADDRESS)}. Contato: <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
<h2>O que coletamos</h2>
<p>Dados de conta (e-mail, nome, perfil), logs técnicos de acesso, e cookies necessários. Cookies opcionais só depois do aceite na barra.</p>
<h2>Para que usamos</h2>
<p>Autenticar usuários, operar o console de estoque/vendas/NF-e, segurança, cumprimento legal e contato pelo e-mail informado.</p>
<h2>Retenção</h2>
<p>Dados de conta enquanto o cadastro existir. Logs o tempo necessário para segurança e auditoria. Consentimento de cookies: 12 meses.</p>
<h2>Direitos (LGPD)</h2>
<p>Acesso, correção, exclusão, portabilidade e informação sobre compartilhamento: escreva para <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
<h2>Venda de dados</h2>
<p>Não vendemos dados pessoais.</p>
<h2>Subprocessadores (v1)</h2>
<p>Amazon Web Services (hospedagem, região sa-east-1) e Let's Encrypt (certificados TLS). Marketplaces (Mercado Livre, Shopee, SHEIN, Magalu) recebem dados de pedido quando você usa o console para vender nesses canais.</p>
"""
    return legal_shell("pt-BR", "Privacidade", inner, "/privacidade/", "/privacidade/", "/en/privacy/",
                       "Política de privacidade da Vilmo. Controlador CNPJ 68.431.371/0001-61, admin@vilmomkt.com.")


def privacy_en() -> str:
    inner = f"""
<p>Controller: <strong>{esc(RAZAO)}</strong>, CNPJ {CNPJ}, {esc(ADDRESS)}. Contact: <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
<h2>What we collect</h2>
<p>Account data (email, name, role), technical access logs, and necessary cookies. Optional cookies only after you accept on the banner.</p>
<h2>Why</h2>
<p>Authenticate users, run the stock/sales/NF-e console, security, legal duties, and contact via the email you give us.</p>
<h2>Retention</h2>
<p>Account data while the account exists. Logs as needed for security and audit. Cookie consent: 12 months.</p>
<h2>LGPD rights</h2>
<p>Access, correction, deletion, portability, and information about sharing: email <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
<h2>Sale of data</h2>
<p>We do not sell personal data.</p>
<h2>Subprocessors (v1)</h2>
<p>Amazon Web Services (hosting, sa-east-1) and Let's Encrypt (TLS certificates). Marketplaces (Mercado Livre, Shopee, SHEIN, Magalu) receive order data when you use the console to sell on those channels.</p>
"""
    return legal_shell("en", "Privacy", inner, "/en/privacy/", "/privacidade/", "/en/privacy/",
                       "Vilmo privacy policy. Controller CNPJ 68.431.371/0001-61, admin@vilmomkt.com.")


def terms_pt() -> str:
    inner = f"""
<p>Estes termos regem o uso de <strong>vilmomkt.com</strong> e do console em /web/, operados por {esc(RAZAO)}, CNPJ {CNPJ}.</p>
<h2>Licença de uso</h2>
<p>Concedemos uma licença limitada, não exclusiva, para usar o site e o aplicativo conforme o perfil da conta (admin, empresa ou vendedor).</p>
<h2>Contas</h2>
<p>Contas são criadas pelo operador ou pela empresa. Não há auto-cadastro público neste recorte. Você é responsável pelas credenciais.</p>
<h2>Uso aceitável</h2>
<p>Não tente acessar dados de outro CNPJ ou de outro vendedor, não abuse de APIs e não publique conteúdo ilícito nos canais conectados.</p>
<h2>Marketplaces são terceiros</h2>
<p>Mercado Livre, Shopee, SHEIN e Magalu têm termos próprios. Vilmo não é esses marketplaces.</p>
<h2>SLA</h2>
<p>A versão inicial não promete disponibilidade percentual. Trabalhamos para manter o serviço no ar.</p>
<h2>Lei e foro</h2>
<p>Lei brasileira. Foro da comarca de Florianópolis/SC.</p>
<p>Contato: <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
"""
    return legal_shell("pt-BR", "Termos de uso", inner, "/termos/", "/termos/", "/en/terms/",
                       "Termos de uso de vilmomkt.com. Lei brasileira, foro Florianópolis/SC. CNPJ 68.431.371/0001-61.")


def terms_en() -> str:
    inner = f"""
<p>These terms govern <strong>vilmomkt.com</strong> and the /web/ console, operated by {esc(RAZAO)}, CNPJ {CNPJ}.</p>
<h2>Licence</h2>
<p>We grant a limited, non-exclusive licence to use the site and app according to the account level (admin, company, or vendor).</p>
<h2>Accounts</h2>
<p>Accounts are created by the operator or the company. There is no public self-serve signup in this slice. You are responsible for credentials.</p>
<h2>Acceptable use</h2>
<p>Do not access another CNPJ’s or another vendor’s data, do not abuse APIs, and do not post unlawful listings on connected channels.</p>
<h2>Marketplaces are third parties</h2>
<p>Mercado Livre, Shopee, SHEIN, and Magalu have their own terms. Vilmo is not those marketplaces.</p>
<h2>SLA</h2>
<p>v1 does not promise a percentage uptime. We work to keep the service available.</p>
<h2>Law and venue</h2>
<p>Brazilian law. Venue: Florianópolis/SC.</p>
<p>Contact: <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
"""
    return legal_shell("en", "Terms of use", inner, "/en/terms/", "/termos/", "/en/terms/",
                       "Terms of use for vilmomkt.com. Brazilian law, venue Florianópolis/SC. CNPJ 68.431.371/0001-61.")


def cookies_pt() -> str:
    inner = f"""
<p>Controlador: {esc(RAZAO)}, CNPJ {CNPJ}. Contato: <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
<p>Necessários ficam sempre ligados. Opcionais começam <strong>desligados</strong> até Aceitar. Não pré-marcamos o aceite.</p>
<table>
  <thead><tr><th>Nome</th><th>Tipo</th><th>Finalidade</th><th>Prazo</th></tr></thead>
  <tbody>
    <tr><td>vilmo_cookie_consent</td><td>Necessário</td><td>Guardar a escolha da barra (necessário / opcional)</td><td>12 meses</td></tr>
    <tr><td>localStorage vilmo_cookie_consent</td><td>Necessário</td><td>Mesmo registro no aparelho</td><td>12 meses</td></tr>
    <tr><td>Analytics (v1)</td><td>Opcional</td><td>Nenhum script de terceiro é carregado neste recorte, mesmo após Aceitar. O gancho existe para o futuro.</td><td>—</td></tr>
  </tbody>
</table>
<p>Para mudar a escolha, use <button type="button" data-cookie="reopen">Gerenciar cookies</button> ou limpe os dados do site no navegador.</p>
<p>Detalhes também na <a href="/privacidade/">política de privacidade</a>.</p>
"""
    return legal_shell("pt-BR", "Política de cookies", inner, "/cookies/", "/cookies/", "/en/cookies/",
                       "Cookies da Vilmo: necessários sempre on, opcionais off até aceitar. CNPJ 68.431.371/0001-61.")


def cookies_en() -> str:
    inner = f"""
<p>Controller: {esc(RAZAO)}, CNPJ {CNPJ}. Contact: <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>
<p>Necessary cookies stay on. Optional cookies start <strong>off</strong> until Accept. We do not pre-tick consent.</p>
<table>
  <thead><tr><th>Name</th><th>Type</th><th>Purpose</th><th>TTL</th></tr></thead>
  <tbody>
    <tr><td>vilmo_cookie_consent</td><td>Necessary</td><td>Store the banner choice (necessary / optional)</td><td>12 months</td></tr>
    <tr><td>localStorage vilmo_cookie_consent</td><td>Necessary</td><td>Same record on the device</td><td>12 months</td></tr>
    <tr><td>Analytics (v1)</td><td>Optional</td><td>No third-party script loads in this slice, even after Accept. The hook exists for later.</td><td>—</td></tr>
  </tbody>
</table>
<p>To change your choice, use <button type="button" data-cookie="reopen">Manage cookies</button> or clear site data in the browser.</p>
<p>See also the <a href="/en/privacy/">privacy policy</a>.</p>
"""
    return legal_shell("en", "Cookie policy", inner, "/en/cookies/", "/cookies/", "/en/cookies/",
                       "Vilmo cookies: necessary always on, optional off until accept. CNPJ 68.431.371/0001-61.")


def contact_pt() -> str:
    inner = f"""
<p>Fale com o controlador do site.</p>
<p><strong>{esc(FANTASIA)}</strong><br>{esc(RAZAO)}<br>CNPJ {CNPJ}<br>{esc(ADDRESS)}<br>
<a href="mailto:{EMAIL}">{EMAIL}</a> · {PHONE}</p>
<p><a class="btn btn-primary" href="mailto:{EMAIL}">Enviar e-mail</a></p>
"""
    return legal_shell("pt-BR", "Contato", inner, "/contato/", "/contato/", "/en/contact/",
                       "Contato Vilmo: admin@vilmomkt.com, CNPJ 68.431.371/0001-61, Florianópolis/SC.")


def contact_en() -> str:
    inner = f"""
<p>Contact the site controller.</p>
<p><strong>{esc(FANTASIA)}</strong><br>{esc(RAZAO)}<br>CNPJ {CNPJ}<br>{esc(ADDRESS)}<br>
<a href="mailto:{EMAIL}">{EMAIL}</a> · {PHONE}</p>
<p><a class="btn btn-primary" href="mailto:{EMAIL}">Send email</a></p>
"""
    return legal_shell("en", "Contact", inner, "/en/contact/", "/contato/", "/en/contact/",
                       "Vilmo contact: admin@vilmomkt.com, CNPJ 68.431.371/0001-61, Florianópolis/SC.")


def not_found() -> str:
    body = """
<section class="legal">
  <div class="wrap legal-doc">
    <h1>Página não encontrada</h1>
    <p>Esse endereço não existe. Volte ao <a href="/">início</a> ou à <a href="/en/">English home</a>.</p>
    <p>CNPJ 68.431.371/0001-61 · <a href="mailto:admin@vilmomkt.com">admin@vilmomkt.com</a></p>
  </div>
</section>
"""
    return page(
        lang="pt-BR",
        title="Página não encontrada · Vilmo",
        description="A página pedida não existe em vilmomkt.com.",
        canonical="/404.html",
        path_pt="/",
        path_en="/en/",
        body=body,
        og_locale="pt_BR",
        og_alt="en_US",
    )


def main() -> None:
    write("index.html", home_pt())
    write("en/index.html", home_en())
    write("privacidade/index.html", privacy_pt())
    write("termos/index.html", terms_pt())
    write("cookies/index.html", cookies_pt())
    write("contato/index.html", contact_pt())
    write("en/privacy/index.html", privacy_en())
    write("en/terms/index.html", terms_en())
    write("en/cookies/index.html", cookies_en())
    write("en/contact/index.html", contact_en())
    write("404.html", not_found())


if __name__ == "__main__":
    main()
