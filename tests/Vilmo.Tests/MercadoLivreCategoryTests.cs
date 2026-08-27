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
                      {"id":"BRAND","name":"Marca","value_type":"string","tags":{"required":true,"grid_filter":true},"values":[{"id":"1","name":"Acme"}]},
                      {"id":"COLOR","name":"Cor","value_type":"list","tags":{"required":true},"values":[{"id":"52049","name":"Preto"},{"id":"-1","name":"N/A"}]},
                      {"id":"NOTES","name":"Notas","value_type":"string","tags":{"hidden":true},"values":[]},
                      {"id":"MODEL","name":"Modelo","value_type":"string","tags":{"required":true},"values":[]},
                      {"id":"SIZE_GRID_ID","name":"ID da guia de tamanhos","value_type":"grid_id","tags":{},"values":[]},
                      {"id":"SIZE_GRID_ROW_ID","name":"ID da linha da guia","value_type":"grid_row_id","tags":{"hidden":true,"variation_attribute":true},"values":[]},
                      {"id":"STYLE","name":"Estilo","value_type":"string","tags":{},"values":[]}
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
        Assert.Contains(list.Items, x => x.Id == "SIZE_GRID_ID" && x.Required);
        Assert.Contains(list.Items, x => x.Id == "SIZE_GRID_ROW_ID" && x.Required);
        Assert.Contains(list.Items, x => x.Id == "STYLE" && !x.Required);
        Assert.True(list.SizeChartRequired);
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
    public void Item_attributes_size_grid_sends_value_name_only()
    {
        var rows = MercadoLivreItemAttributes.Build(new Dictionary<string, string>
        {
            ["ml:SIZE_GRID_ID"] = """{"value_id":"26008","value_name":"26008"}""",
            ["ml:SIZE_GRID_ROW_ID"] = """{"value_id":"26008:1","value_name":"26008:1"}""",
            ["ml:SIZE"] = """{"value_name":"P"}"""
        }, null, null);
        var grid = Assert.Single(rows, r => (string)r["id"]! == "SIZE_GRID_ID");
        Assert.Equal("26008", grid["value_name"]);
        Assert.False(grid.ContainsKey("value_id"));
        var row = Assert.Single(rows, r => (string)r["id"]! == "SIZE_GRID_ROW_ID");
        Assert.Equal("26008:1", row["value_name"]);
        Assert.False(row.ContainsKey("value_id"));
        Assert.Contains(rows, r => (string)r["id"]! == "SIZE" && (string)r["value_name"]! == "P");
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

    [Fact]
    public async Task Size_charts_search_returns_guide_and_rows()
    {
        var company = Guid.NewGuid();
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("/categories/MLB107292", StringComparison.Ordinal)
                && !path.Contains("attributes", StringComparison.Ordinal)
                && !path.Contains("charts", StringComparison.Ordinal))
            {
                return Json(200, """{"id":"MLB107292","name":"Camisas","children_categories":[],"path_from_root":[],"settings":{"catalog_domain":"MLB-SHIRTS","listing_allowed":true}}""");
            }
            if (path.Contains("/catalog/charts/search", StringComparison.Ordinal))
                return Json(200, """{"charts":[{"id":"26008","names":{"MLB":"Camisa feminina"},"type":"STANDARD","domain_id":"SHIRTS"}]}""");
            if (path.Contains("/catalog/charts/26008", StringComparison.Ordinal))
            {
                return Json(200, """
                    {"id":"26008","names":{"MLB":"Camisa feminina"},"rows":[
                      {"id":"26008:1","attributes":[{"id":"SIZE","values":[{"name":"P"}]}]}
                    ]}
                    """);
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var cfg = new CompanyMarketplaceConfig
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            IsEnabled = true,
            LinkStatus = "Linked"
        };
        db.CompanyMarketplaceConfigs.Add(cfg);
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "AccessToken",
            ParameterValue = "APP_USR-test", IsSecret = false
        });
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "UserId",
            ParameterValue = "123456", IsSecret = false
        });
        await db.SaveChangesAsync();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListSizeChartsAsync(company, "MLB107292", "339665", "Feminino", "Nike", default);
        Assert.Equal("mercadolivre", list.Source);
        Assert.Equal("26008", Assert.Single(list.Items).Id);
        var detail = await svc.GetSizeChartAsync(company, "26008", default);
        Assert.NotNull(detail);
        Assert.Equal("26008:1", detail!.Rows[0].Id);
        Assert.Equal("P", detail.Rows[0].Size);
    }

    [Fact]
    public async Task Size_charts_retry_without_custom_brand()
    {
        var company = Guid.NewGuid();
        var bodies = new List<string>();
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("/categories/MLB107292", StringComparison.Ordinal)
                && !path.Contains("attributes", StringComparison.Ordinal)
                && !path.Contains("charts", StringComparison.Ordinal))
            {
                return Json(200, """{"id":"MLB107292","name":"Camisas","children_categories":[],"path_from_root":[],"settings":{"catalog_domain":"MLB-SHIRTS","listing_allowed":true}}""");
            }
            if (path.Contains("/catalog/charts/search", StringComparison.Ordinal))
            {
                var body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
                bodies.Add(body);
                if (body.Contains("VilmoTeste", StringComparison.Ordinal))
                    return Json(200, """{"charts":[]}""");
                return Json(200, """{"charts":[{"id":"26008","names":{"MLB":"Guia padrao"},"type":"STANDARD","domain_id":"SHIRTS"}]}""");
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var cfg = new CompanyMarketplaceConfig
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            IsEnabled = true,
            LinkStatus = "Linked"
        };
        db.CompanyMarketplaceConfigs.Add(cfg);
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "AccessToken",
            ParameterValue = "APP_USR-test", IsSecret = false
        });
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "UserId",
            ParameterValue = "123456", IsSecret = false
        });
        await db.SaveChangesAsync();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListSizeChartsAsync(company, "MLB107292", "19159491", "Sem gênero infantil", "VilmoTeste", default);
        Assert.Equal("mercadolivre", list.Source);
        Assert.Equal("26008", Assert.Single(list.Items).Id);
        Assert.Contains(bodies, b => b.Contains("VilmoTeste", StringComparison.Ordinal));
        Assert.Contains(bodies, b => b.Contains("GENDER", StringComparison.Ordinal) && !b.Contains("VilmoTeste", StringComparison.Ordinal));
        Assert.Contains("SHIRTS", bodies[0]);
    }

    [Fact]
    public async Task Size_charts_resolve_seller_from_users_me()
    {
        long? sellerSent = null;
        var company = Guid.NewGuid();
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.Contains("/users/me", StringComparison.Ordinal))
                return Json(200, """{"id":778899}""");
            if (path.Contains("/categories/MLB107292", StringComparison.Ordinal)
                && !path.Contains("charts", StringComparison.Ordinal)
                && !path.Contains("attributes", StringComparison.Ordinal))
            {
                return Json(200, """{"id":"MLB107292","name":"Camisas","children_categories":[],"path_from_root":[],"settings":{"catalog_domain":"MLB-SHIRTS","listing_allowed":true}}""");
            }
            if (path.Contains("/catalog/charts/search", StringComparison.Ordinal))
            {
                var body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
                using var doc = JsonDocument.Parse(body);
                sellerSent = doc.RootElement.GetProperty("seller_id").GetInt64();
                return Json(200, """{"charts":[{"id":"26008","names":{"MLB":"Guia"},"type":"STANDARD","domain_id":"SHIRTS"}]}""");
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var cfg = new CompanyMarketplaceConfig
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            IsEnabled = true,
            LinkStatus = "Linked"
        };
        db.CompanyMarketplaceConfigs.Add(cfg);
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "AccessToken",
            ParameterValue = "APP_USR-test", IsSecret = false
        });
        await db.SaveChangesAsync();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListSizeChartsAsync(company, "MLB107292", "339665", "Feminino", null, default);
        Assert.Equal(778899, sellerSent);
        Assert.Equal("26008", Assert.Single(list.Items).Id);
    }

    [Fact]
    public async Task Size_charts_create_when_search_empty()
    {
        var company = Guid.NewGuid();
        var created = false;
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Get && path.Contains("/categories/MLB107292", StringComparison.Ordinal))
                return Json(200, """{"id":"MLB107292","name":"Camisas","children_categories":[],"path_from_root":[],"settings":{"catalog_domain":"MLB-SHIRTS","listing_allowed":true}}""");
            if (req.Method == HttpMethod.Post && path.EndsWith("/catalog/charts/search", StringComparison.Ordinal))
                return Json(200, """{"charts":[]}""");
            if (req.Method == HttpMethod.Post && path.Contains("/technical_specs", StringComparison.Ordinal))
            {
                return Json(200, """
                    {"input":{"groups":[{"components":[{"attributes":[
                      {"id":"GENDER","tags":["required","grid_template_required"],"hierarchy":"PARENT_PK","values":[{"id":"339665","name":"Feminino"}]},
                      {"id":"SIZE","tags":["main_attribute_candidate"],"hierarchy":"ITEM","value_type":"string"},
                      {"id":"CHEST_CIRCUMFERENCE_FROM","name":"Peito desde","value_type":"number_unit","tags":["BODY_MEASURE","required"],"default_unit_id":"cm","hierarchy":"CHILD_DEPENDENT"},
                      {"id":"CHEST_CIRCUMFERENCE_TO","name":"Peito até","value_type":"number_unit","tags":["BODY_MEASURE","required"],"default_unit_id":"cm","hierarchy":"CHILD_DEPENDENT"}
                    ]}]}]}}
                    """);
            }
            if (req.Method == HttpMethod.Post && path.EndsWith("/catalog/charts", StringComparison.Ordinal))
            {
                created = true;
                var body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
                Assert.Contains("SHIRTS", body, StringComparison.Ordinal);
                Assert.Contains("SIZE", body, StringComparison.Ordinal);
                Assert.Contains("BODY_MEASURE", body, StringComparison.Ordinal);
                Assert.Contains("FILTRABLE_SIZE", body, StringComparison.Ordinal);
                Assert.Contains("CHEST_CIRCUMFERENCE_FROM", body, StringComparison.Ordinal);
                Assert.DoesNotContain("MLB-SHIRTS", body, StringComparison.Ordinal);
                return Json(201, """{"id":"998877","names":{"MLB":"Guia de tamanhos camisas Feminino"}}""");
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var cfg = new CompanyMarketplaceConfig
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            IsEnabled = true,
            LinkStatus = "Linked"
        };
        db.CompanyMarketplaceConfigs.Add(cfg);
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "AccessToken",
            ParameterValue = "APP_USR-test", IsSecret = false
        });
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "UserId",
            ParameterValue = "123456", IsSecret = false
        });
        await db.SaveChangesAsync();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListSizeChartsAsync(company, "MLB107292", "339665", "Feminino", "VilmoTeste", default);
        Assert.True(created);
        Assert.Equal("mercadolivre", list.Source);
        Assert.Equal("998877", Assert.Single(list.Items).Id);
    }

    [Fact]
    public async Task Size_charts_create_uses_fallback_chest_when_spec_fails()
    {
        var company = Guid.NewGuid();
        string? createdBody = null;
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Get && path.Contains("/categories/MLB107292", StringComparison.Ordinal))
                return Json(200, """{"id":"MLB107292","name":"Camisas","children_categories":[],"path_from_root":[],"settings":{"catalog_domain":"MLB-SHIRTS","listing_allowed":true}}""");
            if (req.Method == HttpMethod.Post && path.EndsWith("/catalog/charts/search", StringComparison.Ordinal))
                return Json(200, """{"charts":[]}""");
            if (req.Method == HttpMethod.Post && path.Contains("/technical_specs", StringComparison.Ordinal))
                return Json(400, """{"message":"Chart validation errors found","cause":[{"message":"missing FOOT_LENGTH","cell":{"attribute_id":"FOOT_LENGTH"}}]}""");
            if (req.Method == HttpMethod.Post && path.EndsWith("/catalog/charts", StringComparison.Ordinal))
            {
                createdBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
                return Json(201, """{"id":"112233","names":{"MLB":"Guia"}}""");
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var cfg = new CompanyMarketplaceConfig
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            IsEnabled = true,
            LinkStatus = "Linked"
        };
        db.CompanyMarketplaceConfigs.Add(cfg);
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "AccessToken",
            ParameterValue = "APP_USR-test", IsSecret = false
        });
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "UserId",
            ParameterValue = "123456", IsSecret = false
        });
        await db.SaveChangesAsync();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListSizeChartsAsync(company, "MLB107292", "339665", "Feminino", "VilmoTeste", default);
        Assert.Equal("112233", Assert.Single(list.Items).Id);
        Assert.Contains("BODY_MEASURE", createdBody, StringComparison.Ordinal);
        Assert.Contains("FILTRABLE_SIZE", createdBody, StringComparison.Ordinal);
        Assert.Contains("CHEST_CIRCUMFERENCE_FROM", createdBody, StringComparison.Ordinal);
        Assert.Contains("PP", createdBody, StringComparison.Ordinal);
        Assert.Contains("13853812", createdBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Size_charts_create_child_gender_uses_g_sizes()
    {
        var company = Guid.NewGuid();
        string? createdBody = null;
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Get && path.Contains("/categories/MLB107292", StringComparison.Ordinal))
                return Json(200, """{"id":"MLB107292","name":"Camisas","children_categories":[],"path_from_root":[],"settings":{"catalog_domain":"MLB-SHIRTS","listing_allowed":true}}""");
            if (req.Method == HttpMethod.Post && path.EndsWith("/catalog/charts/search", StringComparison.Ordinal))
                return Json(200, """{"charts":[]}""");
            if (req.Method == HttpMethod.Post && path.Contains("/technical_specs", StringComparison.Ordinal))
                return Json(200, "{}");
            if (req.Method == HttpMethod.Post && path.EndsWith("/catalog/charts", StringComparison.Ordinal))
            {
                createdBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
                return Json(201, """{"id":"445566","names":{"MLB":"Guia"}}""");
            }
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var cfg = new CompanyMarketplaceConfig
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            IsEnabled = true,
            LinkStatus = "Linked"
        };
        db.CompanyMarketplaceConfigs.Add(cfg);
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "AccessToken",
            ParameterValue = "APP_USR-test", IsSecret = false
        });
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "UserId",
            ParameterValue = "123456", IsSecret = false
        });
        await db.SaveChangesAsync();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListSizeChartsAsync(company, "MLB107292", "19159491", "Sem gênero infantil", "VilmoTeste", default);
        Assert.Equal("445566", Assert.Single(list.Items).Id);
        Assert.Contains("\"1\"", createdBody, StringComparison.Ordinal);
        Assert.Contains("1 ano", createdBody, StringComparison.Ordinal);
        Assert.Contains("12189459", createdBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PP\"", createdBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Size_charts_without_token_do_not_call_search()
    {
        var handler = new StubHandler();
        handler.Impl = req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/categories/MLB107292", StringComparison.Ordinal))
                return Json(200, """{"id":"MLB107292","name":"Camisas","children_categories":[],"path_from_root":[],"settings":{"catalog_domain":"MLB-SHIRTS","listing_allowed":true}}""");
            if (req.RequestUri!.PathAndQuery.Contains("/catalog/charts", StringComparison.Ordinal))
                return Json(500, """{"error":"should-not-call"}""");
            return Json(404, "{}");
        };
        await using var db = Sqlite();
        var svc = new MercadoLivreCategoryService(db, new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()));
        var list = await svc.ListSizeChartsAsync(Guid.NewGuid(), "MLB107292", "339665", "Feminino", null, default);
        Assert.Equal("needs_token", list.Source);
        Assert.Empty(list.Items);
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
