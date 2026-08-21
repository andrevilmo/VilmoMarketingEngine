using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vilmo.Data;

namespace Vilmo.Services;

public sealed class MercadoLivreCategoryService(AppDbContext db, IHttpClientFactory httpFactory, IMemoryCache cache)
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
        var detail = new MlCategoryDetail(
            el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? id : id,
            el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? id : id,
            path,
            children,
            children.Count == 0,
            listingAllowed
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
            Str(x, "domain_name")
        )).Where(x => !string.IsNullOrWhiteSpace(x.CategoryId)).ToList();
        return new MlCategorySuggest(items, query);
    }

    async Task<string> BaseUrlAsync(CancellationToken ct)
    {
        var url = await db.Marketplaces.AsNoTracking()
            .Where(m => m.Code == MarketplaceCode)
            .Select(m => m.BaseUrl)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(url) ? DefaultBase : url.TrimEnd('/');
    }

    async Task<JsonElement?> GetJsonAsync(string path, CancellationToken ct)
    {
        var url = $"{await BaseUrlAsync(ct)}{path}";
        try
        {
            var client = httpFactory.CreateClient("marketplace");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
}

public sealed record MlCategoryList(IReadOnlyList<MlCategoryRef> Items, string Source);

public sealed record MlCategorySuggestion(string CategoryId, string CategoryName, string? DomainId, string? DomainName);

public sealed record MlCategorySuggest(IReadOnlyList<MlCategorySuggestion> Items, string Query);

public sealed record MlCategoryRef(string Id, string Name);

public sealed record MlCategoryDetail(
    string Id,
    string Name,
    IReadOnlyList<MlCategoryRef> PathFromRoot,
    IReadOnlyList<MlCategoryRef> Children,
    bool Leaf,
    bool ListingAllowed);
