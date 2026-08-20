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
    window.addEventListener("hashchange", renderRoute);
    if (!location.hash) {
      location.hash = store.me.level === "Admin" ? "#/empresas" : (store.me.level === "Vendor" ? "#/vendas" : "#/dashboard");
    } else renderRoute();
    document.getElementById("logout").onclick = () => { store.token = null; location.href = "login.html"; };
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

  async function renderRoute() {
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
        </form>
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
      <td><button class="kt-btn kt-btn-sm kt-btn-primary pub" data-sku="${p.sku}">Publicar</button></td></tr>`);
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
    const list = await api("/listings");
    const rows = list.map(l => `<tr><td>${l.sku}</td><td>${l.marketplaceCode}</td><td>${l.status}</td><td>${l.remoteId || ""}</td></tr>`);
    return page("Anúncios", "", table(["SKU", "Marketplace", "Status", "Id remoto"], rows));
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
    const cards = list.map(m => `
      <form class="vilmo-card mkt-form" data-code="${m.code}">
        <div class="flex justify-between items-center mb-3">
          <h3 class="font-medium">${m.displayName}</h3>
          <label class="text-sm"><input type="checkbox" name="isEnabled" ${m.isEnabled ? "checked" : ""}> Ativo</label>
        </div>
        <div class="text-xs mb-2">Status: ${m.linkStatus}</div>
        ${(m.fields||[]).map(f => f.filledByOauth ? `<div class="text-sm text-muted-foreground">${f.label}: ${f.value || "—"}</div>` :
          `<label class="block mb-2 text-sm">${f.label}<input class="kt-input" name="${f.parameterKey}" value="${f.value || ""}" ${f.isSecret ? 'type="password"' : ""}></label>`).join("")}
        <div class="flex gap-2 mt-3">
          <button class="kt-btn kt-btn-primary" type="submit">Salvar</button>
          <button class="kt-btn kt-btn-outline connect" type="button" data-code="${m.code}">Conectar</button>
        </div>
      </form>`).join("");
    return page("Marketplaces da empresa", "", `<div class="vilmo-grid cols-2">${cards}</div>`);
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
        msg.innerHTML = `<div class="kt-alert kt-alert-success">Status: ${r.status || "queued"} · ${r.chave || chave}</div>`;
      } catch (ex) { msg.innerHTML = `<div class="kt-alert kt-alert-danger">${ex.message}</div>`; }
    };
    if ($("#xml-form")) $("#xml-form").onsubmit = async (e) => {
      e.preventDefault();
      const file = e.target.file.files[0];
      if (!file) return;
      const fd = new FormData();
      fd.append("file", file);
      await api("/nfe/xml", { method: "POST", body: fd, headers: {} });
      alert("XML ingerido.");
    };
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
      await api("/advertisements", { method: "POST", body: { sku: btn.dataset.sku } });
      location.hash = "#/anuncios";
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
      const r = await api(`/marketplaces/${btn.dataset.code}/connect`, { method: "POST", body: {} });
      location.href = r.authorizationUrl;
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
