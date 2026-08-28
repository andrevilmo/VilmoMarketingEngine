using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Marketplaces;
using Vilmo.Security;
using Vilmo.Services;

namespace Vilmo.Tests;

public class ListingImportMatchTests
{
    static RemoteMarketplaceAd Ad(string id, string? sku = null, string? gtin = null) =>
        new(id, "Titulo " + id, 49.9m, 4m, "active", "https://ml.test/" + id, null, sku, gtin,
            "MLB123", "gold_special", ["https://example.com/p.jpg"], "buy_it_now", "me2", "{}");

    static Product Product(string sku, string? ean = null) =>
        new() { Id = Guid.NewGuid(), Sku = sku, Name = sku, Ean = ean, SalePrice = 10m };

    [Fact]
    public void Classify_already_linked_by_remote_id()
    {
        var listing = new Listing { Sku = "CAMISETA-001", RemoteId = "MLB1", AdvertisementId = Guid.NewGuid() };
        var r = ListingImportService.Classify(Ad("MLB1", "OUTRO"), [Product("CAMISETA-001")], [], [listing]);
        Assert.Equal(RemoteAdMatchStatuses.AlreadyLinked, r.Status);
        Assert.Equal("CAMISETA-001", r.Sku);
        Assert.Equal("remote_id", r.Reason);
    }

    [Fact]
    public void Classify_suggests_seller_custom_field_sku()
    {
        var r = ListingImportService.Classify(Ad("MLB2", "CAMISETA-001"), [Product("CAMISETA-001")], [], []);
        Assert.Equal(RemoteAdMatchStatuses.Suggested, r.Status);
        Assert.Equal("CAMISETA-001", r.Sku);
        Assert.Equal("seller_custom_field", r.Reason);
    }

    [Fact]
    public void Classify_suggests_unique_ean()
    {
        var r = ListingImportService.Classify(Ad("MLB3", gtin: "7891234567895"), [Product("CAMISETA-001", "7891234567895")], [], []);
        Assert.Equal(RemoteAdMatchStatuses.Suggested, r.Status);
        Assert.Equal("CAMISETA-001", r.Sku);
        Assert.Equal("ean", r.Reason);
    }

    [Fact]
    public void Classify_unmatched_when_ean_is_ambiguous()
    {
        var r = ListingImportService.Classify(
            Ad("MLB4", gtin: "7891234567895"),
            [Product("A", "7891234567895"), Product("B", "789-1234567895")],
            [], []);
        Assert.Equal(RemoteAdMatchStatuses.Unmatched, r.Status);
        Assert.Equal("ean_ambiguous", r.Reason);
    }

    [Fact]
    public void Classify_unmatched_without_sku_or_ean()
    {
        var r = ListingImportService.Classify(Ad("MLB5"), [Product("CAMISETA-001")], [], []);
        Assert.Equal(RemoteAdMatchStatuses.Unmatched, r.Status);
        Assert.Null(r.Sku);
    }

    [Fact]
    public void ParseItem_skips_closed_deleted_inactive()
    {
        Assert.Null(MercadoLivreListingCatalogAdapter.ParseItem(Json("""{"id":"MLB-C","status":"closed","title":"x"}""")));
        Assert.Null(MercadoLivreListingCatalogAdapter.ParseItem(Json("""{"id":"MLB-D","status":"deleted","title":"x"}""")));
        Assert.Null(MercadoLivreListingCatalogAdapter.ParseItem(Json("""{"id":"MLB-I","status":"inactive","title":"x"}""")));
    }

    [Fact]
    public void ParseItem_reads_sku_gtin_and_pictures()
    {
        var item = MercadoLivreListingCatalogAdapter.ParseItem(Json("""
            {
              "id":"MLB123",
              "title":"Camiseta",
              "status":"active",
              "price":89.9,
              "available_quantity":7,
              "permalink":"https://ml.test/MLB123",
              "seller_custom_field":"CAMISETA-001",
              "category_id":"MLB1234",
              "listing_type_id":"gold_special",
              "buying_mode":"buy_it_now",
              "shipping":{"mode":"me2"},
              "pictures":[{"secure_url":"https://example.com/a.jpg"}],
              "attributes":[{"id":"GTIN","value_name":"7891234567895"}]
            }
            """));
        Assert.NotNull(item);
        Assert.Equal("MLB123", item!.RemoteId);
        Assert.Equal("CAMISETA-001", item.SellerCustomField);
        Assert.Equal("7891234567895", item.Gtin);
        Assert.Equal("gold_special", item.ListingTypeId);
        Assert.Equal("me2", item.ShippingMode);
        Assert.Equal(["https://example.com/a.jpg"], item.PictureUrls);
        Assert.Equal(7m, item.Quantity);
    }

    static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();
}

public class ListingImportApiTests : IClassFixture<ApiFactory>
{
    readonly HttpClient _client;

    public ListingImportApiTests(ApiFactory factory) => _client = factory.CreateClient();

    async Task<(string Token, Guid CompanyId)> AdminAsync()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new { email = "admin@vilmomkt.com", password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var login = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = login.RootElement.GetProperty("accessToken").GetString()!;
        var me = await _client.SendAsync(Authed(HttpMethod.Get, "/me", token));
        me.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        return (token, doc.RootElement.GetProperty("memberships")[0].GetProperty("companyId").GetGuid());
    }

    static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null, Guid? companyId = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (companyId is { } cid) req.Headers.Add("X-Company-Id", cid.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }
        return req;
    }

    [Fact]
    public async Task Import_without_oauth_returns_400()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements/imports/MercadoLivre", token, new { }, companyId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("MarketplaceNotLinked", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Import_unknown_marketplace_returns_400()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements/imports/Shopee", token, new { }, companyId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("ImportNotSupported", await res.Content.ReadAsStringAsync());
    }
}

public class ListingImportFlowTests : IClassFixture<ApiFactory>
{
    readonly ApiFactory _factory;
    readonly HttpClient _client;

    public ListingImportFlowTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    async Task<(string Token, Guid CompanyId, Guid UserId)> AdminAsync()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new { email = "admin@vilmomkt.com", password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var login = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = login.RootElement.GetProperty("accessToken").GetString()!;
        var me = await _client.SendAsync(Authed(HttpMethod.Get, "/me", token));
        me.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var companyId = doc.RootElement.GetProperty("memberships")[0].GetProperty("companyId").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == "admin@vilmomkt.com").Select(u => u.Id).FirstAsync();
        return (token, companyId, userId);
    }

    static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null, Guid? companyId = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (companyId is { } cid) req.Headers.Add("X-Company-Id", cid.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }
        return req;
    }

    static RemoteMarketplaceAd Remote(string id, string title, string? sku = null, string? gtin = null) =>
        new(id, title, 79.9m, 5m, "active", "https://ml.test/" + id, "https://example.com/t.jpg", sku, gtin,
            "MLB5672", "gold_special", ["https://example.com/p.jpg"], "buy_it_now", "me2", "{\"id\":\"" + id + "\"}");

    [Fact]
    public async Task Enqueue_process_classify_link_does_not_change_on_hand()
    {
        var (token, companyId, userId) = await AdminAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<SecretProtector>();

        var cfg = await db.CompanyMarketplaceConfigs.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == "MercadoLivre");
        if (cfg is null)
        {
            cfg = new CompanyMarketplaceConfig
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                MarketplaceCode = "MercadoLivre",
                IsEnabled = true,
                LinkStatus = LinkStatuses.Linked
            };
            db.CompanyMarketplaceConfigs.Add(cfg);
        }
        else
        {
            cfg.IsEnabled = true;
            cfg.LinkStatus = LinkStatuses.Linked;
        }
        if (!await db.CompanyMarketplaceParameters.AnyAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == "AccessToken"))
        {
            db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
            {
                Id = Guid.NewGuid(),
                ConfigId = cfg.Id,
                ParameterKey = "AccessToken",
                ParameterValue = protector.Protect("APP_USR-import-flow-token"),
                IsSecret = true
            });
        }
        if (!await db.CompanyMarketplaceParameters.AnyAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == "UserId"))
        {
            db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
            {
                Id = Guid.NewGuid(),
                ConfigId = cfg.Id,
                ParameterKey = "UserId",
                ParameterValue = "123456789"
            });
        }
        db.Listings.Add(new Listing
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            VendorUserId = userId,
            Sku = "CAMISETA-001",
            MarketplaceCode = "MercadoLivre",
            Status = ListingStatuses.Published,
            RemoteId = "MLB-EXISTING"
        });
        await db.SaveChangesAsync();

        var shirtBefore = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.Sku == "CAMISETA-001")
            .Select(b => b.OnHand).FirstAsync();
        var pantsBefore = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.Sku == "CALCA-001")
            .Select(b => b.OnHand).FirstAsync();

        var enqueue = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements/imports/MercadoLivre", token, new { }, companyId));
        Assert.Equal(HttpStatusCode.Accepted, enqueue.StatusCode);
        using var queued = JsonDocument.Parse(await enqueue.Content.ReadAsStringAsync());
        Assert.Equal("Queued", queued.RootElement.GetProperty("status").GetString());
        var runId = queued.RootElement.GetProperty("runId").GetGuid();

        var work = await db.WorkItems.SingleAsync(w => w.Kind == WorkKinds.ListingImport && w.Status == "Pending");
        Assert.Contains(runId.ToString(), work.PayloadJson, StringComparison.OrdinalIgnoreCase);

        var adapter = new FakeCatalogAdapter([
            Remote("MLB-EXISTING", "Camiseta já no Vilmo", "CAMISETA-001"),
            Remote("MLB-SKU", "Calça do ML", "CALCA-001"),
            Remote("MLB-OPEN", "Anúncio sem SKU")
        ]);
        var imports = new ListingImportService(db, new ListingImportLogService(db), protector, [adapter], new NoHttpFactory());
        await imports.ProcessQueuedAsync(work, default);
        Assert.Equal("Done", work.Status);

        var rows = await db.MarketplaceRemoteAds.Where(x => x.CompanyId == companyId).ToListAsync();
        Assert.Equal(RemoteAdMatchStatuses.AlreadyLinked, rows.Single(x => x.RemoteId == "MLB-EXISTING").MatchStatus);
        Assert.Equal(RemoteAdMatchStatuses.Suggested, rows.Single(x => x.RemoteId == "MLB-SKU").MatchStatus);
        Assert.Equal("CALCA-001", rows.Single(x => x.RemoteId == "MLB-SKU").SuggestedSku);
        Assert.Equal(RemoteAdMatchStatuses.Unmatched, rows.Single(x => x.RemoteId == "MLB-OPEN").MatchStatus);

        var logs = await _client.SendAsync(Authed(HttpMethod.Get, $"/advertisements/import-logs?runId={runId}", token, companyId: companyId));
        logs.EnsureSuccessStatusCode();
        Assert.Contains("done", await logs.Content.ReadAsStringAsync());

        var ctx = new CompanyContext
        {
            UserId = userId,
            Email = "admin@vilmomkt.com",
            Name = "Admin",
            IsAdmin = true,
            Level = LoginLevels.Admin,
            CompanyId = companyId,
            Profile = UserProfiles.CompanyAdmin
        };
        var suggested = rows.Single(x => x.RemoteId == "MLB-SKU");
        var linked = await imports.LinkAsync(ctx, suggested.Id, "CALCA-001", default);
        using var linkedDoc = JsonDocument.Parse(JsonSerializer.Serialize(linked));
        Assert.True(linkedDoc.RootElement.GetProperty("stockUnchanged").GetBoolean());

        Assert.Equal(pantsBefore, await db.InventoryBalances.Where(b => b.CompanyId == companyId && b.Sku == "CALCA-001").Select(b => b.OnHand).FirstAsync());
        Assert.Equal(shirtBefore, await db.InventoryBalances.Where(b => b.CompanyId == companyId && b.Sku == "CAMISETA-001").Select(b => b.OnHand).FirstAsync());
        var listing = await db.Listings.SingleAsync(l => l.CompanyId == companyId && l.RemoteId == "MLB-SKU");
        Assert.Equal(ListingStatuses.Published, listing.Status);
        Assert.Equal("CALCA-001", listing.Sku);
        Assert.False(await db.WorkItems.AnyAsync(w => w.Kind == WorkKinds.PublishListing));

        var open = rows.Single(x => x.RemoteId == "MLB-OPEN");
        await imports.IgnoreAsync(ctx, open.Id, default);
        Assert.Equal(RemoteAdMatchStatuses.Ignored, (await db.MarketplaceRemoteAds.FirstAsync(x => x.Id == open.Id)).MatchStatus);
    }

    sealed class FakeCatalogAdapter(IReadOnlyList<RemoteMarketplaceAd> ads) : IMarketplaceListingCatalogAdapter
    {
        public string MarketplaceCode => MercadoLivreListingCatalogAdapter.Code;
        public Task<IReadOnlyList<RemoteMarketplaceAd>> ListAdsAsync(MarketplaceCatalogContext ctx, CancellationToken ct) =>
            Task.FromResult(ads);
    }
}

public class ListingImportHttpTests
{
    [Fact]
    public async Task Catalog_401_throws_instead_of_empty_list()
    {
        var adapter = new MercadoLivreListingCatalogAdapter(new HandlerFactory(new FixedHandler(HttpStatusCode.Unauthorized, """{"message":"invalid access token"}""")));
        var ex = await Assert.ThrowsAsync<MarketplaceHttpException>(() =>
            adapter.ListAdsAsync(new MarketplaceCatalogContext(Guid.NewGuid(), "APP_USR-expired", "https://api.mercadolibre.com", "123"), default));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Catalog_scan_unsupported_then_empty_offset_is_empty()
    {
        var handler = new SequenceHandler();
        handler.Map.Add("search_type=scan", (HttpStatusCode.BadRequest, """{"error":"not_supported"}"""));
        handler.Map.Add("offset=0", (HttpStatusCode.OK, """{"results":[],"paging":{"total":0}}"""));
        var adapter = new MercadoLivreListingCatalogAdapter(new HandlerFactory(handler));
        var ads = await adapter.ListAdsAsync(new MarketplaceCatalogContext(Guid.NewGuid(), "APP_USR-ok", "https://api.mercadolibre.com", "123"), default);
        Assert.Empty(ads);
    }

    sealed class HandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    sealed class FixedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }

    sealed class SequenceHandler : HttpMessageHandler
    {
        public Dictionary<string, (HttpStatusCode Status, string Body)> Map { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            foreach (var (key, val) in Map)
            {
                if (!url.Contains(key, StringComparison.Ordinal)) continue;
                return Task.FromResult(new HttpResponseMessage(val.Status)
                {
                    Content = new StringContent(val.Body, System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}

sealed class NoHttpFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => throw new InvalidOperationException("http not expected");
}
