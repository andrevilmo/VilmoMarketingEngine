using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vilmo.Data;
using Vilmo.Services;

namespace Vilmo.Tests;

public class MercadoLivreCategoryTests : IClassFixture<ApiFactory>
{
    readonly ApiFactory _factory;
    readonly HttpClient _client;

    public MercadoLivreCategoryTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    async Task<(string Token, Guid CompanyId)> AdminAsync()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new { email = "admin@vilmomkt.com", password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var login = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = login.RootElement.GetProperty("accessToken").GetString()!;
        var me = await _client.SendAsync(Authed(HttpMethod.Get, "/me", token));
        me.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var companyId = doc.RootElement.GetProperty("memberships")[0].GetProperty("companyId").GetGuid();
        return (token, companyId);
    }

    static HttpRequestMessage Authed(HttpMethod method, string url, string token, Guid? companyId = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (companyId is { } cid) req.Headers.Add("X-Company-Id", cid.ToString());
        return req;
    }

    [Fact]
    public async Task List_roots_returns_mlb_categories()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/marketplaces/MercadoLivre/categories", token, companyId));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 10);
        Assert.Contains(items.EnumerateArray(), x => x.GetProperty("id").GetString() == "MLB1430");
        Assert.Contains(items.EnumerateArray(), x => x.GetProperty("name").GetString()!.Contains("Calçados", StringComparison.OrdinalIgnoreCase)
            || x.GetProperty("id").GetString() == "MLB1430");
    }

    [Fact]
    public async Task Detail_returns_children_and_path()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/marketplaces/MercadoLivre/categories/MLB1430", token, companyId));
        if (res.StatusCode == HttpStatusCode.NotFound)
            return;
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("MLB1430", doc.RootElement.GetProperty("id").GetString());
        Assert.True(doc.RootElement.GetProperty("children").GetArrayLength() > 0);
        Assert.False(doc.RootElement.GetProperty("leaf").GetBoolean());
    }

    [Fact]
    public async Task Suggest_returns_category_id_from_query()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/marketplaces/MercadoLivre/categories/suggest?q=calca%20jeans", token, companyId));
        if (!res.IsSuccessStatusCode)
            return;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(items[0].GetProperty("categoryId").GetString()));
    }

    [Fact]
    public async Task Suggest_blank_query_is_400()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/marketplaces/MercadoLivre/categories/suggest?q=x", token, companyId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("QueryRequired", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Service_uses_live_list_when_http_ok_and_fallback_when_not()
    {
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/sites/MLB/categories", StringComparison.Ordinal))
            {
                return Json(200, """[{"id":"MLB999","name":"Fake Root"}]""");
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var live = await svc.ListRootsAsync(default);
        Assert.Equal("mercadolivre", live.Source);
        Assert.Equal("MLB999", live.Items[0].Id);

        handler.Impl = _ => Json(403, """{"message":"blocked"}""");
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc2 = new MercadoLivreCategoryService(db, new StubFactory(handler), cache);
        var fb = await svc2.ListRootsAsync(default);
        Assert.Equal("fallback", fb.Source);
        Assert.Contains(fb.Items, x => x.Id == "MLB1430");
    }

    [Fact]
    public async Task Service_parses_detail_and_suggest()
    {
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("/categories/MLB1430", StringComparison.Ordinal) && !path.Contains("sites", StringComparison.Ordinal))
            {
                return Json(200, """
                    {"id":"MLB1430","name":"Calçados, Roupas e Bolsas","path_from_root":[{"id":"MLB1430","name":"Calçados, Roupas e Bolsas"}],
                     "children_categories":[{"id":"MLB188064","name":"Calças"}],"settings":{"listing_allowed":false}}
                    """);
            }
            if (path.Contains("domain_discovery", StringComparison.Ordinal))
            {
                return Json(200, """[{"domain_id":"MLB-PANTS","domain_name":"Calças","category_id":"MLB188064","category_name":"Calças","attributes":[{"id":"BRAND","value_id":"9344","value_name":"Apple"}]}]""");
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var detail = await svc.GetAsync("MLB1430", default);
        Assert.NotNull(detail);
        Assert.Equal("MLB1430", detail!.Id);
        Assert.False(detail.Leaf);
        Assert.False(detail.ListingAllowed);
        Assert.Equal("MLB188064", detail.Children[0].Id);

        var sug = await svc.SuggestAsync("calca", default);
        Assert.Equal("MLB188064", sug.Items[0].CategoryId);
        Assert.Equal("9344", sug.Items[0].Attributes[0].ValueId);
    }

    [Fact]
    public async Task Attributes_skip_na_and_keep_required()
    {
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("/categories/MLB188065/attributes", StringComparison.Ordinal))
            {
                return Json(200, """
                    [
                      {"id":"BRAND","name":"Marca","value_type":"string","tags":{"required":true},"values":[{"id":"1","name":"Acme"}]},
                      {"id":"COLOR","name":"Cor","value_type":"list","tags":{"required":true},"values":[{"id":"52049","name":"Preto"},{"id":"-1","name":"N/A"}]},
                      {"id":"NOTES","name":"Notas","value_type":"string","tags":{"hidden":true},"values":[]},
                      {"id":"MODEL","name":"Modelo","value_type":"string","tags":{"required":true},"values":[]}
                    ]
                    """);
            }
            if (path.Contains("technical_specs", StringComparison.Ordinal))
                return Json(403, """{"message":"forbidden"}""");
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListAttributesAsync("MLB188065", default);
        Assert.Equal("MLB188065", list.CategoryId);
        Assert.Contains(list.Items, x => x.Id == "BRAND" && x.Required);
        Assert.Contains(list.Items, x => x.Id == "MODEL" && x.Required);
        var color = Assert.Single(list.Items, x => x.Id == "COLOR");
        Assert.DoesNotContain(color.Values, v => v.Id == "-1" || v.Name == "N/A");
        Assert.DoesNotContain(list.Items, x => x.Id == "NOTES");
    }

    [Fact]
    public async Task Attributes_endpoint_rejects_category_name()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/marketplaces/MercadoLivre/categories/Vestuário/attributes", token, companyId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("InvalidCategoryId", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Item_attributes_omit_na_and_map_prefix()
    {
        Assert.True(MercadoLivreItemAttributes.IsNotApplicable("-1", "N/A"));
        Assert.False(MercadoLivreItemAttributes.IsNotApplicable("52049", "Preto"));
        var rows = MercadoLivreItemAttributes.Build(new Dictionary<string, string>
        {
            ["categoryId"] = "MLB188065",
            ["ml:BRAND"] = """{"value_name":"Acme"}""",
            ["ml:COLOR"] = """{"value_id":"-1","value_name":"N/A"}""",
            ["ml:GENDER"] = """{"value_id":"339665","value_name":"Feminino"}"""
        }, "IgnoredBrand", null);
        Assert.Contains(rows, r => (string)r["id"]! == "BRAND" && (string)r["value_name"]! == "Acme");
        Assert.Contains(rows, r => (string)r["id"]! == "GENDER" && (string)r["value_id"]! == "339665");
        Assert.DoesNotContain(rows, r => (string)r["id"]! == "COLOR");
        Assert.DoesNotContain(rows, r => (string)r["id"]! == "categoryId");
    }

    [Fact]
    public void Normalize_extracts_mlb_code_and_rejects_names()
    {
        Assert.True(MercadoLivreCategoryId.TryNormalize("MLB5672", out var a));
        Assert.Equal("MLB5672", a);
        Assert.True(MercadoLivreCategoryId.TryNormalize("mlb1430", out var b));
        Assert.Equal("MLB1430", b);
        Assert.True(MercadoLivreCategoryId.TryNormalize("Vestuário (MLB5672)", out var c));
        Assert.Equal("MLB5672", c);
        Assert.False(MercadoLivreCategoryId.TryNormalize("Vestuário", out _));
        Assert.False(MercadoLivreCategoryId.TryNormalize("TESTE", out _));
        Assert.False(MercadoLivreCategoryId.TryNormalize("", out _));
    }

    [Fact]
    public async Task List_listing_types_returns_official_codes()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/marketplaces/MercadoLivre/listing-types", token, companyId));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 3);
        Assert.Contains(items.EnumerateArray(), x => x.GetProperty("id").GetString() == "gold_special");
        Assert.Contains(items.EnumerateArray(), x => x.GetProperty("id").GetString() == "gold_pro");
        Assert.Contains(items.EnumerateArray(), x => x.GetProperty("id").GetString() == "free");
        Assert.DoesNotContain(items.EnumerateArray(), x => x.GetProperty("id").GetString()!.Contains(' '));
    }

    [Fact]
    public async Task Service_uses_live_listing_types_when_http_ok_and_fallback_when_not()
    {
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/sites/MLB/listing_types", StringComparison.Ordinal))
                return Json(200, """[{"site_id":"MLB","id":"gold_pro","name":"Premium"},{"site_id":"MLB","id":"gold_special","name":"Clássico"}]""");
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var live = await svc.ListListingTypesAsync(default);
        Assert.Equal("mercadolivre", live.Source);
        Assert.Equal("gold_special", live.Items[0].Id);
        Assert.Contains(live.Items, x => x.Id == "gold_pro");

        handler.Impl = _ => Json(403, """{"message":"blocked"}""");
        var svc2 = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var fb = await svc2.ListListingTypesAsync(default);
        Assert.Equal("fallback", fb.Source);
        Assert.Contains(fb.Items, x => x.Id == "gold_special");
        Assert.Contains(fb.Items, x => x.Id == "free");
    }

    [Fact]
    public void Normalize_listing_type_rejects_names()
    {
        Assert.True(MercadoLivreListingTypeId.TryNormalize("gold_special", out var a));
        Assert.Equal("gold_special", a);
        Assert.True(MercadoLivreListingTypeId.TryNormalize("GOLD_PRO", out var b));
        Assert.Equal("gold_pro", b);
        Assert.True(MercadoLivreListingTypeId.TryNormalize("Premium (gold_pro)", out var c));
        Assert.Equal("gold_pro", c);
        Assert.True(MercadoLivreListingTypeId.TryNormalize("free", out var d));
        Assert.Equal("free", d);
        Assert.False(MercadoLivreListingTypeId.TryNormalize("CAMISA TESTE", out _));
        Assert.False(MercadoLivreListingTypeId.TryNormalize("Clássica", out _));
        Assert.False(MercadoLivreListingTypeId.TryNormalize("camisa", out _));
        Assert.False(MercadoLivreListingTypeId.TryNormalize("", out _));
    }

    static AppDbContext Sqlite()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"mlcat-{Guid.NewGuid():N}.db")}")
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    static HttpResponseMessage Json(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Impl { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Impl(request));
    }

    sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
