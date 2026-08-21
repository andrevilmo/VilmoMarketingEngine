using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vilmo.Data;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class MercadoLivreCategoryService(
    AppDbContext db,
    IHttpClientFactory httpFactory,
    IMemoryCache cache,
    SecretProtector? protector = null)
{
    public const string MarketplaceCode = "MercadoLivre";
    const string DefaultBase = "https://api.mercadolibre.com";
    const string Site = "MLB";
    static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);

    /// <summary>Official MLB root categories. Used when GET /sites/MLB/categories is unavailable.</summary>
    public static readonly IReadOnlyList<MlCategoryRef> FallbackRoots =
    [
        new("MLB5672", "Acessórios para Veículos"),
        new("MLB271599", "Agro"),
        new("MLB1403", "Alimentos e Bebidas"),
        new("MLB1367", "Antiguidades e Coleções"),
        new("MLB1368", "Arte, Papelaria e Armarinho"),
        new("MLB1384", "Bebês"),
        new("MLB1246", "Beleza e Cuidado Pessoal"),
        new("MLB1132", "Brinquedos e Hobbies"),
        new("MLB1430", "Calçados, Roupas e Bolsas"),
        new("MLB1743", "Carros, Motos e Outros"),
        new("MLB1574", "Casa, Móveis e Decoração"),
        new("MLB1051", "Celulares e Telefones"),
        new("MLB1500", "Construção"),
        new("MLB1039", "Câmeras e Acessórios"),
        new("MLB5726", "Eletrodomésticos"),
        new("MLB1000", "Eletrônicos, Áudio e Vídeo"),
        new("MLB1276", "Esportes e Fitness"),
        new("MLB263532", "Ferramentas"),
        new("MLB12404", "Festas e Lembrancinhas"),
        new("MLB1144", "Games"),
        new("MLB1459", "Imóveis"),
        new("MLB1499", "Indústria e Comércio"),
        new("MLB1648", "Informática"),
        new("MLB218519", "Ingressos"),
        new("MLB1182", "Instrumentos Musicais"),
        new("MLB3937", "Joias e Relógios"),
        new("MLB1196", "Livros, Revistas e Comics"),
        new("MLB1953", "Mais Categorias"),
        new("MLB1168", "Música, Filmes e Seriados"),
        new("MLB1071", "Pet Shop"),
        new("MLB264586", "Saúde"),
        new("MLB1540", "Serviços")
    ];

    public async Task<MlCategoryList> ListRootsAsync(CancellationToken ct)
    {
        if (cache.TryGetValue("ml:cats:roots", out MlCategoryList? cached) && cached is not null)
            return cached;
        var live = await GetJsonAsync($"/sites/{Site}/categories", ct);
        IReadOnlyList<MlCategoryRef> items;
        var source = "mercadolivre";
        if (live is { ValueKind: JsonValueKind.Array } arr && arr.GetArrayLength() > 0)
        {
            items = arr.EnumerateArray()
                .Select(ReadRef)
                .Where(x => x is not null)
                .Cast<MlCategoryRef>()
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            items = FallbackRoots;
            source = "fallback";
        }
        var result = new MlCategoryList(items, source);
        cache.Set("ml:cats:roots", result, CacheFor);
        return result;
    }

    /// <summary>Official MLB listing types. Used when GET /sites/MLB/listing_types is unavailable.</summary>
    public static readonly IReadOnlyList<MlListingType> FallbackListingTypes =
    [
        new("gold_special", "Clássica"),
        new("gold_pro", "Premium"),
        new("free", "Gratuita"),
        new("gold", "Ouro"),
        new("silver", "Prata"),
        new("bronze", "Bronze")
    ];

    public async Task<MlListingTypeList> ListListingTypesAsync(CancellationToken ct)
    {
        if (cache.TryGetValue("ml:listing-types", out MlListingTypeList? cached) && cached is not null)
            return cached;
        var live = await GetJsonAsync($"/sites/{Site}/listing_types", ct);
        IReadOnlyList<MlListingType> items;
        var source = "mercadolivre";
        if (live is { ValueKind: JsonValueKind.Array } arr && arr.GetArrayLength() > 0)
        {
            items = arr.EnumerateArray()
                .Select(ReadListingType)
                .Where(x => x is not null)
                .Cast<MlListingType>()
                .GroupBy(x => x.Id, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(RankListingType)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            items = [];
        }
        if (items.Count == 0)
        {
            items = FallbackListingTypes;
            source = "fallback";
        }
        var result = new MlListingTypeList(items, source);
        cache.Set("ml:listing-types", result, CacheFor);
        return result;
    }

    public async Task<object> SellerStatusAsync(Guid companyId, CancellationToken ct)
    {
        var seller = await ReadSellerAsync(companyId, ct);
        if (seller is null)
            return MercadoLivreSellerListing.PublicStatus(null, false);
        var me = await SendJsonAsync(HttpMethod.Get, "/users/me", ct, seller.Value.Token, seller.Value.SellerId);
        return MercadoLivreSellerListing.PublicStatus(me, true);
    }

    static MlListingType? ReadListingType(JsonElement x)
    {
        if (!MercadoLivreListingTypeId.TryNormalize(Str(x, "id"), out var id))
            return null;
        return new MlListingType(id, Str(x, "name") ?? id);
    }

    static int RankListingType(MlListingType t) => t.Id switch
    {
        "gold_special" => 0,
        "gold_pro" => 1,
        "free" => 2,
        _ => 10
    };

    public async Task<MlCategoryDetail?> GetAsync(string categoryId, CancellationToken ct)
    {
        var id = (categoryId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(id)) return null;
        var key = $"ml:cats:{id}";
        if (cache.TryGetValue(key, out MlCategoryDetail? cached) && cached is not null)
            return cached;
        var json = await GetJsonAsync($"/categories/{Uri.EscapeDataString(id)}", ct);
        if (json is null || json.Value.ValueKind != JsonValueKind.Object) return null;
        var el = json.Value;
        var children = ReadRefList(el, "children_categories");
        var path = ReadRefList(el, "path_from_root");
        var listingAllowed = true;
        if (el.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object
            && settings.TryGetProperty("listing_allowed", out var la))
        {
            listingAllowed = la.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => !string.Equals(la.GetString(), "not_allowed", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }
        string? catalogDomain = Str(el, "catalog_domain");
        if (string.IsNullOrWhiteSpace(catalogDomain)
            && el.TryGetProperty("settings", out var settingsObj) && settingsObj.ValueKind == JsonValueKind.Object)
            catalogDomain = Str(settingsObj, "catalog_domain");
        var detail = new MlCategoryDetail(
            el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? id : id,
            el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? id : id,
            path,
            children,
            children.Count == 0,
            listingAllowed,
            catalogDomain
        );
        cache.Set(key, detail, CacheFor);
        return detail;
    }

    public async Task<MlCategorySuggest> SuggestAsync(string? q, CancellationToken ct)
    {
        var query = (q ?? "").Trim();
        if (query.Length < 2)
            throw new ArgumentException("QueryRequired");
        if (query.Length > 200) query = query[..200];
        var json = await GetJsonAsync($"/sites/{Site}/domain_discovery/search?q={Uri.EscapeDataString(query)}", ct)
            ?? throw new InvalidOperationException("CategorySuggestFailed");
        if (json.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("CategorySuggestFailed");
        var items = json.EnumerateArray().Select(x => new MlCategorySuggestion(
            Str(x, "category_id") ?? "",
            Str(x, "category_name") ?? "",
            Str(x, "domain_id"),
            Str(x, "domain_name"),
            ReadSuggestedAttributes(x)
        )).Where(x => !string.IsNullOrWhiteSpace(x.CategoryId)).ToList();
        return new MlCategorySuggest(items, query);
    }

    public async Task<MlCategoryAttributeList> ListAttributesAsync(string categoryId, CancellationToken ct)
    {
        if (!MercadoLivreCategoryId.TryNormalize(categoryId, out var id))
            throw new ArgumentException("InvalidCategoryId");
        var key = $"ml:cats:{id}:attrs";
        if (cache.TryGetValue(key, out MlCategoryAttributeList? cached) && cached is not null)
            return cached;
        var json = await GetJsonAsync($"/categories/{Uri.EscapeDataString(id)}/attributes", ct);
        if (json is null || json.Value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("CategoryAttributesFailed");
        var items = json.Value.EnumerateArray()
            .Select(ReadAttribute)
            .Where(x => x is not null)
            .Cast<MlCategoryAttribute>()
            .ToList();
        var sizeChartRequired = items.Any(x =>
            x.ValueType.Equals("grid_id", StringComparison.OrdinalIgnoreCase)
            || x.Id.Equals("SIZE_GRID_ID", StringComparison.OrdinalIgnoreCase));
        if (sizeChartRequired)
        {
            items = items.Select(x =>
                x.Id.Equals("SIZE_GRID_ID", StringComparison.OrdinalIgnoreCase)
                || x.Id.Equals("SIZE_GRID_ROW_ID", StringComparison.OrdinalIgnoreCase)
                || x.ValueType.Equals("grid_id", StringComparison.OrdinalIgnoreCase)
                || x.ValueType.Equals("grid_row_id", StringComparison.OrdinalIgnoreCase)
                    ? x with { Required = true }
                    : x).ToList();
        }
        var recommended = await ReadRecommendedIdsAsync(id, ct);
        foreach (var rec in recommended)
        {
            var match = items.FirstOrDefault(x => x.Id.Equals(rec, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !match.Required)
            {
                var idx = items.IndexOf(match);
                items[idx] = match with { Recommended = true };
            }
        }
        items = items
            .OrderByDescending(x => x.Required)
            .ThenByDescending(x => x.Recommended)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var domain = (await GetAsync(id, ct))?.CatalogDomain;
        var result = new MlCategoryAttributeList(id, items, recommended, "mercadolivre", domain, sizeChartRequired);
        cache.Set(key, result, CacheFor);
        return result;
    }

    public async Task<MlSizeChartList> ListSizeChartsAsync(
        Guid companyId, string categoryId, string? genderId, string? genderName, string? brand, CancellationToken ct)
    {
        if (!MercadoLivreCategoryId.TryNormalize(categoryId, out var id))
            throw new ArgumentException("InvalidCategoryId");
        var domain = (await GetAsync(id, ct))?.CatalogDomain;
        if (string.IsNullOrWhiteSpace(domain))
            return new MlSizeChartList(id, domain, [], "no_domain", "Esta categoria não tem domínio de guia de tamanhos.");
        var creds = await ReadSellerAsync(companyId, ct);
        if (creds is null)
            return new MlSizeChartList(id, domain, [], "needs_token", "Conecte o Mercado Livre para listar as guias de tamanho.");
        IReadOnlyList<MlSizeChartRef> charts = [];
        string? lastError = null;
        var usedDomain = DomainIds(domain)[0];
        foreach (var domainId in DomainIds(domain))
        {
            foreach (var filters in SizeChartFilterSets(genderId, genderName, brand))
            {
                var body = new Dictionary<string, object?>
                {
                    ["domain_id"] = domainId,
                    ["site_id"] = Site,
                    ["seller_id"] = creds.Value.SellerId,
                    ["offset"] = 0,
                    ["limit"] = 50,
                    ["attributes"] = filters
                };
                var call = await CallAsync(HttpMethod.Post, "/catalog/charts/search", ct, creds.Value.Token, creds.Value.SellerId, body);
                if (call.Json is null)
                {
                    lastError = call.Error ?? $"HTTP {call.Status}";
                    continue;
                }
                var found = ReadCharts(call.Json);
                if (found.Count == 0) continue;
                charts = MergeCharts(charts, found);
                usedDomain = domainId;
                break;
            }
            if (charts.Count > 0) break;
        }
        if (charts.Count == 0)
        {
            var created = await TryCreateSizeChartAsync(creds.Value, usedDomain, genderId, genderName, brand, ct);
            if (created.Chart is not null)
            {
                charts = [created.Chart];
                lastError = null;
            }
            else if (!string.IsNullOrWhiteSpace(created.Error))
                lastError = created.Error;
        }
        var source = charts.Count > 0 ? "mercadolivre" : lastError is null ? "empty" : "error";
        var message = charts.Count > 0
            ? null
            : lastError is not null
                ? $"Mercado Livre não listou guias ({lastError})."
                : "Nenhuma guia para este gênero. Escolha outro gênero ou crie a guia no Mercado Livre.";
        return new MlSizeChartList(id, usedDomain, charts, source, message);
    }

    public async Task<MlSizeChartDetail?> GetSizeChartAsync(Guid companyId, string chartId, CancellationToken ct)
    {
        var id = (chartId ?? "").Trim();
        if (id.Length == 0 || id.Contains('/') || id.Contains(' '))
            throw new ArgumentException("InvalidSizeChartId");
        var creds = await ReadSellerAsync(companyId, ct);
        if (creds is null) return null;
        var json = await SendJsonAsync(HttpMethod.Get, $"/catalog/charts/{Uri.EscapeDataString(id)}", ct, creds.Value.Token, creds.Value.SellerId);
        if (json is null || json.Value.ValueKind != JsonValueKind.Object) return null;
        return ReadChartDetail(json.Value);
    }

    async Task<string> BaseUrlAsync(CancellationToken ct)
    {
        var url = await db.Marketplaces.AsNoTracking()
            .Where(m => m.Code == MarketplaceCode)
            .Select(m => m.BaseUrl)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(url) ? DefaultBase : url.TrimEnd('/');
    }

    async Task<JsonElement?> GetJsonAsync(string path, CancellationToken ct) =>
        (await CallAsync(HttpMethod.Get, path, ct)).Json;

    async Task<JsonElement?> SendJsonAsync(
        HttpMethod method, string path, CancellationToken ct,
        string? bearer = null, long? callerId = null, object? body = null) =>
        (await CallAsync(method, path, ct, bearer, callerId, body)).Json;

    async Task<MlHttp> CallAsync(
        HttpMethod method, string path, CancellationToken ct,
        string? bearer = null, long? callerId = null, object? body = null)
    {
        var url = $"{await BaseUrlAsync(ct)}{path}";
        try
        {
            var client = httpFactory.CreateClient("marketplace");
            using var req = new HttpRequestMessage(method, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(bearer))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            if (callerId is > 0)
                req.Headers.TryAddWithoutValidation("x-caller-id", callerId.Value.ToString());
            if (body is not null)
                req.Content = JsonContent.Create(body);
            using var resp = await client.SendAsync(req, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return new(null, (int)resp.StatusCode, TrimErr(raw, resp.StatusCode));
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "null" : raw);
            return new(doc.RootElement.Clone(), (int)resp.StatusCode, null);
        }
        catch (Exception ex)
        {
            return new(null, 0, ex.Message);
        }
    }

    static string TrimErr(string raw, HttpStatusCode status)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var el = doc.RootElement;
            var parts = new List<string>();
            var msg = Str(el, "message") ?? Str(el, "error");
            if (!string.IsNullOrWhiteSpace(msg)) parts.Add(msg);
            foreach (var key in new[] { "cause", "errors" })
            {
                if (!el.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var c in arr.EnumerateArray())
                {
                    var m = Str(c, "message") ?? Str(c, "code");
                    if (c.TryGetProperty("cell", out var cell) && cell.ValueKind == JsonValueKind.Object)
                    {
                        var attr = Str(cell, "attribute_id");
                        if (!string.IsNullOrWhiteSpace(attr) && (m is null || !m.Contains(attr, StringComparison.Ordinal)))
                            m = string.IsNullOrWhiteSpace(m) ? attr : $"{m} [{attr}]";
                    }
                    if (!string.IsNullOrWhiteSpace(m) && !parts.Contains(m)) parts.Add(m);
                }
            }
            if (parts.Count > 0) return string.Join(" — ", parts);
        }
        catch (JsonException)
        {
            /* keep status */
        }
        var t = (raw ?? "").Trim();
        if (t.StartsWith("<", StringComparison.Ordinal)) return $"HTTP {(int)status}";
        if (t.Length > 180) t = t[..180];
        return t.Length > 0 ? t : $"HTTP {(int)status}";
    }

    async Task<(string Token, long SellerId)?> ReadSellerAsync(Guid companyId, CancellationToken ct)
    {
        var cfg = await db.CompanyMarketplaceConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == MarketplaceCode, ct);
        if (cfg is null) return null;
        var tokenRow = await db.CompanyMarketplaceParameters.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == "AccessToken", ct);
        var userRow = await db.CompanyMarketplaceParameters.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == "UserId", ct);
        var token = tokenRow?.ParameterValue;
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (tokenRow is { IsSecret: true } && protector is not null)
        {
            try { token = protector.Unprotect(token); }
            catch { /* keep packed value */ }
        }
        if (token.Contains("demo", StringComparison.OrdinalIgnoreCase)) return null;
        long.TryParse(userRow?.ParameterValue, out var sellerId);
        if (sellerId <= 0)
        {
            var me = await SendJsonAsync(HttpMethod.Get, "/users/me", ct, token);
            if (me is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("id", out var idEl))
            {
                if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out var n)) sellerId = n;
                else long.TryParse(idEl.GetString(), out sellerId);
            }
        }
        return (token, sellerId);
    }

    static IReadOnlyList<string> DomainIds(string domain)
    {
        var d = domain.Trim();
        if (d.StartsWith($"{Site}-", StringComparison.OrdinalIgnoreCase))
            return [d[(Site.Length + 1)..]];
        return [d];
    }

    static IEnumerable<List<object>> SizeChartFilterSets(string? genderId, string? genderName, string? brand)
    {
        var gender = SizeChartGender(genderId, genderName);
        var hasBrand = !string.IsNullOrWhiteSpace(brand);
        if (gender is not null && hasBrand)
            yield return [gender, new { id = "BRAND", values = new[] { new { name = brand!.Trim() } } }];
        if (gender is not null)
            yield return [gender];
        yield return [];
    }

    static object? SizeChartGender(string? genderId, string? genderName)
    {
        if (string.IsNullOrWhiteSpace(genderId) && string.IsNullOrWhiteSpace(genderName))
            return null;
        var val = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(genderId)) val["id"] = genderId.Trim();
        if (!string.IsNullOrWhiteSpace(genderName)) val["name"] = genderName.Trim();
        return new { id = "GENDER", values = new[] { val } };
    }

    static IReadOnlyList<MlSizeChartRef> MergeCharts(IReadOnlyList<MlSizeChartRef> current, IReadOnlyList<MlSizeChartRef> extra)
    {
        var map = current.ToDictionary(x => x.Id, StringComparer.Ordinal);
        foreach (var c in extra)
            map.TryAdd(c.Id, c);
        return map.Values.ToList();
    }

    async Task<(MlSizeChartRef? Chart, string? Error)> TryCreateSizeChartAsync(
        (string Token, long SellerId) creds, string domainId,
        string? genderId, string? genderName, string? brand, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(genderId) && string.IsNullOrWhiteSpace(genderName))
            return (null, null);
        var spec = await ReadGridSpecAsync(domainId, genderId, genderName, brand, creds, ct);
        var mainId = spec.MainId ?? "SIZE";
        var genderVal = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(genderId)) genderVal["id"] = genderId.Trim();
        if (!string.IsNullOrWhiteSpace(genderName)) genderVal["name"] = genderName.Trim();
        var attrs = new List<object> { new { id = "GENDER", values = new[] { genderVal } } };
        if (!string.IsNullOrWhiteSpace(brand))
            attrs.Add(new { id = "BRAND", values = new[] { new { name = brand.Trim() } } });
        foreach (var field in spec.ChartFields)
        {
            if (field.Id.Equals("GENDER", StringComparison.OrdinalIgnoreCase)
                || field.Id.Equals("BRAND", StringComparison.OrdinalIgnoreCase))
                continue;
            var value = ChartFieldValue(field);
            if (value is null) continue;
            attrs.Add(new { id = field.Id, values = new[] { value } });
        }
        var sizes = RowSizeNames(spec, mainId, genderId, genderName);
        var rows = sizes.Select((size, i) => new
        {
            attributes = RowAttributes(spec, mainId, size, i)
        }).ToArray();
        var body = new Dictionary<string, object?>
        {
            ["names"] = new Dictionary<string, string> { [Site] = ChartTitle(genderName, brand) },
            ["domain_id"] = domainId,
            ["site_id"] = Site,
            ["main_attribute"] = new { attributes = new[] { new { site_id = Site, id = mainId } } },
            ["attributes"] = attrs,
            ["rows"] = rows
        };
        if (!string.IsNullOrWhiteSpace(spec.MeasureType))
            body["measure_type"] = spec.MeasureType;
        var call = await CallAsync(HttpMethod.Post, "/catalog/charts", ct, creds.Token, creds.SellerId, body);
        if (call.Json is { ValueKind: JsonValueKind.Object } json)
        {
            var parsed = ReadChartRef(json);
            if (parsed is not null) return (parsed, null);
            var id = Str(json, "id");
            if (!string.IsNullOrWhiteSpace(id))
                return (new MlSizeChartRef(id, ChartTitle(genderName, brand), "SPECIFIC", domainId), null);
        }
        return (null, call.Error ?? spec.Error ?? "create_failed");
    }

    async Task<GridSpec> ReadGridSpecAsync(
        string domainId, string? genderId, string? genderName, string? brand,
        (string Token, long SellerId) creds, CancellationToken ct)
    {
        var genderVal = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(genderId)) genderVal["id"] = genderId.Trim();
        if (!string.IsNullOrWhiteSpace(genderName)) genderVal["name"] = genderName.Trim();
        var filter = new List<object>();
        if (genderVal.Count > 0)
        {
            var gender = new Dictionary<string, object?> { ["id"] = "GENDER", ["values"] = new[] { genderVal } };
            if (genderVal.TryGetValue("id", out var gid)) gender["value_id"] = gid;
            if (genderVal.TryGetValue("name", out var gname)) gender["value_name"] = gname;
            filter.Add(gender);
        }
        if (!string.IsNullOrWhiteSpace(brand))
            filter.Add(new Dictionary<string, object?>
            {
                ["id"] = "BRAND",
                ["value_name"] = brand.Trim(),
                ["values"] = new[] { new { name = brand.Trim() } }
            });
        string? lastError = null;
        foreach (var d in PrefixedDomainIds(domainId))
        {
            var post = await CallAsync(
                HttpMethod.Post,
                $"/domains/{Uri.EscapeDataString(d)}/technical_specs?section=grids",
                ct, creds.Token, creds.SellerId,
                new { attributes = filter });
            if (HasGridInput(post.Json))
                return ParseGridSpec(post.Json);
            lastError = post.Error ?? lastError;
            var get = await CallAsync(
                HttpMethod.Get,
                $"/domains/{Uri.EscapeDataString(d)}/technical_specs?section=grids",
                ct, creds.Token, creds.SellerId);
            if (HasGridInput(get.Json))
                return ParseGridSpec(get.Json);
            lastError = get.Error ?? lastError;
        }
        return GridSpec.Fallback(lastError);
    }

    static bool HasGridInput(JsonElement? json) =>
        json is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty("input", out var input)
        && input.ValueKind == JsonValueKind.Object;

    static IReadOnlyList<string> PrefixedDomainIds(string domainId)
    {
        var d = domainId.Trim();
        if (d.StartsWith($"{Site}-", StringComparison.OrdinalIgnoreCase))
            return [d, d[(Site.Length + 1)..]];
        return [$"{Site}-{d}", d];
    }

    static GridSpec ParseGridSpec(JsonElement? json)
    {
        if (json is null) return GridSpec.Fallback(null);
        var fields = new Dictionary<string, GridField>(StringComparer.OrdinalIgnoreCase);
        void Walk(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in el.EnumerateArray()) Walk(c);
                return;
            }
            if (el.ValueKind != JsonValueKind.Object) return;
            var id = Str(el, "id");
            if (!string.IsNullOrWhiteSpace(id) && (el.TryGetProperty("value_type", out _) || el.TryGetProperty("tags", out _)))
            {
                var field = ReadGridField(el, id);
                if (field is not null)
                    fields[field.Id] = field;
            }
            foreach (var p in el.EnumerateObject())
            {
                if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    Walk(p.Value);
            }
        }
        Walk(json.Value);
        if (fields.Count == 0) return GridSpec.Fallback(null);
        var main = fields.Values.FirstOrDefault(x => x.MainCandidate)?.Id
            ?? (fields.ContainsKey("SIZE") ? "SIZE" : fields.Values.FirstOrDefault()?.Id);
        var clothing = fields.Values.Any(x => x.Clothing && x.Required);
        var body = fields.Values.Any(x => x.Body && x.Required);
        var measureType = clothing ? "CLOTHING_MEASURE" : body ? "BODY_MEASURE" : null;
        var chart = fields.Values.Where(IsChartField).ToList();
        var rows = fields.Values.Where(x => IsRowField(x, measureType)).ToList();
        if (rows.Count == 0 && main is not null && fields.TryGetValue(main, out var mainField))
            rows.Add(mainField);
        if (!rows.Any(x => x.Body || x.Clothing))
        {
            measureType ??= "BODY_MEASURE";
            rows.AddRange(GridSpec.Fallback(null).RowFields.Where(x => x.Body || x.Clothing));
        }
        if (!rows.Any(x => x.Id.Equals("FILTRABLE_SIZE", StringComparison.OrdinalIgnoreCase)))
            rows.AddRange(GridSpec.Fallback(null).RowFields.Where(x => x.Id.Equals("FILTRABLE_SIZE", StringComparison.OrdinalIgnoreCase)));
        return new GridSpec(main, measureType, chart, rows, null);
    }

    static GridField? ReadGridField(JsonElement el, string id)
    {
        var hidden = HasTag(el, "hidden") || HasTag(el, "read_only");
        var required = HasTag(el, "required") || HasTag(el, "new_required");
        if (hidden && !required && !HasTag(el, "main_attribute_candidate"))
            return null;
        return new GridField(
            id,
            Str(el, "name") ?? id,
            Str(el, "value_type") ?? "string",
            required,
            HasTag(el, "main_attribute_candidate"),
            HasTag(el, "CLOTHING_MEASURE"),
            HasTag(el, "BODY_MEASURE"),
            Str(el, "default_unit_id") ?? FirstUnitId(el),
            ReadAttributeValues(el),
            Str(el, "hierarchy") ?? "");
    }

    static string? FirstUnitId(JsonElement el)
    {
        if (!el.TryGetProperty("units", out var units) || units.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var u in units.EnumerateArray())
        {
            var id = Str(u, "id");
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        return null;
    }

    static bool IsChartField(GridField field)
    {
        if (field.Id.Equals("GENDER", StringComparison.OrdinalIgnoreCase)
            || field.Id.Equals("BRAND", StringComparison.OrdinalIgnoreCase))
            return true;
        if (field.MainCandidate || field.Body || field.Clothing) return false;
        var h = field.Hierarchy;
        return field.Required && (
            h.Equals("PARENT_PK", StringComparison.OrdinalIgnoreCase)
            || h.Equals("FAMILY", StringComparison.OrdinalIgnoreCase)
            || h.Length == 0);
    }

    static bool IsRowField(GridField field, string? measureType)
    {
        if (field.Id.Equals("GENDER", StringComparison.OrdinalIgnoreCase)
            || field.Id.Equals("BRAND", StringComparison.OrdinalIgnoreCase))
            return false;
        if (measureType == "CLOTHING_MEASURE" && field.Body) return false;
        if (measureType == "BODY_MEASURE" && field.Clothing) return false;
        if (field.MainCandidate || field.Body || field.Clothing) return true;
        var h = field.Hierarchy;
        return h.Equals("CHILD_PK", StringComparison.OrdinalIgnoreCase)
            || h.Equals("ITEM", StringComparison.OrdinalIgnoreCase)
            || h.Equals("CHILD_DEPENDENT", StringComparison.OrdinalIgnoreCase);
    }

    static Dictionary<string, string>? ChartFieldValue(GridField field)
    {
        var hit = field.Values.FirstOrDefault();
        if (hit is not null)
        {
            var val = new Dictionary<string, string> { ["name"] = hit.Name };
            if (!string.IsNullOrWhiteSpace(hit.Id)) val["id"] = hit.Id;
            return val;
        }
        if (field.Id.Equals("STYLE", StringComparison.OrdinalIgnoreCase))
            return new Dictionary<string, string> { ["name"] = "Casual" };
        return null;
    }

    static bool IsChildGender(string? genderId, string? genderName)
    {
        var id = (genderId ?? "").Trim();
        var name = (genderName ?? "").Trim();
        if (id is "19159491" or "339667" or "339668" or "371795") return true;
        return name.Contains("infantil", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Meninos", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Meninas", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Bebês", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Bebes", StringComparison.OrdinalIgnoreCase);
    }

    static IReadOnlyList<string> RowSizeNames(GridSpec spec, string mainId, string? genderId = null, string? genderName = null)
    {
        var preferred = IsChildGender(genderId, genderName)
            ? new[] { "1", "2", "3", "4", "6" }
            : new[] { "PP", "P", "M", "G", "GG" };
        var main = spec.RowFields.FirstOrDefault(x => x.Id.Equals(mainId, StringComparison.OrdinalIgnoreCase))
            ?? spec.RowFields.FirstOrDefault(x => x.MainCandidate);
        var names = main?.Values.Select(v => v.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];
        var picked = preferred.Where(p => names.Any(n => n.Equals(p, StringComparison.OrdinalIgnoreCase))).ToList();
        if (picked.Count > 0) return picked;
        if (names.Count > 0) return names.Take(5).ToList();
        return preferred;
    }

    static List<object> RowAttributes(GridSpec spec, string mainId, string size, int index)
    {
        var rows = spec.RowFields.Count > 0
            ? spec.RowFields
            : GridSpec.Fallback(null).RowFields;
        var list = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in rows)
        {
            if (!seen.Add(field.Id)) continue;
            var value = RowFieldValue(spec, field, mainId, size, index);
            if (value is null) continue;
            list.Add(new { id = field.Id, values = new[] { value } });
        }
        if (!seen.Contains(mainId))
            list.Insert(0, new { id = mainId, values = new[] { new Dictionary<string, string> { ["name"] = size } } });
        return list;
    }

    static Dictionary<string, string>? RowFieldValue(GridSpec _, GridField field, string mainId, string size, int index)
    {
        if (field.Id.Equals(mainId, StringComparison.OrdinalIgnoreCase)
            || field.MainCandidate
            || field.Id.Equals("SIZE", StringComparison.OrdinalIgnoreCase)
            || field.Id.Equals("FILTRABLE_SIZE", StringComparison.OrdinalIgnoreCase)
            || field.Id.EndsWith("_SIZE", StringComparison.OrdinalIgnoreCase))
        {
            var hit = field.Values.FirstOrDefault(v => v.Name.Equals(size, StringComparison.OrdinalIgnoreCase))
                ?? field.Values.FirstOrDefault(v => v.Name.StartsWith(size + " ", StringComparison.OrdinalIgnoreCase))
                ?? field.Values.FirstOrDefault(v =>
                    v.Name.StartsWith(size, StringComparison.OrdinalIgnoreCase)
                    && (v.Name.Contains("ano", StringComparison.OrdinalIgnoreCase) || char.IsDigit(v.Name[0])));
            var val = new Dictionary<string, string> { ["name"] = hit?.Name ?? size };
            if (!string.IsNullOrWhiteSpace(hit?.Id)) val["id"] = hit!.Id;
            return val;
        }
        if (field.ValueType.Equals("number_unit", StringComparison.OrdinalIgnoreCase)
            || field.Body || field.Clothing)
        {
            var unit = string.IsNullOrWhiteSpace(field.DefaultUnit) ? "cm" : field.DefaultUnit;
            var from = field.Id.Contains("TO", StringComparison.OrdinalIgnoreCase);
            var cm = 80 + (index * 4) + (from ? 4 : 0);
            return new Dictionary<string, string> { ["name"] = $"{cm} {unit}" };
        }
        var first = field.Values.FirstOrDefault();
        if (first is not null)
        {
            var val = new Dictionary<string, string> { ["name"] = first.Name };
            if (!string.IsNullOrWhiteSpace(first.Id)) val["id"] = first.Id;
            return val;
        }
        return field.Required ? new Dictionary<string, string> { ["name"] = size } : null;
    }

    static string ChartTitle(string? genderName, string? brand)
    {
        var g = string.IsNullOrWhiteSpace(genderName) ? "geral" : genderName.Trim();
        var b = string.IsNullOrWhiteSpace(brand) ? "" : " " + brand.Trim();
        var name = $"Guia de tamanhos camisas {g}{b}";
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is ' ' ? c : ' ').ToArray();
        name = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return name.Length <= 60 ? name : name[..60].Trim();
    }

    static IReadOnlyList<MlSizeChartRef> ReadCharts(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } obj) return [];
        if (!obj.TryGetProperty("charts", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray().Select(ReadChartRef).Where(x => x is not null).Cast<MlSizeChartRef>().ToList();
    }

    static MlSizeChartRef? ReadChartRef(JsonElement x)
    {
        var id = Str(x, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        var name = ChartName(x) ?? id;
        return new MlSizeChartRef(id, name, Str(x, "type") ?? "", Str(x, "domain_id") ?? "");
    }

    static MlSizeChartDetail ReadChartDetail(JsonElement x)
    {
        var id = Str(x, "id") ?? "";
        var rows = new List<MlSizeChartRow>();
        if (x.TryGetProperty("rows", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var i = 1;
            foreach (var row in arr.EnumerateArray())
            {
                var rowId = Str(row, "id");
                if (string.IsNullOrWhiteSpace(rowId))
                    rowId = $"{id}:{i}";
                var size = RowSizeName(row);
                rows.Add(new MlSizeChartRow(rowId, size ?? rowId));
                i++;
            }
        }
        return new MlSizeChartDetail(id, ChartName(x) ?? id, Str(x, "type") ?? "", rows);
    }

    static string? ChartName(JsonElement x)
    {
        if (!x.TryGetProperty("names", out var names) || names.ValueKind != JsonValueKind.Object)
            return Str(x, "name");
        if (names.TryGetProperty(Site, out var mlb) && mlb.ValueKind == JsonValueKind.String)
            return mlb.GetString();
        foreach (var p in names.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                return p.Value.GetString();
        }
        return null;
    }

    static string? RowSizeName(JsonElement row)
    {
        if (!row.TryGetProperty("attributes", out var attrs) || attrs.ValueKind != JsonValueKind.Array)
            return null;
        string? fallback = null;
        foreach (var a in attrs.EnumerateArray())
        {
            var id = Str(a, "id") ?? "";
            var name = FirstValueName(a);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (id.Equals("SIZE", StringComparison.OrdinalIgnoreCase)
                || id.Equals("FILTRABLE_SIZE", StringComparison.OrdinalIgnoreCase)
                || id.EndsWith("_SIZE", StringComparison.OrdinalIgnoreCase))
                return name;
            fallback ??= name;
        }
        return fallback;
    }

    static string? FirstValueName(JsonElement attr)
    {
        if (attr.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vals.EnumerateArray())
            {
                var n = Str(v, "name");
                if (!string.IsNullOrWhiteSpace(n)) return n;
            }
        }
        return Str(attr, "value_name") ?? Str(attr, "name");
    }

    static MlCategoryRef? ReadRef(JsonElement x)
    {
        var id = Str(x, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        return new MlCategoryRef(id, Str(x, "name") ?? id);
    }

    static List<MlCategoryRef> ReadRefList(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray().Select(ReadRef).Where(x => x is not null).Cast<MlCategoryRef>().ToList();
    }

    static string? Str(JsonElement x, string name) =>
        x.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    static MlCategoryAttribute? ReadAttribute(JsonElement x)
    {
        var id = Str(x, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        var valueType = Str(x, "value_type") ?? "string";
        var grid = valueType.Equals("grid_id", StringComparison.OrdinalIgnoreCase)
            || valueType.Equals("grid_row_id", StringComparison.OrdinalIgnoreCase)
            || id.Equals("SIZE_GRID_ID", StringComparison.OrdinalIgnoreCase)
            || id.Equals("SIZE_GRID_ROW_ID", StringComparison.OrdinalIgnoreCase);
        var required = HasTag(x, "required") || HasTag(x, "new_required") || grid;
        var hidden = HasTag(x, "hidden");
        var readOnly = HasTag(x, "read_only");
        if (readOnly && !grid) return null;
        if (hidden && !required && !grid) return null;
        var values = ReadAttributeValues(x);
        return new MlCategoryAttribute(
            id,
            Str(x, "name") ?? id,
            valueType,
            Int(x, "value_max_length"),
            required,
            HasTag(x, "catalog_required"),
            HasTag(x, "new_required"),
            HasTag(x, "allow_variations"),
            hidden,
            Recommended: false,
            values,
            HasTag(x, "grid_filter") || HasTag(x, "grid_template_required"));
    }

    static List<MlAttributeValue> ReadAttributeValues(JsonElement x)
    {
        if (!x.TryGetProperty("values", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<MlAttributeValue>();
        foreach (var v in arr.EnumerateArray())
        {
            var vid = Str(v, "id");
            var name = Str(v, "name") ?? vid ?? "";
            if (MercadoLivreItemAttributes.IsNotApplicable(vid, name)) continue;
            if (string.IsNullOrWhiteSpace(vid) && string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new MlAttributeValue(vid ?? "", name));
            if (list.Count >= 250) break;
        }
        return list;
    }

    static List<MlSuggestedAttribute> ReadSuggestedAttributes(JsonElement x)
    {
        if (!x.TryGetProperty("attributes", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<MlSuggestedAttribute>();
        foreach (var v in arr.EnumerateArray())
        {
            var id = Str(v, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var valueId = Str(v, "value_id");
            var valueName = Str(v, "value_name");
            if (MercadoLivreItemAttributes.IsNotApplicable(valueId, valueName)) continue;
            list.Add(new MlSuggestedAttribute(id, valueId, valueName));
        }
        return list;
    }

    async Task<IReadOnlyList<string>> ReadRecommendedIdsAsync(string categoryId, CancellationToken ct)
    {
        var json = await GetJsonAsync($"/categories/{Uri.EscapeDataString(categoryId)}/technical_specs/input", ct);
        if (json is null) return [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectRecommendedIds(json.Value, ids);
        return ids.ToList();
    }

    static void CollectRecommendedIds(JsonElement el, HashSet<string> ids)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in el.EnumerateArray()) CollectRecommendedIds(child, ids);
            return;
        }
        if (el.ValueKind != JsonValueKind.Object) return;
        var id = Str(el, "id");
        if (!string.IsNullOrWhiteSpace(id) && (HasTag(el, "required") || HasTag(el, "catalog_required")))
            ids.Add(id);
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                CollectRecommendedIds(prop.Value, ids);
        }
    }

    static bool HasTag(JsonElement el, string name)
    {
        if (!el.TryGetProperty("tags", out var tags)) return false;
        if (tags.ValueKind == JsonValueKind.Object)
        {
            if (!tags.TryGetProperty(name, out var v)) return false;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        if (tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tags.EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String && t.GetString()!.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    static int? Int(JsonElement x, string name)
    {
        if (!x.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        return int.TryParse(el.GetString(), out var p) ? p : null;
    }
}

public sealed record MlCategoryList(IReadOnlyList<MlCategoryRef> Items, string Source);

public sealed record MlCategorySuggestion(
    string CategoryId,
    string CategoryName,
    string? DomainId,
    string? DomainName,
    IReadOnlyList<MlSuggestedAttribute> Attributes);

public sealed record MlSuggestedAttribute(string Id, string? ValueId, string? ValueName);

public sealed record MlCategorySuggest(IReadOnlyList<MlCategorySuggestion> Items, string Query);

public sealed record MlAttributeValue(string Id, string Name);

public sealed record MlCategoryAttribute(
    string Id,
    string Name,
    string ValueType,
    int? ValueMaxLength,
    bool Required,
    bool CatalogRequired,
    bool NewRequired,
    bool AllowVariations,
    bool Hidden,
    bool Recommended,
    IReadOnlyList<MlAttributeValue> Values,
    bool GridFilter = false);

public sealed record MlCategoryAttributeList(
    string CategoryId,
    IReadOnlyList<MlCategoryAttribute> Items,
    IReadOnlyList<string> RecommendedIds,
    string Source,
    string? DomainId = null,
    bool SizeChartRequired = false);

public sealed record MlSizeChartRef(string Id, string Name, string Type, string DomainId);

public sealed record MlSizeChartList(
    string CategoryId,
    string? DomainId,
    IReadOnlyList<MlSizeChartRef> Items,
    string Source,
    string? Message = null);

public sealed record MlSizeChartRow(string Id, string Size);

public sealed record MlSizeChartDetail(string Id, string Name, string Type, IReadOnlyList<MlSizeChartRow> Rows);

public sealed record MlListingType(string Id, string Name);

public sealed record MlListingTypeList(IReadOnlyList<MlListingType> Items, string Source);

sealed record MlHttp(JsonElement? Json, int Status, string? Error);

sealed record GridField(
    string Id,
    string Name,
    string ValueType,
    bool Required,
    bool MainCandidate,
    bool Clothing,
    bool Body,
    string? DefaultUnit,
    IReadOnlyList<MlAttributeValue> Values,
    string Hierarchy);

sealed record GridSpec(
    string? MainId,
    string? MeasureType,
    IReadOnlyList<GridField> ChartFields,
    IReadOnlyList<GridField> RowFields,
    string? Error)
{
    public static GridSpec Fallback(string? error) => new(
        "SIZE",
        "BODY_MEASURE",
        [],
        [
            new("SIZE", "Tamanho", "string", true, true, false, false, null, [], "ITEM"),
            new("FILTRABLE_SIZE", "Tamanho filtrável", "list", true, false, false, false, null,
            [
                new("13853812", "PP"),
                new("13853813", "P"),
                new("12917795", "M"),
                new("13853814", "G"),
                new("13853815", "GG"),
                new("12917804", "G1"),
                new("12917798", "G2"),
                new("12917801", "G3"),
                new("12917807", "G4"),
                new("12917765", "G5"),
                new("12189459", "1 ano"),
                new("12189461", "2 anos"),
                new("12189463", "3 anos"),
                new("12189465", "4 anos"),
                new("12189469", "6 anos")
            ], "ITEM"),
            new("CHEST_CIRCUMFERENCE_FROM", "Circunferência do peito desde", "number_unit", true, false, false, true, "cm", [], "CHILD_DEPENDENT"),
            new("CHEST_CIRCUMFERENCE_TO", "Circunferência do peito até", "number_unit", true, false, false, true, "cm", [], "CHILD_DEPENDENT")
        ],
        error);
}

public sealed record MlCategoryRef(string Id, string Name);

public sealed record MlCategoryDetail(
    string Id,
    string Name,
    IReadOnlyList<MlCategoryRef> PathFromRoot,
    IReadOnlyList<MlCategoryRef> Children,
    bool Leaf,
    bool ListingAllowed,
    string? CatalogDomain = null);
