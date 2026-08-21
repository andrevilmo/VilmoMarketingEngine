/* Vilmo Metronic HTML client — talks to /api */
const Vilmo = (() => {
  const API = `${location.origin}/api`;
  const store = {
    get token() { return sessionStorage.getItem("vilmo_token"); },
    set token(v) { v ? sessionStorage.setItem("vilmo_token", v) : sessionStorage.removeItem("vilmo_token"); },
    get companyId() { return sessionStorage.getItem("vilmo_company"); },
    set companyId(v) { v ? sessionStorage.setItem("vilmo_company", v) : sessionStorage.removeItem("vilmo_company"); },
    me: null
  };
  let ingestLogTimer = null;

  function uuid() { return crypto.randomUUID(); }

  async function api(path, opts = {}) {
    const headers = Object.assign({ "Accept": "application/json" }, opts.headers || {});
    if (store.token) headers.Authorization = `Bearer ${store.token}`;
    if (store.companyId) headers["X-Company-Id"] = store.companyId;
    if (opts.body && !(opts.body instanceof FormData) && !headers["Content-Type"]) {
      headers["Content-Type"] = "application/json";
    }
    if (opts.method && opts.method !== "GET" && !headers["Idempotency-Key"]) headers["Idempotency-Key"] = uuid();
    const res = await fetch(API + path, { ...opts, headers, body: opts.body instanceof FormData || typeof opts.body === "string" ? opts.body : (opts.body ? JSON.stringify(opts.body) : undefined) });
    const text = await res.text();
    let data = null;
    try { data = text ? JSON.parse(text) : null; } catch { data = text; }
    if (res.status === 401) {
      store.token = null;
      if (!location.pathname.endsWith("login.html")) location.href = "login.html";
    }
    if (!res.ok) {
      const err = new Error((data && (data.message || data.error)) || res.statusText);
      err.status = res.status;
      err.data = data;
      throw err;
    }
    return data;
  }

  function bootLogin() {
    if (store.token) { location.href = "index.html"; return; }
    const form = document.getElementById("login-form");
    form.addEventListener("submit", async (e) => {
      e.preventDefault();
      const err = document.getElementById("login-error");
      err.classList.add("hidden");
      try {
        const body = await api("/auth/login", { method: "POST", body: { email: email.value, password: password.value } });
        store.token = body.accessToken;
        const mem = body.user.memberships || [];
        if (mem.length) store.companyId = mem[0].companyId;
        const level = body.user.level;
        location.href = level === "Admin" ? "index.html#/empresas" : (level === "Vendor" ? "index.html#/vendas" : "index.html#/dashboard");
      } catch (ex) {
        err.textContent = ex.message || "E-mail ou senha inválidos.";
        err.classList.remove("hidden");
      }
    });
  }

  const menus = {
    Admin: [
      ["empresas", "Empresas"],
      ["usuarios", "Usuários"],
      ["dashboard", "Dashboard"],
      ["estoque", "Estoque"],
      ["nfe", "Ingerir NF-e"],
      ["produtos", "Produtos"],
      ["anuncios", "Anúncios"],
      ["vendas", "Vendas"],
      ["vendedores", "Vendedores"],
      ["marketplaces", "Marketplaces"],
      ["config", "Configurações"]
    ],
    Company: [
      ["dashboard", "Dashboard"],
      ["estoque", "Estoque"],
      ["nfe", "Ingerir NF-e"],
      ["produtos", "Produtos"],
      ["anuncios", "Anúncios"],
      ["vendas", "Vendas"],
      ["vendedores", "Vendedores"],
      ["marketplaces", "Marketplaces"],
      ["config", "Configurações"]
    ],
    Vendor: [
      ["dashboard", "Dashboard"],
      ["vendas", "Minhas vendas"],
      ["anuncios", "Meus anúncios"],
      ["meus-marketplaces", "Meus marketplaces"],
      ["perfil", "Perfil"]
    ]
  };

  async function bootApp() {
    if (!store.token) { location.href = "login.html"; return; }
    store.me = await api("/me");
    if (!store.companyId && store.me.memberships?.[0]) store.companyId = store.me.memberships[0].companyId;
    renderChrome();
    bindMenuToggle();
    window.addEventListener("hashchange", () => {
      closeMobileMenu();
      renderRoute();
    });
    if (!location.hash) {
      location.hash = store.me.level === "Admin" ? "#/empresas" : (store.me.level === "Vendor" ? "#/vendas" : "#/dashboard");
    } else renderRoute();
    document.getElementById("logout").onclick = () => { store.token = null; location.href = "login.html"; };
  }

  function setMobileMenu(open) {
    const sidebar = document.getElementById("sidebar");
    const toggle = document.getElementById("menu-toggle");
    const backdrop = document.getElementById("sidebar-backdrop");
    if (!sidebar || !toggle || !backdrop) return;
    sidebar.classList.toggle("mobile-open", open);
    backdrop.classList.toggle("on", open);
    toggle.setAttribute("aria-expanded", open ? "true" : "false");
    toggle.setAttribute("aria-label", open ? "Fechar menu" : "Abrir menu");
    document.body.classList.toggle("sidebar-open", open);
  }

  function closeMobileMenu() { setMobileMenu(false); }

  function bindMenuToggle() {
    const sidebar = document.getElementById("sidebar");
    const toggle = document.getElementById("menu-toggle");
    const backdrop = document.getElementById("sidebar-backdrop");
    const closeBtn = document.getElementById("menu-close");
    if (!sidebar || !toggle || !backdrop || toggle.dataset.bound) return;
    toggle.dataset.bound = "1";
    const onToggle = (e) => {
      e.preventDefault();
      e.stopPropagation();
      setMobileMenu(!sidebar.classList.contains("mobile-open"));
    };
    toggle.addEventListener("click", onToggle);
    if (closeBtn) closeBtn.addEventListener("click", (e) => { e.preventDefault(); closeMobileMenu(); });
    backdrop.addEventListener("click", closeMobileMenu);
    sidebar.addEventListener("click", (e) => {
      if (e.target.closest("a.menu-link")) closeMobileMenu();
    });
    window.addEventListener("resize", () => {
      if (window.matchMedia("(min-width: 1024px)").matches) closeMobileMenu();
    });
  }

  function renderChrome() {
    document.getElementById("user-name").textContent = store.me.name || store.me.email;
    const badge = document.getElementById("role-badge");
    badge.textContent = store.me.level === "Admin" ? "Admin" : store.me.level === "Vendor" ? "Vendedor" : "Empresa";
    const sw = document.getElementById("company-switcher");
    if (store.me.level === "Admin") {
      sw.classList.remove("hidden");
      api("/companies").then(list => {
        sw.innerHTML = list.map(c => `<option value="${c.id}" ${c.id === store.companyId ? "selected" : ""}>${c.tradeName || c.legalName} · ${c.cnpjFormatted}</option>`).join("");
        sw.onchange = () => { store.companyId = sw.value; renderRoute(); };
      });
    }
    const nav = document.getElementById("menu");
    nav.innerHTML = menus[store.me.level].map(([id, label]) =>
      `<a class="menu-link" data-route="${id}" href="#/${id}">${label}</a>`).join("");
  }

  function page(title, extra, body) {
    return `<div class="flex items-center justify-between mb-5">
      <h1 class="text-xl font-semibold">${title}</h1>
      <div>${extra || ""}</div>
    </div>${body}`;
  }

  function table(headers, rows) {
    return `<div class="vilmo-card overflow-auto"><table class="vilmo-table"><thead><tr>${headers.map(h => `<th>${h}</th>`).join("")}</tr></thead>
      <tbody>${rows.length ? rows.join("") : `<tr><td colspan="${headers.length}">Nenhum registro.</td></tr>`}</tbody></table></div>`;
  }

  function esc(s) {
    return String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }

  function fmtWhen(iso) {
    if (!iso) return "";
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? String(iso) : d.toLocaleString("pt-BR");
  }

  function adErr(code) {
    return ({
      MarketplaceRequired: "Selecione ao menos um marketplace.",
      UnknownMarketplace: "Marketplace não habilitado para esta empresa.",
      FamilyNameRequired: "Informe o nome da família do anúncio.",
      FamilyNameTooLong: "Nome da família deve ter no máximo 60 caracteres.",
      TitleRequired: "Informe o título do anúncio.",
      QueryRequired: "Preencha o título ou o nome da família para sugerir a categoria.",
      CategoryNotFound: "Categoria do Mercado Livre não encontrada.",
      CategorySuggestFailed: "Não foi possível sugerir a categoria no Mercado Livre.",
      InvalidCategoryId: "Categoria ML deve ser o código da opção (ex.: MLB5672), não o nome.",
      InvalidListingTypeId: "Tipo de anúncio ML deve ser o código da opção (ex.: gold_special), não um nome livre.",
      SizeChartNotFound: "Tabela de medidas do Mercado Livre não encontrada.",
      InvalidSizeChartId: "Informe o ID da guia de tamanhos do Mercado Livre.",
      InvalidSizeGridId: "Escolha a guia e a linha nas listas do Mercado Livre. SIZE_GRID_ROW_ID deve ser a linha (ex.: 26008:1), não o mesmo número da guia.",
      MlAttributeRequired: "Preencha os atributos obrigatórios da categoria do Mercado Livre.",
      NoChannels: "Este anúncio não tem canais.",
      AdvertisementNotFound: "Anúncio não encontrado.",
      ListingNotFound: "Canal não encontrado neste anúncio."
    })[code] || code;
  }

  const mlListingFallback = [
    { id: "gold_special", name: "Clássica" },
    { id: "gold_pro", name: "Premium" },
    { id: "free", name: "Gratuita" },
    { id: "gold", name: "Ouro" },
    { id: "silver", name: "Prata" },
    { id: "bronze", name: "Bronze" }
  ];
  const mlListingTypeCode = (raw) => {
    const t = String(raw || "").trim();
    const paren = t.match(/\(([a-z][a-z0-9]*(?:_[a-z0-9]+)*)\)\s*$/i);
    const id = (paren ? paren[1] : t).toLowerCase();
    if (!/^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/.test(id)) return "";
    if (id.includes("_") || ["gold", "silver", "bronze", "free"].includes(id)) return id;
    return "";
  };
  const mlListingTypeOptions = (items, selected) => {
    const sel = mlListingTypeCode(selected) || "gold_special";
    return (items || []).map(t => {
      const id = t.id || t.Id;
      const name = t.name || t.Name || id;
      return `<option value="${esc(id)}"${id === sel ? " selected" : ""}>${esc(name)} (${esc(id)})</option>`;
    }).join("");
  };

  function stopIngestLogPoll() {
    if (ingestLogTimer) {
      clearInterval(ingestLogTimer);
      ingestLogTimer = null;
    }
  }

  function prettyTech(raw) {
    if (!raw) return "{}";
    try { return JSON.stringify(JSON.parse(raw), null, 2); } catch { return String(raw); }
  }

  function stepTitle(code) {
    return ({
      received: "Pedido recebido",
      credentials: "Credenciais",
      authorize: "Autorização",
      redirect: "Login do canal",
      callback: "Resposta do canal",
      exchanging: "Troca do código",
      calling: "Envio ao canal",
      applied: "Concluído",
      failed: "Falhou",
      saved: "Salvo",
      validated: "Validado",
      queued: "Na fila",
      worker_started: "Worker",
      publish: "Publicar"
    })[code] || code || "Passo";
  }

  function techPanel(raw) {
    if (!raw) return `<pre>{}</pre>`;
    let obj = null;
    try { obj = typeof raw === "string" ? JSON.parse(raw) : raw; } catch { return `<pre>${esc(String(raw))}</pre>`; }
    if (!obj || typeof obj !== "object") return `<pre>${esc(prettyTech(typeof raw === "string" ? raw : JSON.stringify(raw)))}</pre>`;
    const request = obj.request || obj.sent || null;
    const response = obj.response || obj.callbackResponse || null;
    const rest = { ...obj };
    delete rest.request; delete rest.sent; delete rest.response; delete rest.callbackResponse;
    if (!request && !response) return `<pre>${esc(JSON.stringify(obj, null, 2))}</pre>`;
    let html = "";
    if (request) html += `<div class="tech-block"><div class="tech-block-label">Enviado</div><pre>${esc(JSON.stringify(request, null, 2))}</pre></div>`;
    if (response) html += `<div class="tech-block"><div class="tech-block-label">Resposta</div><pre>${esc(JSON.stringify(response, null, 2))}</pre></div>`;
    if (Object.keys(rest).length) html += `<div class="tech-block"><div class="tech-block-label">Contexto</div><pre>${esc(JSON.stringify(rest, null, 2))}</pre></div>`;
    return html;
  }

  function stepAccordionHtml(title, steps, { open = false, dataAttr = "" } = {}) {
    const list = Array.isArray(steps) ? steps : [];
    const last = list[0];
    const lastLabel = last ? last.userMessage : "Nenhum passo ainda";
    const rows = list.length
      ? list.map((l, i) => {
          const when = l.createdAt ? fmtWhen(l.createdAt) : "";
          return `<li class="ad-log-step ${esc(l.level || "")} ${i === 0 ? "latest" : ""}">
            <div class="ad-log-step-head">
              <span class="ingest-level ${esc(l.level || "")}">${esc(l.level || "")}</span>
              <span class="text-xs text-muted-foreground">${esc(when)}</span>
              ${i === 0 ? `<span class="badge-latest">último</span>` : ""}
            </div>
            <div class="font-medium text-sm">${esc(stepTitle(l.stepCode))}</div>
            <div>${esc(l.userMessage || "")}</div>
            <details class="ingest-tech">
              <summary>Ver técnico (enviado / resposta)</summary>
              ${techPanel(l.technicalJson)}
            </details>
          </li>`;
        }).join("")
      : `<li class="text-sm text-muted-foreground">Nenhum passo ainda.</li>`;
    return `<details class="ad-log" ${open ? "open" : ""} ${dataAttr}>
      <summary>${esc(title)} <span class="ad-log-last text-xs text-muted-foreground">· ${esc(lastLabel)}</span></summary>
      <ol class="ad-log-steps">${rows}</ol>
    </details>`;
  }

  function ingestProgressMeta(step, level) {
    const order = ["received", "xml_received", "validated", "queued", "replayed", "worker_started", "waiting_distdfe", "certificate_missing", "xml_parsed", "stock_applied"];
    const idx = order.indexOf(step);
    const pct = idx < 0 ? (level === "error" ? 100 : 15) : Math.round(((idx + 1) / order.length) * 100);
    const cls = level === "error" ? "error" : (level === "warning" ? "warn" : "");
    return { pct, cls };
  }

  function renderIngestLogs(payload) {
    const items = (payload && payload.items) || [];
    const progressEl = document.getElementById("ingest-log-progress");
    const tableEl = document.getElementById("ingest-log-table");
    if (!progressEl || !tableEl) return;
    if (!items.length) {
      progressEl.innerHTML = `<div class="label text-sm text-muted-foreground">Nenhuma ingestão registrada ainda para esta empresa.</div>`;
      tableEl.innerHTML = `<div class="overflow-auto"><table class="vilmo-table"><thead><tr><th>Quando</th><th>Situação</th><th>Mensagem</th><th>Detalhes técnicos</th></tr></thead>
        <tbody><tr><td colspan="4">Nenhum registro.</td></tr></tbody></table></div>`;
      return;
    }
    const latest = items[0];
    const meta = ingestProgressMeta(latest.stepCode, latest.level);
    progressEl.innerHTML = `<div class="label"><strong>Último passo:</strong> ${esc(latest.userMessage)}</div>
      <div class="ingest-log-bar ${meta.cls}"><span style="width:${meta.pct}%"></span></div>`;
    const rows = items.map((l, i) => {
      const when = l.createdAt ? new Date(l.createdAt).toLocaleString("pt-BR") : "";
      const tech = prettyTech(l.technicalJson);
      const latestMark = i === 0 ? `<span class="badge-latest">último passo</span>` : "";
      return `<tr class="${i === 0 ? "ingest-log-latest" : ""}">
        <td class="whitespace-nowrap">${esc(when)}</td>
        <td><span class="ingest-level ${esc(l.level)}">${esc(l.level)}</span><div class="text-xs text-muted-foreground">${esc(l.stepCode)}${latestMark}</div></td>
        <td>${esc(l.userMessage)}${l.chaveAcesso ? `<div class="text-xs text-muted-foreground">${esc(l.chaveAcesso)}</div>` : ""}</td>
        <td><details class="ingest-tech"><summary>Ver técnico</summary><pre>${esc(tech)}</pre></details></td>
      </tr>`;
    });
    tableEl.innerHTML = `<div class="overflow-auto"><table class="vilmo-table"><thead><tr><th>Quando</th><th>Situação</th><th>Mensagem</th><th>Detalhes técnicos</th></tr></thead>
      <tbody>${rows.join("")}</tbody></table></div>`;
  }

  async function loadIngestLogs(opts = {}) {
    const tableEl = document.getElementById("ingest-log-table");
    if (!tableEl) return;
    const params = new URLSearchParams();
    if (opts.runId) params.set("runId", opts.runId);
    const chaveEl = document.getElementById("chave");
    const chave = (chaveEl && chaveEl.value || "").replace(/\D/g, "");
    if (chave.length === 44) params.set("chave", chave);
    const qs = params.toString() ? `?${params}` : "";
    try {
      const data = await api(`/nfe/ingest-logs${qs}`);
      renderIngestLogs(data);
    } catch (ex) {
      tableEl.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(ex.message)}</div>`;
    }
  }

  function startIngestLogPoll(runId) {
    stopIngestLogPoll();
    loadIngestLogs({ runId });
    let ticks = 0;
    ingestLogTimer = setInterval(() => {
      ticks += 1;
      loadIngestLogs({ runId });
      if (ticks >= 20) stopIngestLogPoll();
    }, 2000);
  }

  async function renderRoute() {
    stopIngestLogPoll();
    const view = document.getElementById("view");
    const route = (location.hash.replace("#/", "").split("?")[0] || "dashboard");
    document.querySelectorAll(".menu-link").forEach(a => a.classList.toggle("active", a.dataset.route === route.split("/")[0]));
    try {
      if (route === "dashboard") view.innerHTML = await viewDashboard();
      else if (route === "empresas") view.innerHTML = await viewCompanies();
      else if (route === "empresa-nova") view.innerHTML = viewCompanyForm();
      else if (route === "usuarios") view.innerHTML = await viewUsers();
      else if (route === "estoque") view.innerHTML = await viewInventory();
      else if (route === "nfe") view.innerHTML = await viewNfe();
      else if (route === "produtos") view.innerHTML = await viewProducts();
      else if (route === "anuncios") view.innerHTML = await viewListings();
      else if (route === "vendas") view.innerHTML = await viewSales();
      else if (route.startsWith("venda/")) view.innerHTML = await viewSale(route.split("/")[1]);
      else if (route === "vendedores") view.innerHTML = await viewVendors();
      else if (route === "vendedor-novo") view.innerHTML = await viewVendorForm();
      else if (route === "marketplaces") view.innerHTML = await viewMarketplaces();
      else if (route === "meus-marketplaces") view.innerHTML = await viewMyMarketplaces();
      else if (route === "perfil" || route === "config") view.innerHTML = await viewProfile();
      else view.innerHTML = page("Não encontrado", "", "<p>Tela inexistente.</p>");
      bindView();
    } catch (ex) {
      view.innerHTML = `<div class="kt-alert kt-alert-danger">${ex.message}</div>`;
    }
  }

  async function viewDashboard() {
    const d = await api("/dashboard");
    const cards = (d.salesByStatus || []).map(s => `<div class="vilmo-card"><div class="text-sm text-muted-foreground">${s.status}</div><div class="text-2xl font-semibold">${s.count}</div></div>`).join("");
    return page("Dashboard", "", `<div class="vilmo-grid cols-3 mb-4">
      <div class="vilmo-card"><div class="text-sm text-muted-foreground">Empresas</div><div class="text-2xl font-semibold">${d.companies}</div></div>
      <div class="vilmo-card"><div class="text-sm text-muted-foreground">Usuários</div><div class="text-2xl font-semibold">${d.users}</div></div>
      <div class="vilmo-card"><div class="text-sm text-muted-foreground">Estoque baixo</div><div class="text-2xl font-semibold">${d.lowStock}</div></div>
    </div><h2 class="font-medium mb-3">Vendas por status</h2><div class="vilmo-grid cols-3">${cards || "<p>Sem vendas ainda.</p>"}</div>`);
  }

  async function viewCompanies() {
    const list = await api("/companies");
    const rows = list.map(c => `<tr>
      <td><a href="#/marketplaces">${c.tradeName || c.legalName}</a><div class="text-xs text-muted-foreground">${c.cnpjFormatted}</div></td>
      <td>${c.status}</td>
      <td><span class="light ${c.readyToList ? "ok" : "off"}"></span>Listar
          <span class="light ${c.readyToSyncSales ? "ok" : "off"}"></span>Sync
          <span class="light ${c.readyToInvoice ? "ok" : "off"}"></span>NF-e</td>
    </tr>`);
    return page("Empresas", `<a class="kt-btn kt-btn-primary" href="#/empresa-nova">Nova empresa</a>`, table(["Empresa", "Status", "Prontidão"], rows));
  }

  function viewCompanyForm() {
    return page("Nova empresa", `<a class="kt-btn kt-btn-outline" href="#/empresas">Voltar</a>`, `
      <form id="company-form" class="vilmo-card vilmo-grid cols-2">
        <label>Razão social<input class="kt-input" name="legalName" required></label>
        <label>Nome fantasia<input class="kt-input" name="tradeName"></label>
        <label>CNPJ<input class="kt-input" name="cnpj" required></label>
        <label>IE<input class="kt-input" name="ie"></label>
        <label>E-mail<input class="kt-input" name="email"></label>
        <label>Telefone<input class="kt-input" name="phone"></label>
        <label>Logradouro<input class="kt-input" name="street" required></label>
        <label>Número<input class="kt-input" name="number" required></label>
        <label>Complemento<input class="kt-input" name="complement"></label>
        <label>Bairro<input class="kt-input" name="neighborhood" required></label>
        <label>Cidade<input class="kt-input" name="city" required></label>
        <label>UF<input class="kt-input" name="uf" maxlength="2" required></label>
        <label>CEP (8 dígitos)<input class="kt-input" name="cep" required></label>
        <label>Regime<select class="kt-select" name="taxRegime"><option>Simples</option><option>LucroPresumido</option><option>LucroReal</option></select></label>
        <div class="col-span-2"><button class="kt-btn kt-btn-primary" type="submit">Salvar rascunho</button></div>
      </form>`);
  }

  async function viewUsers() {
    const list = await api("/users");
    const rows = list.map(u => `<tr><td>${u.name}</td><td>${u.email}</td><td>${u.profile}</td><td>${u.status}</td></tr>`);
    return page("Usuários", "", table(["Nome", "E-mail", "Perfil", "Status"], rows) + `
      <form id="user-form" class="vilmo-card vilmo-grid cols-2 mt-4">
        <h3 class="col-span-2 font-medium">Novo usuário empresa</h3>
        <label>E-mail<input class="kt-input" name="email" required></label>
        <label>Nome<input class="kt-input" name="name" required></label>
        <label>Senha<input class="kt-input" name="password" type="password"></label>
        <label>Canais (vírgula)<input class="kt-input" name="codes" value="MercadoLivre"></label>
        <div class="col-span-2"><button class="kt-btn kt-btn-primary" type="submit">Criar</button></div>
      </form>`);
  }

  async function viewInventory() {
    const list = await api("/inventory");
    const rows = list.map(p => `<tr>
      <td>${p.sku}</td><td>${p.name}</td><td>${p.onHand}</td>
      <td><input class="kt-input kt-input-sm price" data-sku="${p.sku}" value="${p.salePrice}" style="max-width:120px"></td>
      <td><button class="kt-btn kt-btn-sm kt-btn-outline save-price" data-sku="${p.sku}">Salvar preço</button></td>
    </tr>`);
    return page("Estoque", `<a class="kt-btn kt-btn-primary" href="#/nfe">Ingerir NF-e</a>`, table(["SKU", "Produto", "Saldo", "Preço de venda", ""], rows));
  }

  async function viewNfe() {
    const companies = store.me.level === "Admin" ? await api("/companies") : store.me.memberships;
    const current = companies.find(c => (c.id || c.companyId) === store.companyId) || companies[0];
    const cnpj = current.cnpj || current.Cnpj || "";
    const locked = store.me.level !== "Admin";
    return page("Ingerir NF-e", "", `
      <div class="vilmo-grid cols-2">
        <form id="nfe-form" class="vilmo-card flex flex-col gap-3">
          <label>Empresa / CNPJ
            ${locked ? `<input class="kt-input" name="cnpj" value="${cnpj}" readonly>` :
              `<select class="kt-select" name="cnpj">${companies.map(c => `<option value="${c.cnpj}" ${ (c.id||c.companyId)===store.companyId?"selected":""}>${c.tradeName||c.legalName||""} · ${c.cnpjFormatted||c.cnpj}</option>`).join("")}</select>`}
          </label>
          <label>Chave de acesso (44 dígitos)<input class="kt-input" name="chave" id="chave" maxlength="44" required></label>
          <div class="flex gap-2 flex-wrap">
            <button class="kt-btn kt-btn-primary" type="submit">Ingerir chave</button>
            <button class="kt-btn kt-btn-outline" type="button" id="scan-btn">Ler código (câmera)</button>
            <label class="kt-btn kt-btn-outline" id="scan-photo-label">Foto / arquivo
              <input type="file" id="scan-photo" accept="image/*" capture="environment" hidden>
            </label>
          </div>
          <div id="nfe-msg"></div>
        </form>
        <form id="xml-form" class="vilmo-card flex flex-col gap-3">
          <label>XML da NF-e (fallback)<input class="kt-input" type="file" name="file" accept=".xml"></label>
          <button class="kt-btn kt-btn-outline" type="submit">Enviar XML</button>
          <div id="xml-msg"></div>
        </form>
      </div>
      <div class="vilmo-card ingest-log-card">
        <div class="flex items-center justify-between gap-3 mb-3">
          <h2 class="font-medium">Andamento da ingestão</h2>
          <button class="kt-btn kt-btn-sm kt-btn-outline" type="button" id="refresh-ingest-logs">Atualizar</button>
        </div>
        <p class="text-sm text-muted-foreground mb-3">O último passo executado aparece primeiro. Detalhes técnicos ficam ocultos em cada linha.</p>
        <div id="ingest-log-progress" class="ingest-log-progress"></div>
        <div id="ingest-log-table"></div>
      </div>
      <div id="camera-overlay" class="camera-overlay hidden">
        <div class="camera-panel">
          <p id="scan-hint">Aponte para o código de barras ou QR da DANFE</p>
          <div class="camera-view">
            <video id="cam" autoplay playsinline muted></video>
            <div class="viewfinder" aria-hidden="true"></div>
          </div>
          <div class="flex gap-2 flex-wrap">
            <button class="kt-btn kt-btn-outline" type="button" id="flip-cam">Trocar câmera</button>
            <button class="kt-btn kt-btn-primary" type="button" id="close-cam">Cancelar</button>
          </div>
        </div>
      </div>`);
  }

  async function viewProducts() {
    const list = await api("/products");
    const rows = list.map(p => `<tr><td>${p.sku}</td><td>${p.name}</td><td>${p.ean || ""}</td><td>${p.salePrice}</td>
      <td><button class="kt-btn kt-btn-sm kt-btn-primary pub" data-sku="${esc(p.sku)}" data-name="${esc(p.name)}">Publicar</button></td></tr>`);
    return page("Produtos", "", table(["SKU", "Nome", "EAN", "Preço", ""], rows) + `
      <form id="product-form" class="vilmo-card vilmo-grid cols-2 mt-4">
        <h3 class="col-span-2 font-medium">Novo / atualizar produto</h3>
        <label>SKU<input class="kt-input" name="sku" required></label>
        <label>Nome<input class="kt-input" name="name" required></label>
        <label>EAN<input class="kt-input" name="ean"></label>
        <label>NCM<input class="kt-input" name="ncm"></label>
        <label>Preço<input class="kt-input" name="salePrice" type="number" step="0.01"></label>
        <div class="col-span-2"><button class="kt-btn kt-btn-primary" type="submit">Salvar</button></div>
      </form>`);
  }

  async function viewListings() {
    const [ads, products, fields, markets] = await Promise.all([
      api("/advertisements"),
      api("/products"),
      api("/marketplaces/listing-fields"),
      api("/marketplaces")
    ]);
    let vendors = [];
    if (store.me.level !== "Vendor") {
      try { vendors = await api(`/companies/${store.companyId}/vendors`); } catch { vendors = []; }
    }
    const productOpts = (products || []).map(p => `<option value="${esc(p.sku)}">${esc(p.sku)} · ${esc(p.name)}</option>`).join("");
    const extraByMkt = fields.byMarketplace || {};
    const extraField = (m, d) => {
      const key = d.fieldKey || d.FieldKey;
      if (m.code === "MercadoLivre" && key === "listingTypeId") {
        const mark = (d.required || d.Required) ? " *" : "";
        return `<label class="col-span-2">${esc(d.label)}${mark}
          <select class="kt-select attr-field ml-listing-type" data-mkt="${esc(m.code)}" data-key="${esc(key)}">${mlListingTypeOptions(mlListingFallback, "gold_special")}</select>
          <span class="text-xs text-muted-foreground">O Mercado Livre recebe o código da opção (gold_special), não um nome livre.</span>
        </label>`;
      }
      if (m.code === "MercadoLivre" && key === "categoryId") {
        return `<div class="ml-cat col-span-2">
          <div class="ml-cat-head">
            <span>${esc(d.label)}</span>
            <button type="button" class="kt-btn kt-btn-outline kt-btn-sm ml-cat-predict">Sugerir pela descrição/título</button>
          </div>
          <div class="ml-cat-levels vilmo-grid cols-2"></div>
          <input type="hidden" class="attr-field ml-cat-value" data-mkt="${esc(m.code)}" data-key="${esc(key)}" value="">
          <div class="ml-cat-attrs vilmo-grid cols-2"></div>
          <p class="ml-cat-hint text-xs text-muted-foreground">O Mercado Livre recebe o código da opção (MLB…), não o nome. Ao mudar a categoria, todos os atributos dela aparecem abaixo — os com * são obrigatórios para publicar.</p>
        </div>`;
      }
      return `<label>${esc(d.label)}<input class="kt-input attr-field" data-mkt="${esc(m.code)}" data-key="${esc(key)}"></label>`;
    };
    const extras = (markets || []).map(m => {
      const defs = extraByMkt[m.code] || extraByMkt[m.Code] || [];
      if (!defs.length) return "";
      const inputs = defs.map(d => extraField(m, d)).join("");
      return `<div class="ad-extra" data-extra="${esc(m.code)}"><h4 class="text-sm font-medium mb-2">${esc(m.displayName || m.code)}</h4><div class="vilmo-grid cols-2">${inputs}</div></div>`;
    }).join("");
    const checks = (markets || []).map(m => `<label class="text-sm"><input type="checkbox" class="mkt-code" value="${esc(m.code)}" checked> ${esc(m.displayName || m.code)}</label>`).join("");
    const vendorSel = store.me.level === "Vendor" ? "" : `<label>Vendedor (opcional)
      <select class="kt-select" name="vendorUserId"><option value="">Eu / empresa</option>${(vendors || []).map(v => `<option value="${v.id || v.userId || ""}">${esc(v.name || v.email || "")}</option>`).join("")}</select></label>`;
    const list = Array.isArray(ads) ? ads : (ads && ads.advertisements) || [];
    window.__vilmoAds = Object.fromEntries((list || []).map(a => [String(a.id), a]));
    const cards = list.map(a => {
      const items = (a.items || []).map(i => `${i.quantity}× ${i.sku}`).join(", ");
      const channels = (a.channels || []).map(c => {
        const remoteBits = [];
        if (c.remoteId) remoteBits.push(`id ${esc(c.remoteId)}`);
        if (c.remoteStatus) remoteBits.push(esc(c.remoteStatus));
        if (c.remotePrice != null) remoteBits.push(`R$ ${esc(String(c.remotePrice))}`);
        if (c.remoteQuantity != null) remoteBits.push(`qtd ${esc(String(c.remoteQuantity))}`);
        const title = c.remoteTitle ? `<div class="text-sm">${esc(c.remoteTitle)}</div>` : "";
        const link = c.remotePermalink
          ? `<a class="text-xs" href="${esc(c.remotePermalink)}" target="_blank" rel="noopener">Abrir no marketplace</a>`
          : "";
        const synced = c.lastSyncedAt
          ? `Atualizado ${esc(fmtWhen(c.lastSyncedAt))}`
          : "Sem dados online ainda";
        const steps = c.publishLog || [];
        return `<div class="ad-channel" data-ad="${esc(a.id)}" data-code="${esc(c.marketplaceCode)}">
          <div class="ad-channel-head">
            <div>
              <div class="font-medium">${esc(c.displayName || c.marketplaceCode)}</div>
              <div class="text-xs text-muted-foreground">${esc(c.statusPt || c.status)}</div>
            </div>
            <div class="ad-channel-actions">
              <button type="button" class="kt-btn kt-btn-outline kt-btn-sm ad-cancel">Cancelar</button>
              <button type="button" class="kt-btn kt-btn-primary kt-btn-sm ad-publish-ch">Publicar neste canal</button>
            </div>
          </div>
          ${title}
          <div class="text-xs text-muted-foreground">${remoteBits.join(" · ") || synced}</div>
          <div class="text-xs text-muted-foreground">${esc(synced)}</div>
          ${link}
          ${stepAccordionHtml("Passos da publicação", steps, { dataAttr: `data-ad="${esc(a.id)}" data-code="${esc(c.marketplaceCode)}"` })}
        </div>`;
      }).join("") || `<p class="text-sm text-muted-foreground">Nenhum marketplace neste anúncio.</p>`;
      return `<article class="vilmo-card ad-card mb-3" data-ad="${esc(a.id)}">
        <div class="ad-card-head">
          <div>
            <h3 class="font-medium">${esc(a.title)}</h3>
            <div class="text-xs text-muted-foreground">${esc(a.sku)} · ${a.kind === "Kit" ? "conjunto" : "produto"} · ${esc(items)} · R$ ${esc(String(a.price))} · qtd ${esc(String(a.availableQuantity))}</div>
          </div>
          <div class="ad-card-head-actions">
            <label class="ad-switch" title="Mostrar ou ocultar canais">
              <input type="checkbox" class="ad-card-open" role="switch" aria-label="Mostrar detalhes do anúncio">
              <span class="ad-switch-label">Detalhes</span>
            </label>
            <button type="button" class="kt-btn kt-btn-outline kt-btn-sm ad-reuse">Aproveitar anúncio</button>
            <button type="button" class="kt-btn kt-btn-outline kt-btn-sm ad-refresh">Atualizar dados online</button>
          </div>
        </div>
        <div class="ad-card-body">
          <div class="ad-channels">${channels}</div>
          <div class="ad-card-msg"></div>
        </div>
      </article>`;
    }).join("") || `<p class="text-sm text-muted-foreground">Nenhum anúncio ainda.</p>`;
    return page("Anúncios", "", `
      <form id="ad-form" class="vilmo-card vilmo-grid cols-2 mb-4">
        <h3 class="col-span-2 font-medium">Salvar anúncio</h3>
        <p class="col-span-2 text-sm text-muted-foreground">Escolha os marketplaces e salve o rascunho. Depois use <b>Publicar neste canal</b> ou <b>Cancelar</b> em cada marketplace, e <b>Atualizar dados online</b> para puxar o anúncio publicado.</p>
        <div class="col-span-2 ad-kind">
          <label class="text-sm"><input type="radio" name="kind" value="Product" checked> Um produto</label>
          <label class="text-sm"><input type="radio" name="kind" value="Kit"> Conjunto / kit</label>
        </div>
        <div class="col-span-2">
          <div class="font-medium text-sm mb-2">Marketplaces</div>
          <div class="flex flex-wrap gap-3">${checks}</div>
          ${extras}
        </div>
        ${vendorSel}
        <label>Título<input class="kt-input" name="title" required maxlength="180"></label>
        <label>Nome da família (Mercado Livre, obrigatório)<input class="kt-input" name="familyName" required maxlength="60" placeholder="Ex.: Calça jeans feminina"></label>
        <label>SKU do anúncio (vazio = SKU do produto ou KIT-…)<input class="kt-input" name="sku"></label>
        <label class="col-span-2">Descrição<textarea class="kt-input" name="description" rows="3"></textarea></label>
        <label>Preço (BRL)<input class="kt-input" name="price" type="number" step="0.01" min="0" required></label>
        <label>Quantidade do anúncio<input class="kt-input" name="availableQuantity" type="number" step="1" min="1" value="1" required></label>
        <label>Condição<select class="kt-select" name="condition"><option value="new">Novo</option><option value="used">Usado</option></select></label>
        <label>Marca<input class="kt-input" name="brand"></label>
        <label>EAN / GTIN<input class="kt-input" name="gtin"></label>
        <label>Peso (g)<input class="kt-input" name="weightGrams" type="number" step="1" min="0"></label>
        <label>Altura (cm)<input class="kt-input" name="heightCm" type="number" step="0.1" min="0"></label>
        <label>Largura (cm)<input class="kt-input" name="widthCm" type="number" step="0.1" min="0"></label>
        <label>Comprimento (cm)<input class="kt-input" name="lengthCm" type="number" step="0.1" min="0"></label>
        <div class="col-span-2">
          <div class="font-medium text-sm mb-2">Itens do anúncio (estoque)</div>
          <div id="ad-items" class="ad-items"></div>
          <template id="ad-item-options">${productOpts}</template>
          <button class="kt-btn kt-btn-outline kt-btn-sm" type="button" id="ad-add-item">Adicionar item</button>
        </div>
        <div class="col-span-2"><button class="kt-btn kt-btn-primary" type="submit">Salvar anúncio</button></div>
        <div id="ad-msg" class="col-span-2"></div>
      </form>
      ${cards}`);
  }

  async function viewSales() {
    const list = await api("/sales");
    const rows = list.map(s => `<tr>
      <td><a href="#/venda/${s.id}">${s.remoteOrderId}</a></td>
      <td>${s.marketplaceCode}</td>
      <td>${s.statusPt}</td>
      <td>${s.total}</td>
      <td>${s.recipientName}</td>
    </tr>`);
    return page(store.me.level === "Vendor" ? "Minhas vendas" : "Vendas", "", table(["Pedido", "Canal", "Status", "Total", "Destinatário"], rows));
  }

  async function viewSale(id) {
    const s = await api(`/sales/${id}`);
    const emit = (s.status === "Paid" || s.status === "InvoiceRejected")
      ? `<button class="kt-btn kt-btn-primary" id="emit-nfe">Emitir nota fiscal eletrônica</button>` : "";
    const label = (s.status === "PreparingForDispatch" || s.status === "LabelPrinted")
      ? `<a class="kt-btn kt-btn-primary" href="${API}/sales/${id}/label.pdf" target="_blank" id="print-label">Imprimir etiqueta para envio</a>` : "";
    return page(`Venda ${s.remoteOrderId}`, `${emit} ${label}`, `
      <div class="vilmo-grid cols-2">
        <div class="vilmo-card">
          <div><b>Status:</b> ${s.statusPt}</div>
          <div><b>Canal:</b> ${s.marketplaceCode}</div>
          <div><b>Total:</b> ${s.total}</div>
          <div><b>Destinatário:</b> ${s.recipientName}</div>
          <div>${s.address.street}, ${s.address.number} — ${s.address.city}/${s.address.uf} CEP ${s.address.cep}</div>
        </div>
        <div class="vilmo-card">
          <h3 class="font-medium mb-2">Itens</h3>
          ${(s.items||[]).map(i => `<div>${i.qty || i.quantity} × ${i.sku} ${i.name} — ${i.unitPrice}</div>`).join("")}
          <h3 class="font-medium mt-4 mb-2">Dados do marketplace</h3>
          ${(s.attributes||[]).map(a => `<div class="text-sm">${a.fieldName}: ${a.fieldValue}</div>`).join("") || "<div class='text-sm'>—</div>"}
        </div>
      </div>
      <p class="text-xs text-muted-foreground mt-3">Etiqueta 10×15 cm. Colar no maior lado, sem cobrir o código de barras, sem dobrar sobre arestas.</p>`);
  }

  async function viewVendors() {
    const list = await api(`/companies/${store.companyId}/vendors`);
    const rows = list.map(v => `<tr><td>${v.name}</td><td>${v.email}</td><td>${v.status}</td></tr>`);
    return page("Vendedores", `<a class="kt-btn kt-btn-primary" href="#/vendedor-novo">Novo vendedor</a>`, table(["Nome", "E-mail", "Status"], rows));
  }

  async function viewVendorForm() {
    const companies = store.me.level === "Admin" ? await api("/companies") : [];
    const select = store.me.level === "Admin"
      ? `<label>Empresa<select class="kt-select" name="companyId">${companies.map(c => `<option value="${c.id}">${c.tradeName || c.legalName} · ${c.cnpjFormatted}</option>`).join("")}</select></label>`
      : `<label>Empresa<input class="kt-input" value="CNPJ da sua empresa" readonly></label>`;
    return page("Novo vendedor", `<a class="kt-btn kt-btn-outline" href="#/vendedores">Voltar</a>`, `
      <form id="vendor-form" class="vilmo-card vilmo-grid cols-2">
        ${select}
        <label>E-mail<input class="kt-input" name="email" required></label>
        <label>Nome<input class="kt-input" name="name" required></label>
        <label>Telefone<input class="kt-input" name="phone"></label>
        <label>Senha<input class="kt-input" name="password" type="password"></label>
        <label>Canais (vírgula ou *)<input class="kt-input" name="codes" value="*"></label>
        <div class="col-span-2"><button class="kt-btn kt-btn-primary" type="submit">Criar vendedor</button></div>
      </form>`);
  }

  async function viewMarketplaces() {
    const list = await api(`/companies/${store.companyId}/marketplaces`);
    const q = new URLSearchParams((location.hash.split("?")[1] || ""));
    const oauth = q.get("oauth");
    const oauthErr = q.get("error");
    const oauthBanner = oauth === "ok"
      ? `<div class="vilmo-card mb-4 text-sm">Marketplace conectado. AccessToken gravado.</div>`
      : (oauth === "error" || oauth === "denied"
        ? `<div class="vilmo-card mb-4 text-sm">Falha ao conectar${oauthErr ? ": " + oauthErr : ""}.</div>`
        : "");
    const connected = q.get("connected");
    const cards = list.map(m => `
      <form class="vilmo-card mkt-form" data-code="${esc(m.code)}">
        <div class="flex justify-between items-center mb-3">
          <h3 class="font-medium">${esc(m.displayName)}</h3>
          <label class="text-sm"><input type="checkbox" name="isEnabled" ${m.isEnabled ? "checked" : ""}> Ativo</label>
        </div>
        <div class="text-xs mb-2">Status: ${esc(m.linkStatus)}</div>
        ${(m.fields||[]).map(f => f.filledByOauth ? `<div class="text-sm text-muted-foreground">${esc(f.label)}: ${esc(f.value || "—")}</div>` :
          `<label class="block mb-2 text-sm">${esc(f.label)}<input class="kt-input" name="${esc(f.parameterKey)}" value="${esc(f.value || "")}" ${f.isSecret ? 'type="password"' : ""}></label>`).join("")}
        <div class="flex gap-2 mt-3">
          <button class="kt-btn kt-btn-primary" type="submit">Salvar</button>
          <button class="kt-btn kt-btn-outline connect" type="button" data-code="${esc(m.code)}">Conectar</button>
        </div>
        ${stepAccordionHtml("Passos da conexão", m.connectLog || [], {
          open: !!(connected && connected === m.code && (oauth === "ok" || oauth === "error" || oauth === "denied")),
          dataAttr: `data-code="${esc(m.code)}"`
        })}
      </form>`).join("");
    return page("Marketplaces da empresa", "", `${oauthBanner}<div class="vilmo-grid cols-2">${cards}</div>`);
  }

  async function viewMyMarketplaces() {
    const d = await api(`/companies/${store.companyId}/vendors/${store.me.id}`);
    const rows = (d.marketplaces || []).map(m => `<tr><td>${m.marketplaceCode}</td><td>${m.linkStatus}</td></tr>`);
    return page("Meus marketplaces", "", table(["Canal", "Status"], rows));
  }

  async function viewProfile() {
    return page("Configurações", "", `<div class="vilmo-card">
      <p><b>${store.me.name}</b> · ${store.me.email}</p>
      <p class="text-sm mt-2">Nível: ${store.me.level}</p>
      <p class="text-sm">A1 e série NF-e ficam na empresa. Envie o certificado PFX abaixo.</p>
      <form id="cert-form" class="mt-4 flex flex-col gap-2 max-w-md">
        <input class="kt-input" type="file" name="file" accept=".pfx,.p12">
        <input class="kt-input" type="password" name="password" placeholder="Senha do A1">
        <button class="kt-btn kt-btn-primary" type="submit">Enviar A1</button>
      </form>
    </div>`);
  }

  function bindView() {
    const $ = (sel) => document.querySelector(sel);
    if ($("#company-form")) $("#company-form").onsubmit = async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const body = Object.fromEntries(fd.entries());
      const created = await api("/companies", { method: "POST", body });
      store.companyId = created.id;
      location.hash = "#/empresas";
    };
    if ($("#user-form")) $("#user-form").onsubmit = async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      await api(`/companies/${store.companyId}/users`, { method: "POST", body: {
        email: fd.get("email"), name: fd.get("name"), password: fd.get("password"),
        marketplaceCodes: fd.get("codes").split(",").map(s => s.trim()).filter(Boolean)
      }});
      renderRoute();
    };
    document.querySelectorAll(".save-price").forEach(btn => btn.onclick = async () => {
      const sku = btn.dataset.sku;
      const price = document.querySelector(`.price[data-sku="${sku}"]`).value;
      await api(`/products/${encodeURIComponent(sku)}/sale-price`, { method: "PUT", body: { salePrice: Number(price) } });
      renderRoute();
    });
    if ($("#nfe-form")) $("#nfe-form").onsubmit = async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const chave = fd.get("chave");
      const cnpj = fd.get("cnpj");
      const msg = $("#nfe-msg");
      try {
        const r = await api(`/nfe/chaves/${chave}/ingest`, { method: "POST", body: { cnpj } });
        const status = r.status || (r.replayed ? "replayed" : "queued");
        msg.innerHTML = `<div class="kt-alert ${r.error ? "kt-alert-danger" : "kt-alert-success"}">${r.message || ("Status: " + status)} · ${r.chave || chave}</div>`;
        startIngestLogPoll(r.runId);
      } catch (ex) { msg.innerHTML = `<div class="kt-alert kt-alert-danger">${ex.message}</div>`; }
    };
    if ($("#xml-form")) $("#xml-form").onsubmit = async (e) => {
      e.preventDefault();
      const file = e.target.file.files[0];
      if (!file) return;
      const fd = new FormData();
      fd.append("file", file);
      const xmlMsg = $("#xml-msg");
      try {
        const r = await api("/nfe/xml", { method: "POST", body: fd, headers: {} });
        if (xmlMsg) xmlMsg.innerHTML = `<div class="kt-alert ${r.error ? "kt-alert-danger" : "kt-alert-success"}">${r.message || ("XML ingerido · " + (r.status || ""))}</div>`;
        startIngestLogPoll(r.runId);
      } catch (ex) {
        if (xmlMsg) xmlMsg.innerHTML = `<div class="kt-alert kt-alert-danger">${ex.message}</div>`;
      }
    };
    if ($("#refresh-ingest-logs")) $("#refresh-ingest-logs").onclick = () => loadIngestLogs();
    if ($("#ingest-log-table")) loadIngestLogs();
    if ($("#scan-btn")) $("#scan-btn").onclick = () => startCamera();
    if ($("#close-cam")) $("#close-cam").onclick = stopCamera;
    if ($("#flip-cam")) $("#flip-cam").onclick = () => flipCamera();
    if ($("#scan-photo")) $("#scan-photo").onchange = async (e) => {
      const file = e.target.files && e.target.files[0];
      e.target.value = "";
      if (!file || !window.VilmoNfeScan) return;
      const chave = await VilmoNfeScan.fromFile(file);
      const msg = document.getElementById("nfe-msg");
      if (chave) {
        document.getElementById("chave").value = chave;
        if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-success">Chave lida da imagem. Confira e toque em Ingerir chave.</div>`;
      } else if (msg) {
        msg.innerHTML = `<div class="kt-alert kt-alert-danger">Código não é chave NF-e. Use o código de 44 dígitos ou o QR da DANFE.</div>`;
      }
    };
    if ($("#product-form")) $("#product-form").onsubmit = async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      await api("/products", { method: "POST", body: { sku: fd.get("sku"), name: fd.get("name"), ean: fd.get("ean"), ncm: fd.get("ncm"), salePrice: Number(fd.get("salePrice") || 0) } });
      renderRoute();
    };
    document.querySelectorAll(".pub").forEach(btn => btn.onclick = async () => {
      const name = btn.dataset.name || btn.dataset.sku;
      await api("/advertisements", { method: "POST", body: {
        sku: btn.dataset.sku,
        kind: "Product",
        title: name,
        familyName: String(name || "").slice(0, 60),
        items: [{ sku: btn.dataset.sku, quantity: 1 }]
      } });
      location.hash = "#/anuncios";
    });
    if ($("#ad-form")) {
      const opts = ($("#ad-item-options") && $("#ad-item-options").innerHTML) || "";
      const itemsBox = $("#ad-items");
      const addRow = (sku, qty) => {
        const row = document.createElement("div");
        row.className = "ad-item-row";
        row.innerHTML = `<label>Produto<select class="kt-select item-sku">${opts}</select></label>
          <label class="qty">Qtd no conjunto<input class="kt-input item-qty" type="number" min="1" step="1" value="${qty || 1}"></label>
          <button class="kt-btn kt-btn-outline kt-btn-sm item-del" type="button">Remover</button>`;
        if (sku) row.querySelector(".item-sku").value = sku;
        row.querySelector(".item-del").onclick = () => {
          if (itemsBox.querySelectorAll(".ad-item-row").length > 1) row.remove();
        };
        itemsBox.appendChild(row);
      };
      addRow();
      if ($("#ad-add-item")) $("#ad-add-item").onclick = () => addRow();
      const syncKind = () => {
        const kit = ($("#ad-form").kind.value === "Kit");
        $("#ad-add-item").classList.toggle("hidden", !kit);
        const rows = itemsBox.querySelectorAll(".ad-item-row");
        rows.forEach((row, i) => {
          row.querySelector(".item-del").classList.toggle("hidden", !kit);
          row.querySelector(".qty").classList.toggle("hidden", !kit);
          if (!kit) row.querySelector(".item-qty").value = 1;
          if (!kit && i > 0) row.remove();
        });
      };
      $("#ad-form").querySelectorAll("input[name=kind]").forEach(r => r.onchange = syncKind);
      syncKind();
      const mlCat = {
        roots: null,
        catCode(c) {
          const raw = typeof c === "string" || typeof c === "number"
            ? c
            : (c && (c.id || c.Id || c.categoryId || c.category_id)) || "";
          const m = String(raw).match(/MLB\d+/i);
          return m ? m[0].toUpperCase() : "";
        },
        catName(c) {
          if (!c || typeof c === "string" || typeof c === "number") return "";
          return c.name || c.Name || c.categoryName || c.category_name || "";
        },
        selectedId(sel) {
          const opt = sel && sel.selectedIndex >= 0 ? sel.options[sel.selectedIndex] : null;
          if (!opt) return "";
          const attr = opt.getAttribute("data-ml-id") || opt.getAttribute("value") || "";
          const fromText = String(opt.textContent || "").match(/MLB\d+/i);
          return this.catCode(attr || (fromText ? fromText[0] : ""));
        },
        optionFrom(o) {
          const id = this.catCode(o.getAttribute("data-ml-id") || o.getAttribute("value") || "");
          if (!id) return null;
          const name = (o.textContent || "").replace(/\s*\(MLB\d+\)\s*$/i, "").trim();
          return { id, name: name || id };
        },
        async rootsList() {
          if (this.roots) return this.roots;
          const r = await api("/marketplaces/MercadoLivre/categories");
          this.roots = r.items || r.Items || [];
          return this.roots;
        },
        wrap() { return document.querySelector(".ml-cat"); },
        valueInp() { return document.querySelector(".ml-cat-value"); },
        levels() { return document.querySelector(".ml-cat-levels"); },
        attrsBox() { return document.querySelector(".ml-cat-attrs"); },
        hint(text) {
          const el = document.querySelector(".ml-cat-hint");
          if (el) el.textContent = text || "O Mercado Livre recebe o código da opção (MLB…), não o nome.";
        },
        optionHtml(list, selected) {
          const sel = this.catCode(selected);
          const opts = [`<option value="">Selecionar…</option>`];
          (list || []).forEach(c => {
            const id = this.catCode(c);
            if (!id) return;
            const name = this.catName(c) || id;
            opts.push(`<option value="${esc(id)}" data-ml-id="${esc(id)}" ${id === sel ? "selected" : ""}>${esc(name)} (${esc(id)})</option>`);
          });
          if (sel && !(list || []).some(c => this.catCode(c) === sel))
            opts.push(`<option value="${esc(sel)}" data-ml-id="${esc(sel)}" selected>${esc(sel)}</option>`);
          return opts.join("");
        },
        selectHtml(list, selected, depth) {
          return `<label>Nível ${depth + 1}<select class="kt-select ml-cat-select" data-depth="${depth}">${this.optionHtml(list, selected)}</select></label>`;
        },
        syncValue() {
          const inp = this.valueInp();
          if (!inp) return "";
          let id = "";
          this.wrap()?.querySelectorAll(".ml-cat-select").forEach(sel => {
            const v = this.selectedId(sel);
            if (v) id = v;
          });
          inp.value = id;
          return id;
        },
        bumpNav() {
          this.navSeq = (this.navSeq || 0) + 1;
          return this.navSeq;
        },
        async setValue(id, meta, extraSaved) {
          const code = this.catCode(id);
          const inp = this.valueInp();
          if (inp) inp.value = code;
          if (!code) {
            this.hint(id ? adErr("InvalidCategoryId") : "");
            await this.loadAttrs("");
            return;
          }
          const name = this.catName(meta) || (meta && meta.name) || code;
          const extra = meta && meta.leaf === false
            ? " Há subcategorias — escolha uma mais específica para publicar."
            : "";
          this.hint(`${name} · ${code}${extra}`);
          await this.loadAttrs(code, extraSaved);
        },
        isNaName(name) {
          const n = String(name || "").trim().toUpperCase();
          return n === "N/A" || n === "NA" || n === "N.A." || n === "NÃO APLICÁVEL" || n === "NAO APLICAVEL" || n === "NÃO SE APLICA";
        },
        savedFromDom() {
          const map = {};
          this.attrsBox()?.querySelectorAll(".ml-attr-field").forEach(el => {
            const p = this.attrPayload(el);
            if (p) map[el.dataset.key] = JSON.stringify(p);
          });
          return map;
        },
        parseSaved(raw) {
          if (!raw) return null;
          try {
            const o = JSON.parse(raw);
            if (o && typeof o === "object")
              return { value_id: o.value_id || o.valueId || "", value_name: o.value_name || o.valueName || "" };
          } catch { /* plain */ }
          if (String(raw).includes("|")) {
            const [value_id, value_name] = String(raw).split("|", 2);
            return { value_id, value_name };
          }
          return { value_id: "", value_name: String(raw) };
        },
        isGridField(el) {
          const id = el && el.dataset ? el.dataset.mlId : "";
          const type = String(el && el.dataset ? el.dataset.valueType : "").toLowerCase();
          return type === "grid_id" || type === "grid_row_id"
            || id === "SIZE_GRID_ID" || id === "SIZE_GRID_ROW_ID";
        },
        attrPayload(el) {
          if (!el) return null;
          if (el.tagName === "SELECT") {
            const opt = el.selectedIndex >= 0 ? el.options[el.selectedIndex] : null;
            const fromOpt = opt && opt.value
              ? (opt.getAttribute("data-ml-value-name") || opt.value)
              : "";
            const manual = el.parentElement?.querySelector(".ml-grid-manual");
            const fromManual = manual ? String(manual.value || "").trim() : "";
            const value_name = fromOpt || fromManual;
            if (!value_name) return null;
            if (this.isGridField(el)) return { value_name };
            const value_id = (opt && opt.value)
              ? (opt.getAttribute("data-ml-value-id") || opt.value)
              : "";
            if (this.isNaName(value_name) || value_id === "-1") return null;
            const row = { value_name };
            if (value_id) row.value_id = value_id;
            return row;
          }
          const value_name = String(el.value || "").trim();
          if (!value_name || this.isNaName(value_name)) return null;
          if (this.isGridField(el)) return { value_name };
          const row = { value_name };
          const list = el.list;
          if (list) {
            const hit = [...list.options].find(o => (o.value || o.textContent) === value_name);
            const vid = hit && (hit.getAttribute("data-ml-value-id") || hit.value);
            if (vid && vid !== value_name) row.value_id = vid;
          }
          return row;
        },
        attrFieldHtml(a, savedMap, form) {
          const id = a.id || a.Id;
          const key = `ml:${id}`;
          const name = a.name || a.Name || id;
          const values = a.values || a.Values || [];
          const required = !!(a.required || a.Required || a.newRequired || a.NewRequired);
          const recommended = !!(a.recommended || a.Recommended);
          const max = a.valueMaxLength || a.ValueMaxLength;
          const type = String(a.valueType || a.ValueType || "string").toLowerCase();
          const gridFilter = !!(a.gridFilter || a.GridFilter);
          const saved = this.parseSaved(savedMap[key]);
          let seedName = saved && saved.value_name || "";
          let seedId = saved && saved.value_id || "";
          if (!seedName && id === "BRAND" && form && form.brand && form.brand.value)
            seedName = form.brand.value.trim();
          if (!seedName && id === "GTIN" && form && form.gtin && form.gtin.value)
            seedName = form.gtin.value.trim();
          const mark = required
            ? ` <span class="ml-attr-req">*</span>`
            : (recommended ? ` <span class="text-xs text-muted-foreground">recomendado</span>` : "");
          const reqAttr = required ? `data-required="1"` : "";
          const extra = `data-mkt="MercadoLivre" data-key="${esc(key)}" data-ml-id="${esc(id)}" data-value-type="${esc(type)}"${gridFilter ? " data-grid-filter=\"1\"" : ""} ${reqAttr}`;
          const isGrid = type === "grid_id" || type === "grid_row_id"
            || id === "SIZE_GRID_ID" || id === "SIZE_GRID_ROW_ID";
          if (isGrid) {
            const hint = id === "SIZE_GRID_ID"
              ? "Depois do gênero, listamos as guias do Mercado Livre. Não use um número inventado — a API recusa SIZE_GRID_ID inválido."
              : "Escolha a linha da guia (ex.: 26008:1). Isso preenche o tamanho automaticamente.";
            const sel = seedId || seedName
              ? `<option value="${esc(seedId || seedName)}" data-ml-value-id="${esc(seedId || seedName)}" data-ml-value-name="${esc(seedName || seedId)}" selected>${esc(seedName || seedId)}</option>`
              : `<option value="">Selecionar…</option>`;
            return `<label class="ml-grid-field">${esc(name)}${mark}
              <select class="kt-select ml-attr-field" ${extra}>${sel}</select>
              <input class="kt-input ml-grid-manual" type="text" value="${esc(seedName && !seedId ? seedName : "")}" placeholder="Ou informe o ID" autocomplete="off">
              <span class="text-xs text-muted-foreground">${hint}</span>
            </label>`;
          }
          const listed = [...values].sort((a, b) =>
            String(a.name || a.Name || "").localeCompare(String(b.name || b.Name || ""), "pt-BR", { sensitivity: "base" }));
          const useSelect = type === "list" || type === "boolean"
            || (id !== "BRAND" && listed.length >= 1 && listed.length <= 250);
          if (useSelect) {
            const opts = [`<option value="">Selecionar…</option>`].concat(listed.map(v => {
              const vid = v.id || v.Id || "";
              const vname = v.name || v.Name || vid;
              if (this.isNaName(vname) || vid === "-1") return "";
              const sel = (seedId && vid === seedId) || (!seedId && seedName && vname === seedName) ? " selected" : "";
              return `<option value="${esc(vid || vname)}" data-ml-value-id="${esc(vid)}" data-ml-value-name="${esc(vname)}"${sel}>${esc(vname)}</option>`;
            }).filter(Boolean));
            if (seedName && !listed.some(v => (v.id || v.Id) === seedId || (v.name || v.Name) === seedName))
              opts.push(`<option value="${esc(seedId || seedName)}" data-ml-value-id="${esc(seedId)}" data-ml-value-name="${esc(seedName)}" selected>${esc(seedName)}</option>`);
            return `<label>${esc(name)}${mark}<select class="kt-select ml-attr-field" ${extra}>${opts.join("")}</select></label>`;
          }
          const listId = `ml-attr-${esc(id)}`;
          const dl = values.length
            ? `<datalist id="${listId}">${values.map(v => {
                const vid = v.id || v.Id || "";
                const vname = v.name || v.Name || vid;
                if (this.isNaName(vname)) return "";
                return `<option value="${esc(vname)}" data-ml-value-id="${esc(vid)}"></option>`;
              }).join("")}</datalist>`
            : "";
          const inputType = type === "number" || type === "number_unit" ? "number" : "text";
          const maxAttr = max ? ` maxlength="${Number(max)}"` : "";
          const listAttr = values.length ? ` list="${listId}"` : "";
          return `<label>${esc(name)}${mark}<input class="kt-input ml-attr-field" ${extra} type="${inputType}" value="${esc(seedName)}"${maxAttr}${listAttr}>${dl}</label>`;
        },
        async loadAttrs(code, extraSaved) {
          const box = this.attrsBox();
          if (!box) return;
          this.attrSeq = (this.attrSeq || 0) + 1;
          const seq = this.attrSeq;
          if (!code) {
            box.innerHTML = "";
            return;
          }
          const prev = Object.assign({}, this.savedFromDom(), extraSaved || {});
          box.innerHTML = `<p class="text-xs text-muted-foreground">Carregando atributos da categoria…</p>`;
          try {
            const r = await api(`/marketplaces/MercadoLivre/categories/${encodeURIComponent(code)}/attributes`);
            if (seq !== this.attrSeq) return;
            const items = r.items || r.Items || [];
            if (!items.length) {
              box.innerHTML = `<p class="text-xs text-muted-foreground">Esta categoria não listou atributos.</p>`;
              return;
            }
            const form = document.getElementById("ad-form");
            const req = items.filter(a => a.required || a.Required || a.newRequired || a.NewRequired);
            const opt = items.filter(a => !(a.required || a.Required || a.newRequired || a.NewRequired));
            const reqHtml = req.map(a => this.attrFieldHtml(a, prev, form)).join("");
            const optHtml = opt.length
              ? `<p class="ml-attr-sec col-span-2">Outros atributos desta categoria</p>${opt.map(a => this.attrFieldHtml(a, prev, form)).join("")}`
              : "";
            box.innerHTML = reqHtml + optHtml;
            this.bindAttrWatchers();
            await this.refreshSizeCharts();
          } catch (ex) {
            if (seq !== this.attrSeq) return;
            box.innerHTML = `<p class="text-xs" style="color:#991b1b">${esc(adErr(ex.message))}</p>`;
          }
        },
        attrById(id) {
          return this.attrsBox()?.querySelector(`.ml-attr-field[data-ml-id="${CSS.escape(id)}"]`);
        },
        bindAttrWatchers() {
          this.attrsBox()?.querySelectorAll(".ml-attr-field").forEach(el => {
            const id = el.dataset.mlId;
            if (el.dataset.gridFilter === "1" || id === "GENDER" || id === "BRAND") {
              el.onchange = () => this.refreshSizeCharts();
              el.onblur = () => this.refreshSizeCharts();
            }
            if (id === "SIZE_GRID_ID")
              el.onchange = () => this.refreshSizeRows();
            if (id === "SIZE_GRID_ROW_ID")
              el.onchange = () => this.syncSizeFromRow();
          });
          this.attrsBox()?.querySelectorAll(".ml-grid-manual").forEach(inp => {
            const sel = inp.parentElement?.querySelector(".ml-attr-field");
            inp.onblur = () => {
              if (sel && sel.dataset.mlId === "SIZE_GRID_ID") this.refreshSizeRows();
            };
          });
        },
        genderBrand() {
          const g = this.attrById("GENDER");
          const b = this.attrById("BRAND");
          const gp = this.attrPayload(g) || {};
          const bp = this.attrPayload(b) || {};
          return {
            genderId: gp.value_id || "",
            genderName: gp.value_name || "",
            brand: bp.value_name || (document.querySelector("#ad-form")?.brand?.value || "").trim()
          };
        },
        fillSelect(el, items, selected, emptyLabel, keepUnknown) {
          if (!el) return;
          const keep = selected || el.value || "";
          const ids = new Set((items || []).map(it => String(it.id || it.Id || "")));
          const opts = [`<option value="">${esc(emptyLabel || "Selecionar…")}</option>`];
          (items || []).forEach(it => {
            const id = it.id || it.Id;
            const name = it.name || it.Name || it.size || it.Size || id;
            const valueName = it.valueName || it.ValueName || id;
            const sel = id === keep || name === keep ? " selected" : "";
            opts.push(`<option value="${esc(id)}" data-ml-value-id="${esc(id)}" data-ml-value-name="${esc(valueName)}" data-ml-size="${esc(it.size || it.Size || "")}"${sel}>${esc(name)}</option>`);
          });
          el.innerHTML = opts.join("");
          if (keepUnknown !== false && keep && !ids.has(keep) && ![...el.options].some(o => o.value === keep)) {
            el.insertAdjacentHTML("beforeend", `<option value="${esc(keep)}" data-ml-value-id="${esc(keep)}" data-ml-value-name="${esc(keep)}" selected>${esc(keep)}</option>`);
          } else if (keep && ids.has(keep)) {
            el.value = keep;
          }
          const manual = el.parentElement?.querySelector(".ml-grid-manual");
          if (manual && keepUnknown === false && manual.value && !ids.has(String(manual.value).trim()))
            manual.value = "";
        },
        async refreshSizeCharts() {
          const el = this.attrById("SIZE_GRID_ID");
          const cat = this.catCode(this.valueInp()?.value);
          if (!el || !cat) return;
          const { genderId, genderName, brand } = this.genderBrand();
          if (!genderId && !genderName) {
            this.fillSelect(el, [], el.value, "Selecione o gênero");
            return;
          }
          const q = new URLSearchParams();
          if (genderId) q.set("genderId", genderId);
          if (genderName) q.set("genderName", genderName);
          if (brand) q.set("brand", brand);
          try {
            const r = await api(`/marketplaces/MercadoLivre/categories/${encodeURIComponent(cat)}/size-charts?${q}`);
            const items = (r.items || r.Items || []).map(c => ({
              id: c.id || c.Id,
              name: `${c.name || c.Name || c.id} (${c.id || c.Id})`,
              valueName: c.id || c.Id
            }));
            const empty = items.length
              ? "Selecionar guia…"
              : (r.message || r.Message || "Nenhuma guia para este gênero/marca");
            this.fillSelect(el, items, items.some(x => x.id === el.value) ? el.value : "", empty, false);
            if (items.length && !el.value) {
              el.value = items[0].id;
            }
            await this.refreshSizeRows();
          } catch {
            this.fillSelect(el, [], el.value, "Informe o ID da guia");
          }
        },
        async refreshSizeRows() {
          const grid = this.attrById("SIZE_GRID_ID");
          const row = this.attrById("SIZE_GRID_ROW_ID");
          if (!row) return;
          const chartId = (this.attrPayload(grid) || {}).value_name || (grid ? grid.value : "") || "";
          if (!chartId) {
            this.fillSelect(row, [], "", "Selecione a guia");
            return;
          }
          try {
            const r = await api(`/marketplaces/MercadoLivre/size-charts/${encodeURIComponent(chartId)}`);
            const items = (r.rows || r.Rows || []).map(x => ({
              id: x.id || x.Id,
              name: `${x.size || x.Size || x.id} (${x.id || x.Id})`,
              size: x.size || x.Size || "",
              valueName: x.id || x.Id
            }));
            this.fillSelect(row, items, items.some(x => x.id === row.value) ? row.value : "", items.length ? "Selecionar tamanho…" : "Sem linhas nesta guia", false);
            if (items.length && !row.value) {
              const sizeEl = this.attrById("SIZE");
              const sizeName = sizeEl && (this.attrPayload(sizeEl) || {}).value_name;
              const hit = sizeName && items.find(x => x.size === sizeName);
              row.value = (hit && hit.id) || items[0].id;
            }
            this.syncSizeFromRow();
          } catch {
            this.fillSelect(row, [], "", "Guia inválida — escolha uma da lista", false);
          }
        },
        syncSizeFromRow() {
          const row = this.attrById("SIZE_GRID_ROW_ID");
          const sizeEl = this.attrById("SIZE");
          if (!row || !sizeEl) return;
          const opt = row.selectedIndex >= 0 ? row.options[row.selectedIndex] : null;
          const sizeName = opt && (opt.getAttribute("data-ml-size") || opt.getAttribute("data-ml-value-name") || "");
          if (!sizeName || sizeName === row.value) return;
          if (sizeEl.tagName === "SELECT") {
            const hit = [...sizeEl.options].find(o =>
              o.getAttribute("data-ml-value-name") === sizeName || o.textContent.trim() === sizeName || o.value === sizeName);
            if (hit) sizeEl.value = hit.value;
          } else {
            sizeEl.value = sizeName;
          }
        },
        fillSuggested(list) {
          (list || []).forEach(s => {
            const id = s.id || s.Id;
            if (!id) return;
            const el = document.querySelector(`.ml-attr-field[data-key="ml:${id}"]`);
            if (!el) return;
            const vid = s.valueId || s.ValueId || s.value_id || "";
            const vname = s.valueName || s.ValueName || s.value_name || "";
            if (this.isNaName(vname)) return;
            if (el.tagName === "SELECT") {
              const hit = [...el.options].find(o =>
                (vid && (o.getAttribute("data-ml-value-id") === vid || o.value === vid))
                || (vname && o.getAttribute("data-ml-value-name") === vname));
              if (hit) el.value = hit.value;
            } else if (!el.value) {
              el.value = vname || vid;
            }
          });
        },
        missingSizeGrid() {
          const grid = this.attrPayload(this.attrById("SIZE_GRID_ID"));
          const row = this.attrPayload(this.attrById("SIZE_GRID_ROW_ID"));
          if (!this.attrById("SIZE_GRID_ID")) return false;
          const gid = grid && grid.value_name || "";
          const rid = row && row.value_name || "";
          if (!gid || !rid) return true;
          if (gid === rid) return true;
          if (!rid.startsWith(gid)) return true;
          return false;
        },
        missingRequired() {
          return [...(this.attrsBox()?.querySelectorAll(".ml-attr-field[data-required='1']") || [])]
            .some(el => !this.attrPayload(el));
        },
        bindSelects() {
          this.levels()?.querySelectorAll(".ml-cat-select").forEach(sel => {
            sel.onchange = () => this.onChange(sel);
          });
        },
        async onChange(sel) {
          const depth = Number(sel.dataset.depth || 0);
          const id = this.selectedId(sel);
          const box = this.levels();
          if (!box) return;
          const snapshot = [...box.querySelectorAll(".ml-cat-select")].slice(0, depth + 1).map(s => ({
            selected: s === sel ? id : this.selectedId(s),
            options: [...s.options].map(o => this.optionFrom(o)).filter(Boolean)
          }));
          this.seq = (this.seq || 0) + 1;
          const seq = this.seq;
          const paint = (levels) => {
            box.innerHTML = levels.map((lv, i) => this.selectHtml(lv.options, lv.selected, i)).join("");
            this.bindSelects();
          };
          if (!id) {
            paint(snapshot);
            await this.setValue(depth > 0 ? snapshot[depth - 1].selected : "");
            return;
          }
          try {
            const detail = await api(`/marketplaces/MercadoLivre/categories/${encodeURIComponent(id)}`);
            if (seq !== this.seq) return;
            const levels = snapshot.slice();
            const children = detail.children || detail.Children || [];
            if (children.length)
              levels.push({ options: children, selected: "" });
            paint(levels);
            await this.setValue(detail.id || detail.Id || id, detail);
          } catch {
            if (seq !== this.seq) return;
            paint(snapshot);
            await this.setValue(id, { name: id, leaf: true });
          }
        },
        async showRoots(selected) {
          const box = this.levels();
          if (!box) return;
          const seq = this.bumpNav();
          const roots = await this.rootsList();
          if (seq !== this.navSeq) return;
          const code = this.catCode(selected);
          box.innerHTML = this.selectHtml(roots, code, 0);
          this.bindSelects();
          if (seq !== this.navSeq) return;
          if (!code) await this.setValue("");
          else await this.setValue(code);
        },
        async apply(id, extraSaved) {
          const box = this.levels();
          if (!box) return;
          const code = this.catCode(id);
          const seq = this.bumpNav();
          if (!code) {
            await this.showRoots();
            if (id) this.hint(adErr("InvalidCategoryId"));
            return;
          }
          try {
            const detail = await api(`/marketplaces/MercadoLivre/categories/${encodeURIComponent(code)}`);
            if (seq !== this.navSeq) return;
            const path = (detail.pathFromRoot && detail.pathFromRoot.length)
              ? detail.pathFromRoot
              : (detail.PathFromRoot && detail.PathFromRoot.length)
                ? detail.PathFromRoot
                : [{ id: detail.id || detail.Id || code, name: this.catName(detail) }];
            const roots = await this.rootsList();
            if (seq !== this.navSeq) return;
            let html = this.selectHtml(roots, this.catCode(path[0]), 0);
            for (let i = 0; i < path.length; i++) {
              const nodeId = this.catCode(path[i]);
              const node = i === path.length - 1 ? detail : await api(`/marketplaces/MercadoLivre/categories/${encodeURIComponent(nodeId)}`);
              if (seq !== this.navSeq) return;
              const kids = node.children || node.Children || [];
              const nextId = path[i + 1] ? this.catCode(path[i + 1]) : "";
              if (kids.length)
                html += this.selectHtml(kids, nextId, i + 1);
            }
            box.innerHTML = html;
            this.bindSelects();
            await this.setValue(detail.id || detail.Id || code, detail, extraSaved);
          } catch {
            if (seq !== this.navSeq) return;
            await this.showRoots(code);
          }
        },
        async ensure() {
          const on = document.querySelector(".mkt-code[value='MercadoLivre']")?.checked;
          const wrap = this.wrap();
          if (!wrap || !on) return;
          const box = this.levels();
          if (box && !box.querySelector(".ml-cat-select")) await this.showRoots(this.catCode(this.valueInp()?.value));
        },
        async predict() {
          const form = document.getElementById("ad-form");
          const q = [form?.familyName?.value, form?.title?.value, form?.brand?.value, form?.description?.value]
            .map(v => String(v || "").trim()).filter(Boolean).join(" ");
          if (q.length < 2) {
            this.hint(adErr("QueryRequired"));
            return;
          }
          this.hint("Consultando Mercado Livre…");
          try {
            const r = await api(`/marketplaces/MercadoLivre/categories/suggest?q=${encodeURIComponent(q)}`);
            const first = (r.items || r.Items || [])[0];
            const sug = first && this.catCode(first.categoryId || first.CategoryId || first);
            if (!sug) {
              this.hint("Nenhuma categoria sugerida. Escolha na lista.");
              return;
            }
            await this.apply(sug);
            this.fillSuggested(first.attributes || first.Attributes || []);
          } catch (ex) {
            this.hint(adErr(ex.message));
          }
        }
      };
      const mlListingType = {
        sel() { return document.querySelector(".ml-listing-type"); },
        fill(items, selected) {
          const el = this.sel();
          if (!el) return;
          const list = (items && items.length) ? items : mlListingFallback;
          const keep = mlListingTypeCode(selected != null ? selected : el.value) || "gold_special";
          el.innerHTML = mlListingTypeOptions(list, keep);
          el.value = keep;
          if (el.value !== keep) el.value = "gold_special";
        },
        async load(selected) {
          const el = this.sel();
          if (!el) return;
          try {
            const r = await api("/marketplaces/MercadoLivre/listing-types");
            this.fill(r.items || r.Items || [], selected);
          } catch {
            this.fill(mlListingFallback, selected);
          }
        }
      };
      const syncExtras = (opts) => {
        const selected = [...document.querySelectorAll(".mkt-code:checked")].map(c => c.value);
        document.querySelectorAll(".ad-extra").forEach(el => el.classList.toggle("on", selected.includes(el.dataset.extra)));
        if (opts && opts.skipEnsure) return;
        if (selected.includes("MercadoLivre")) mlCat.ensure();
      };
      document.querySelectorAll(".mkt-code").forEach(c => c.onchange = syncExtras);
      if ($(".ml-cat-predict")) $(".ml-cat-predict").onclick = () => mlCat.predict();
      syncExtras();
      mlListingType.load();
      const fillFromAd = async (ad) => {
        const form = $("#ad-form");
        if (!form || !ad) return;
        const kind = ad.kind === "Kit" ? "Kit" : "Product";
        form.querySelectorAll("input[name=kind]").forEach(r => { r.checked = r.value === kind; });
        syncKind();
        const sku = String(ad.sku || "");
        form.sku.value = sku && sku.length <= 58 ? `${sku}-COPIA` : "";
        form.title.value = ad.title || "";
        form.familyName.value = ad.familyName || String(ad.title || "").slice(0, 60);
        form.description.value = ad.description || "";
        form.price.value = ad.price != null ? ad.price : "";
        form.availableQuantity.value = ad.availableQuantity != null ? ad.availableQuantity : 1;
        form.condition.value = ad.condition || "new";
        form.brand.value = ad.brand || "";
        form.gtin.value = ad.gtin || "";
        form.weightGrams.value = ad.weightGrams != null ? ad.weightGrams : "";
        form.heightCm.value = ad.heightCm != null ? ad.heightCm : "";
        form.widthCm.value = ad.widthCm != null ? ad.widthCm : "";
        form.lengthCm.value = ad.lengthCm != null ? ad.lengthCm : "";
        if (form.vendorUserId) form.vendorUserId.value = ad.vendorUserId || "";
        itemsBox.querySelectorAll(".ad-item-row").forEach(row => row.remove());
        const items = ad.items && ad.items.length ? ad.items : [{ sku: "", quantity: 1 }];
        items.forEach(i => addRow(i.sku, i.quantity));
        syncKind();
        const codes = new Set((ad.channels || []).map(c => c.marketplaceCode));
        document.querySelectorAll(".mkt-code").forEach(c => { c.checked = codes.size ? codes.has(c.value) : true; });
        document.querySelectorAll(".attr-field").forEach(inp => { inp.value = ""; });
        (ad.attributes || []).forEach(x => {
          const inp = document.querySelector(`.attr-field[data-mkt="${CSS.escape(x.marketplaceCode)}"][data-key="${CSS.escape(x.fieldName)}"]`);
          if (inp) inp.value = x.fieldValue || "";
        });
        const savedListingType = (ad.attributes || []).find(x =>
          x.marketplaceCode === "MercadoLivre" && x.fieldName === "listingTypeId")?.fieldValue;
        await mlListingType.load(savedListingType);
        syncExtras({ skipEnsure: true });
        const mlSaved = {};
        (ad.attributes || []).forEach(x => {
          if (x.marketplaceCode === "MercadoLivre" && String(x.fieldName || "").startsWith("ml:"))
            mlSaved[x.fieldName] = x.fieldValue || "";
        });
        const mlId = (ad.attributes || []).find(x => x.marketplaceCode === "MercadoLivre" && x.fieldName === "categoryId")?.fieldValue;
        if (mlCat.catCode(mlId)) {
          await mlCat.apply(mlId, mlSaved);
        } else if (document.querySelector(".mkt-code[value='MercadoLivre']")?.checked) {
          await mlCat.showRoots();
        }
        const msg = $("#ad-msg");
        if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-success">Formulário preenchido a partir de ${esc(ad.sku)}. Confira o SKU novo e salve.</div>`;
        form.scrollIntoView({ behavior: "smooth", block: "start" });
        form.familyName.focus();
      };
      document.querySelectorAll(".ad-reuse").forEach(btn => {
        btn.onclick = () => {
          const card = btn.closest(".ad-card");
          const ad = (window.__vilmoAds || {})[card && card.dataset.ad];
          fillFromAd(ad);
        };
      });
      document.querySelectorAll(".ad-card-open").forEach(sw => {
        sw.onchange = () => {
          const card = sw.closest(".ad-card");
          if (card) card.classList.toggle("is-open", sw.checked);
        };
      });
      $("#ad-form").onsubmit = async (e) => {
        e.preventDefault();
        const fd = new FormData(e.target);
        const items = [...itemsBox.querySelectorAll(".ad-item-row")].map(row => ({
          sku: row.querySelector(".item-sku").value,
          quantity: Number(row.querySelector(".item-qty").value || 1)
        })).filter(i => i.sku && i.quantity > 0);
        const marketplaceCodes = [...document.querySelectorAll(".mkt-code:checked")].map(c => c.value);
        mlCat.syncValue();
        const attributes = [...document.querySelectorAll(".attr-field")]
          .filter(inp => marketplaceCodes.includes(inp.dataset.mkt) && inp.value)
          .map(inp => ({ marketplaceCode: inp.dataset.mkt, fieldName: inp.dataset.key, fieldValue: inp.value }));
        document.querySelectorAll(".ml-attr-field").forEach(el => {
          if (!marketplaceCodes.includes("MercadoLivre")) return;
          const p = mlCat.attrPayload(el);
          if (!p) return;
          attributes.push({ marketplaceCode: "MercadoLivre", fieldName: el.dataset.key, fieldValue: JSON.stringify(p) });
        });
        const msg = $("#ad-msg");
        if (marketplaceCodes.includes("MercadoLivre")) {
          const cat = attributes.find(a => a.fieldName === "categoryId")?.fieldValue || "";
          if (cat && !/^MLB\d+$/i.test(cat)) {
            if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(adErr("InvalidCategoryId"))}</div>`;
            return;
          }
          const listingType = attributes.find(a => a.fieldName === "listingTypeId")?.fieldValue || "";
          if (!mlListingTypeCode(listingType)) {
            if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(adErr("InvalidListingTypeId"))}</div>`;
            return;
          }
          if (mlCat.missingRequired()) {
            if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(adErr("MlAttributeRequired"))}</div>`;
            return;
          }
          if (mlCat.missingSizeGrid()) {
            if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(adErr("InvalidSizeGridId"))}</div>`;
            return;
          }
        }
        const num = (k) => { const v = fd.get(k); return v === "" || v == null ? null : Number(v); };
        const body = {
          kind: fd.get("kind"),
          sku: fd.get("sku") || undefined,
          title: fd.get("title"),
          familyName: fd.get("familyName"),
          description: fd.get("description") || undefined,
          price: num("price"),
          availableQuantity: num("availableQuantity"),
          condition: fd.get("condition"),
          brand: fd.get("brand") || undefined,
          gtin: fd.get("gtin") || undefined,
          weightGrams: num("weightGrams"),
          heightCm: num("heightCm"),
          widthCm: num("widthCm"),
          lengthCm: num("lengthCm"),
          vendorUserId: fd.get("vendorUserId") || undefined,
          marketplaceCodes,
          enqueuePublish: false,
          items,
          attributes
        };
        if (!marketplaceCodes.length) {
          if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(adErr("MarketplaceRequired"))}</div>`;
          return;
        }
        try {
          await api("/advertisements", { method: "POST", body });
          renderRoute();
        } catch (ex) {
          if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(adErr(ex.message))}</div>`;
        }
      };
    }
    const runAdAction = async (el, path) => {
      const ch = el.closest(".ad-channel");
      const card = el.closest(".ad-card");
      const msg = (card || document).querySelector(".ad-card-msg");
      const adId = (ch || card)?.dataset.ad;
      const code = ch?.dataset.code;
      el.disabled = true;
      try {
        await api(path, { method: "POST", body: {} });
        await renderRoute();
        if (adId) {
          const cardEl = document.querySelector(`.ad-card[data-ad="${CSS.escape(adId)}"]`);
          if (cardEl) {
            cardEl.classList.add("is-open");
            const sw = cardEl.querySelector(".ad-card-open");
            if (sw) sw.checked = true;
          }
        }
        if (adId && code) {
          const logEl = document.querySelector(`.ad-log[data-ad="${adId}"][data-code="${CSS.escape(code)}"]`);
          if (logEl) logEl.open = true;
        }
      } catch (ex) {
        el.disabled = false;
        if (card) {
          card.classList.add("is-open");
          const sw = card.querySelector(".ad-card-open");
          if (sw) sw.checked = true;
        }
        if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">${esc(adErr(ex.message))}</div>`;
        if (ch) {
          const logEl = ch.querySelector(".ad-log");
          if (logEl) logEl.open = true;
        }
      }
    };
    document.querySelectorAll(".ad-publish-ch").forEach(btn => btn.onclick = () => {
      const ch = btn.closest(".ad-channel");
      return runAdAction(btn, `/advertisements/${ch.dataset.ad}/channels/${encodeURIComponent(ch.dataset.code)}/publish`);
    });
    document.querySelectorAll(".ad-cancel").forEach(btn => btn.onclick = () => {
      const ch = btn.closest(".ad-channel");
      return runAdAction(btn, `/advertisements/${ch.dataset.ad}/channels/${encodeURIComponent(ch.dataset.code)}/cancel`);
    });
    document.querySelectorAll(".ad-refresh").forEach(btn => btn.onclick = () => {
      const card = btn.closest(".ad-card");
      return runAdAction(btn, `/advertisements/${card.dataset.ad}/refresh`);
    });
    if ($("#vendor-form")) $("#vendor-form").onsubmit = async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const companyId = fd.get("companyId") || store.companyId;
      const codes = fd.get("codes") === "*" ? ["*"] : fd.get("codes").split(",").map(s => s.trim()).filter(Boolean);
      await api(`/companies/${companyId}/vendors`, { method: "POST", body: {
        email: fd.get("email"), name: fd.get("name"), phone: fd.get("phone"), password: fd.get("password"), marketplaceCodes: codes
      }});
      location.hash = "#/vendedores";
    };
    document.querySelectorAll(".mkt-form").forEach(form => form.onsubmit = async (e) => {
      e.preventDefault();
      const fd = new FormData(form);
      const fields = {};
      fd.forEach((v, k) => { if (k !== "isEnabled") fields[k] = v; });
      await api(`/companies/${store.companyId}/marketplaces/${form.dataset.code}`, {
        method: "PUT",
        body: { isEnabled: form.isEnabled.checked, fields }
      });
      renderRoute();
    });
    document.querySelectorAll(".connect").forEach(btn => btn.onclick = async () => {
      btn.disabled = true;
      try {
        const r = await api(`/marketplaces/${btn.dataset.code}/connect`, { method: "POST", body: {} });
        const logEl = document.querySelector(`.mkt-form[data-code="${CSS.escape(btn.dataset.code)}"] .ad-log`);
        if (logEl && r.logs) {
          logEl.outerHTML = stepAccordionHtml("Passos da conexão", r.logs, {
            open: true,
            dataAttr: `data-code="${esc(btn.dataset.code)}"`
          });
          document.querySelector(`.mkt-form[data-code="${CSS.escape(btn.dataset.code)}"] .ad-log`).open = true;
        }
        if (r.authorizationUrl) location.href = r.authorizationUrl;
      } catch (ex) {
        btn.disabled = false;
        alert(ex.message || "Falha ao conectar.");
      }
    });
    if ($("#emit-nfe")) $("#emit-nfe").onclick = async () => {
      const id = location.hash.split("/")[2];
      await api(`/sales/${id}/nfe`, { method: "POST", body: {} });
      renderRoute();
    };
    if ($("#print-label")) {
      const a = $("#print-label");
      a.onclick = async (e) => {
        e.preventDefault();
        const id = location.hash.split("/")[2];
        const res = await fetch(`${API}/sales/${id}/label.pdf`, { headers: { Authorization: `Bearer ${store.token}`, "X-Company-Id": store.companyId } });
        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        window.open(url, "_blank");
      };
    }
    if ($("#cert-form")) $("#cert-form").onsubmit = async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      await api(`/companies/${store.companyId}/certificate`, { method: "POST", body: fd });
      alert("A1 enviado.");
    };
  }

  function scanCallbacks() {
    return {
      video: document.getElementById("cam"),
      onChave(chave) {
        const input = document.getElementById("chave");
        if (input) input.value = chave;
        const msg = document.getElementById("nfe-msg");
        if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-success">Chave lida. Confira e toque em Ingerir chave.</div>`;
        stopCamera();
      },
      onInvalid() {
        const hint = document.getElementById("scan-hint");
        if (hint) hint.textContent = "Código não é chave NF-e. Aponte para o código de 44 dígitos ou o QR da DANFE.";
      }
    };
  }

  async function startCamera() {
    const overlay = document.getElementById("camera-overlay");
    const hint = document.getElementById("scan-hint");
    if (overlay) overlay.classList.remove("hidden");
    if (hint) hint.textContent = "Aponte para o código de barras ou QR da DANFE";
    if (!window.VilmoNfeScan) {
      alert("Leitor de código não carregou. Digite a chave de 44 dígitos.");
      return;
    }
    try {
      await VilmoNfeScan.start(scanCallbacks());
    } catch (err) {
      stopCamera();
      const msg = document.getElementById("nfe-msg");
      if (msg) msg.innerHTML = `<div class="kt-alert kt-alert-danger">Permita a câmera nas configurações do navegador. Você ainda pode digitar a chave.</div>`;
    }
  }
  async function flipCamera() {
    if (!window.VilmoNfeScan) return;
    const hint = document.getElementById("scan-hint");
    if (hint) hint.textContent = "Trocando câmera…";
    try { await VilmoNfeScan.flip(scanCallbacks()); } catch { /* keep current */ }
  }
  function stopCamera() {
    if (window.VilmoNfeScan) VilmoNfeScan.stop();
    document.getElementById("camera-overlay")?.classList.add("hidden");
  }

  return { bootLogin, bootApp };
})();
