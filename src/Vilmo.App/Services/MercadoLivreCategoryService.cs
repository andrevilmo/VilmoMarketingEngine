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
            return new MlSizeChartList(id, domain, [], "no_domain");
        var creds = await ReadSellerAsync(companyId, ct);
        if (creds is null)
            return new MlSizeChartList(id, domain, [], "needs_token");
        var body = new Dictionary<string, object?>
        {
            ["domain_id"] = domain,
            ["site_id"] = Site,
            ["seller_id"] = creds.Value.SellerId,
            ["attributes"] = SizeChartFilters(genderId, genderName, brand)
        };
        var json = await SendJsonAsync(HttpMethod.Post, "/catalog/charts/search", ct, creds.Value.Token, creds.Value.SellerId, body);
        var charts = ReadCharts(json);
        if (charts.Count == 0 && domain.StartsWith($"{Site}-", StringComparison.OrdinalIgnoreCase))
        {
            body["domain_id"] = domain[(Site.Length + 1)..];
            json = await SendJsonAsync(HttpMethod.Post, "/catalog/charts/search", ct, creds.Value.Token, creds.Value.SellerId, body);
            charts = ReadCharts(json);
        }
        return new MlSizeChartList(id, domain, charts, charts.Count > 0 ? "mercadolivre" : "empty");
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
        await SendJsonAsync(HttpMethod.Get, path, ct);

    async Task<JsonElement?> SendJsonAsync(
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
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "null" : raw);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
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
        return (token, sellerId);
    }

    static List<object> SizeChartFilters(string? genderId, string? genderName, string? brand)
    {
        var filters = new List<object>();
        if (!string.IsNullOrWhiteSpace(genderId) || !string.IsNullOrWhiteSpace(genderName))
        {
            var val = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(genderId)) val["id"] = genderId.Trim();
            if (!string.IsNullOrWhiteSpace(genderName)) val["name"] = genderName.Trim();
            filters.Add(new { id = "GENDER", values = new[] { val } });
        }
        if (!string.IsNullOrWhiteSpace(brand))
            filters.Add(new { id = "BRAND", values = new[] { new { name = brand.Trim() } } });
        return filters;
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
    string Source);

public sealed record MlSizeChartRow(string Id, string Size);

public sealed record MlSizeChartDetail(string Id, string Name, string Type, IReadOnlyList<MlSizeChartRow> Rows);

public sealed record MlListingType(string Id, string Name);

public sealed record MlListingTypeList(IReadOnlyList<MlListingType> Items, string Source);

public sealed record MlCategoryRef(string Id, string Name);

public sealed record MlCategoryDetail(
    string Id,
    string Name,
    IReadOnlyList<MlCategoryRef> PathFromRoot,
    IReadOnlyList<MlCategoryRef> Children,
    bool Leaf,
    bool ListingAllowed,
    string? CatalogDomain = null);
