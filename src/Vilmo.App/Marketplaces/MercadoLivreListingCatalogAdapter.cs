using System.Net.Http.Headers;
using System.Text.Json;
using Vilmo.Marketplaces;

namespace Vilmo.Marketplaces;

public sealed class MercadoLivreListingCatalogAdapter(IHttpClientFactory httpFactory) : IMarketplaceListingCatalogAdapter
{
    public const string Code = "MercadoLivre";
    public string MarketplaceCode => Code;

    const int SearchPageSize = 50;
    const int MultiGetSize = 20;

    public async Task<IReadOnlyList<RemoteMarketplaceAd>> ListAdsAsync(MarketplaceCatalogContext ctx, CancellationToken ct)
    {
        var sellerId = ctx.SellerId;
        if (string.IsNullOrWhiteSpace(sellerId))
        {
            var me = await GetAsync(ctx, "/users/me", ct);
            EnsureOk(me, "/users/me");
            sellerId = ReadId(me.Json)?.ToString();
        }
        if (string.IsNullOrWhiteSpace(sellerId))
            throw new InvalidOperationException("SellerIdRequired");

        var ids = await ListItemIdsAsync(ctx, sellerId, ct);
        var ads = new List<RemoteMarketplaceAd>(ids.Count);
        for (var i = 0; i < ids.Count; i += MultiGetSize)
        {
            var batch = ids.Skip(i).Take(MultiGetSize).ToList();
            var items = await GetItemsAsync(ctx, batch, ct);
            foreach (var item in items)
            {
                var parsed = ParseItem(item);
                if (parsed is not null) ads.Add(parsed);
            }
        }
        return ads;
    }

    async Task<List<string>> ListItemIdsAsync(MarketplaceCatalogContext ctx, string sellerId, CancellationToken ct)
    {
        var ids = new List<string>();
        string? scrollId = null;
        var usedScan = true;
        for (var page = 0; page < 40; page++)
        {
            var path = scrollId is null
                ? $"/users/{Uri.EscapeDataString(sellerId)}/items/search?search_type=scan&limit={SearchPageSize}"
                : $"/users/{Uri.EscapeDataString(sellerId)}/items/search?search_type=scan&scroll_id={Uri.EscapeDataString(scrollId)}&limit={SearchPageSize}";
            var got = await GetAsync(ctx, path, ct);
            if (page == 0 && scrollId is null && !got.Ok && !IsAuthFailure(got.Status))
            {
                usedScan = false;
                break;
            }
            EnsureOk(got, path);
            var pageIds = ReadResultIds(got.Json!.Value);
            if (pageIds.Count == 0) break;
            ids.AddRange(pageIds);
            scrollId = Str(got.Json.Value, "scroll_id");
            if (string.IsNullOrWhiteSpace(scrollId)) break;
        }
        if (!usedScan || ids.Count == 0)
        {
            ids.Clear();
            for (var offset = 0; offset < 2000; offset += SearchPageSize)
            {
                var path = $"/users/{Uri.EscapeDataString(sellerId)}/items/search?offset={offset}&limit={SearchPageSize}";
                var got = await GetAsync(ctx, path, ct);
                EnsureOk(got, path);
                var pageIds = ReadResultIds(got.Json!.Value);
                if (pageIds.Count == 0) break;
                ids.AddRange(pageIds);
                var total = got.Json.Value.TryGetProperty("paging", out var paging) && paging.TryGetProperty("total", out var t) && t.TryGetInt32(out var n)
                    ? n : ids.Count;
                if (offset + SearchPageSize >= total) break;
            }
        }
        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    async Task<List<JsonElement>> GetItemsAsync(MarketplaceCatalogContext ctx, IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var path = $"/items?ids={string.Join(",", ids.Select(Uri.EscapeDataString))}";
        var got = await GetAsync(ctx, path, ct);
        EnsureOk(got, path);
        var list = new List<JsonElement>();
        var json = got.Json!.Value;
        if (json.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in json.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty("body", out var body)
                    && body.ValueKind == JsonValueKind.Object)
                    list.Add(body.Clone());
                else if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty("id", out _))
                    list.Add(row.Clone());
            }
        }
        else if (json.ValueKind == JsonValueKind.Object)
            list.Add(json.Clone());
        return list;
    }

    public static RemoteMarketplaceAd? ParseItem(JsonElement item)
    {
        var id = Str(item, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        var status = Str(item, "status");
        if (status is "closed" or "deleted" or "inactive") return null;
        var pictures = ReadPictures(item);
        var snapshot = item.GetRawText();
        return new RemoteMarketplaceAd(
            id,
            Str(item, "title") ?? id,
            Dec(item, "price"),
            Dec(item, "available_quantity"),
            status,
            Str(item, "permalink"),
            FirstNonEmpty(Str(item, "secure_thumbnail"), Str(item, "thumbnail")),
            FirstNonEmpty(Str(item, "seller_custom_field"), Str(item, "seller_sku"), Attr(item, "SELLER_SKU"), VariationCustomField(item)),
            FirstNonEmpty(Attr(item, "GTIN"), Attr(item, "EAN"), Attr(item, "UPC")),
            Str(item, "category_id"),
            Str(item, "listing_type_id"),
            pictures,
            Str(item, "buying_mode"),
            ShippingMode(item),
            snapshot);
    }

    static List<string> ReadResultIds(JsonElement json)
    {
        var ids = new List<string>();
        if (!json.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return ids;
        foreach (var x in results.EnumerateArray())
        {
            var id = x.ValueKind == JsonValueKind.String ? x.GetString() : Str(x, "id");
            if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
        }
        return ids;
    }

    static IReadOnlyList<string> ReadPictures(JsonElement item)
    {
        var urls = new List<string>();
        if (!item.TryGetProperty("pictures", out var pics) || pics.ValueKind != JsonValueKind.Array)
            return urls;
        foreach (var p in pics.EnumerateArray())
        {
            var url = FirstNonEmpty(Str(p, "secure_url"), Str(p, "url"));
            if (!string.IsNullOrWhiteSpace(url)) urls.Add(url);
        }
        return urls;
    }

    static string? Attr(JsonElement item, string id)
    {
        if (!item.TryGetProperty("attributes", out var attrs) || attrs.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var a in attrs.EnumerateArray())
        {
            if (!id.Equals(Str(a, "id"), StringComparison.OrdinalIgnoreCase)) continue;
            return FirstNonEmpty(Str(a, "value_name"), Str(a, "value_id"));
        }
        return null;
    }

    static string? VariationCustomField(JsonElement item)
    {
        if (!item.TryGetProperty("variations", out var vars) || vars.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var v in vars.EnumerateArray())
        {
            var sku = FirstNonEmpty(Str(v, "seller_custom_field"), Str(v, "seller_sku"));
            if (!string.IsNullOrWhiteSpace(sku)) return sku;
        }
        return null;
    }

    static string? ShippingMode(JsonElement item)
    {
        if (item.TryGetProperty("shipping", out var ship) && ship.ValueKind == JsonValueKind.Object)
            return Str(ship, "mode");
        return null;
    }

    sealed record MlGet(int Status, string Raw, JsonElement? Json)
    {
        public bool Ok => Status is >= 200 and < 300 && Json is not null;
    }

    async Task<MlGet> GetAsync(MarketplaceCatalogContext ctx, string path, CancellationToken ct)
    {
        var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{ctx.BaseUrl.TrimEnd('/')}{path}";
        var client = httpFactory.CreateClient("marketplace");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var resp = await client.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        JsonElement? json = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "null" : raw);
            json = doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? doc.RootElement.Clone()
                : null;
        }
        catch (JsonException)
        {
            json = null;
        }
        return new MlGet((int)resp.StatusCode, raw, json);
    }

    static void EnsureOk(MlGet got, string path)
    {
        if (got.Ok) return;
        throw new MarketplaceHttpException(Code, got.Status, path, got.Raw);
    }

    static bool IsAuthFailure(int status) => status is 401 or 403;

    static long? ReadId(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } obj) return null;
        if (!obj.TryGetProperty("id", out var id)) return null;
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var n)) return n;
        return long.TryParse(id.GetString(), out var s) ? s : null;
    }

    static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;

    static decimal? Dec(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        return decimal.TryParse(v.GetString(), out var s) ? s : null;
    }

    static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
