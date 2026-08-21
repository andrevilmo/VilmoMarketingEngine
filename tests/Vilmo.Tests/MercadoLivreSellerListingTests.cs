using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;
using Vilmo.Services;

namespace Vilmo.Tests;

public class MercadoLivreSellerListingTests
{
    static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public void Address_pending_blocks_list_and_maps_message()
    {
        var user = Json("""
            {"id":123,"nickname":"VILMOTESTE","status":{"list":{"allow":false,"codes":["address_pending"]}}}
            """);
        Assert.False(MercadoLivreSellerListing.CanList(user));
        Assert.Contains("address_pending", MercadoLivreSellerListing.ListCodes(user));
        var msg = MercadoLivreSellerListing.UserMessage(MercadoLivreSellerListing.ListCodes(user));
        Assert.Contains("endereço", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("address_pending", msg);
    }

    [Fact]
    public void Http_401_maps_reconnect_message()
    {
        var msg = MercadoLivreSellerListing.UserMessageFromHttp(401, """{"code":"unauthorized","message":"invalid access token"}""");
        Assert.Contains("Reconecte", msg);
        Assert.DoesNotContain("endereço", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Http_403_body_maps_address_pending()
    {
        var msg = MercadoLivreSellerListing.UserMessageFromHttp(403, """
            {"cause":["address_pending"],"message":"seller.unable_to_list","error":"User is unable to list.","status":403}
            """);
        Assert.Contains("endereço", msg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Veja o retorno técnico", msg);
    }

    [Fact]
    public void User_update_body_uses_company_address_and_cnpj()
    {
        var company = new Company
        {
            LegalName = "A. VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA",
            TradeName = "VilmoTeste",
            Cnpj = "68431371000161",
            Street = "R VITOR KONDER",
            Number = "223",
            Complement = "SALA 1108",
            Neighborhood = "CENTRO",
            City = "FLORIANOPOLIS",
            Uf = "SC",
            Cep = "88015-400",
            Phone = "5180227183"
        };
        var body = MercadoLivreSellerListing.UserUpdateBody(company);
        Assert.Equal("R VITOR KONDER 223 SALA 1108", body["address"]);
        Assert.Equal("BR-SC", body["state"]);
        Assert.Equal("FLORIANOPOLIS", body["city"]);
        Assert.Equal("88015400", body["zip_code"]);
        var phone = Assert.IsType<Dictionary<string, object?>>(body["phone"]);
        Assert.Equal("51", phone["area_code"]);
        Assert.Equal("80227183", phone["number"]);
        var id = Assert.IsType<Dictionary<string, object?>>(body["identification"]);
        Assert.Equal("CNPJ", id["type"]);
        Assert.Equal("68431371000161", id["number"]);
    }

    [Fact]
    public void Allow_true_without_codes_can_list()
    {
        var user = Json("""{"id":1,"status":{"list":{"allow":true,"codes":[]}}}""");
        Assert.True(MercadoLivreSellerListing.CanList(user));
    }
}

public class MercadoLivrePublishAddressGateTests
{
    static AppDbContext Db()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"ml-addr-{Guid.NewGuid():N}.db")}")
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    static SecretProtector Protector() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());

    [Fact]
    public async Task Publish_sends_company_address_then_posts_item_when_ml_allows()
    {
        await using var db = Db();
        var (ad, listing, _) = await SeedAsync(db);
        var calls = new List<string>();
        var mePending = false;
        var handler = new StubHandler
        {
            Impl = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                calls.Add($"{req.Method.Method} {path}");
                if (req.Method == HttpMethod.Get && path.EndsWith("/users/me", StringComparison.Ordinal))
                {
                    if (!mePending)
                    {
                        mePending = true;
                        return Json(200, """{"id":99,"nickname":"VILMOTESTE","status":{"list":{"allow":false,"codes":["address_pending"]}}}""");
                    }
                    return Json(200, """{"id":99,"nickname":"VILMOTESTE","status":{"list":{"allow":true,"codes":[]}}}""");
                }
                if (req.Method == HttpMethod.Put && path.Contains("/users/", StringComparison.Ordinal))
                    return Json(200, """{"id":99,"address":{"state":"BR-SC","city":"FLORIANOPOLIS","zip_code":"88015400"}}""");
                if (req.Method == HttpMethod.Post && path.EndsWith("/addresses", StringComparison.Ordinal))
                    return Json(201, """{"id":1}""");
                if (req.Method == HttpMethod.Post && path.EndsWith("/items", StringComparison.Ordinal))
                    return Json(201, """{"id":"MLB123","status":"active","permalink":"https://mercadolivre.com.br/MLB123"}""");
                return Json(404, """{"error":"not_found"}""");
            }
        };
        var runner = new ListingPublishRunner(db, new ListingPublishLogService(db), Protector(), new StubFactory(handler));
        await runner.PublishAsync(ad, listing, Guid.NewGuid(), default);
        Assert.Equal(ListingStatuses.Published, listing.Status);
        Assert.Equal("MLB123", listing.RemoteId);
        Assert.Contains(calls, c => c.StartsWith("GET ", StringComparison.Ordinal) && c.EndsWith("/users/me", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.StartsWith("PUT ", StringComparison.Ordinal) && c.Contains("/users/99", StringComparison.Ordinal));
        Assert.Contains("POST /items", calls);
        var logs = await db.ListingPublishLogs.ToListAsync();
        Assert.Contains(logs, l => l.UserMessage.Contains("endereço da empresa", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Publish_stops_without_posting_item_when_address_still_pending()
    {
        await using var db = Db();
        var (ad, listing, _) = await SeedAsync(db);
        var calls = new List<string>();
        var handler = new StubHandler
        {
            Impl = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                calls.Add($"{req.Method.Method} {path}");
                if (path.EndsWith("/users/me", StringComparison.Ordinal))
                    return Json(200, """{"id":99,"status":{"list":{"allow":false,"codes":["address_pending"]}}}""");
                if (req.Method == HttpMethod.Put)
                    return Json(200, "{}");
                if (req.Method == HttpMethod.Post && path.EndsWith("/addresses", StringComparison.Ordinal))
                    return Json(400, """{"error":"bad_request"}""");
                throw new InvalidOperationException($"unexpected {req.Method} {path}");
            }
        };
        var runner = new ListingPublishRunner(db, new ListingPublishLogService(db), Protector(), new StubFactory(handler));
        await runner.PublishAsync(ad, listing, Guid.NewGuid(), default);
        Assert.Equal(ListingStatuses.Error, listing.Status);
        Assert.DoesNotContain(calls, c => c.Contains("/items", StringComparison.Ordinal));
        var failed = await db.ListingPublishLogs.FirstAsync(l => l.StepCode == ListingPublishLogSteps.Failed);
        Assert.Contains("endereço", failed.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("address_pending", failed.UserMessage);
    }

    [Fact]
    public async Task Publish_refreshes_expired_token_then_posts_item()
    {
        await using var db = Db();
        var (ad, listing, _) = await SeedAsync(db, withRefresh: true);
        var calls = new List<string>();
        var handler = new StubHandler
        {
            Impl = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                calls.Add($"{req.Method.Method} {path}");
                var auth = req.Headers.Authorization?.Parameter ?? "";
                if (req.Method == HttpMethod.Post && path.EndsWith("/oauth/token", StringComparison.Ordinal))
                    return Json(200, """{"access_token":"APP_USR-fresh","refresh_token":"TG-fresh","user_id":"99"}""");
                if (req.Method == HttpMethod.Get && path.EndsWith("/users/me", StringComparison.Ordinal))
                {
                    if (auth.Contains("fresh", StringComparison.Ordinal))
                        return Json(200, """{"id":99,"nickname":"VILMOTESTE","status":{"list":{"allow":true,"codes":[]}}}""");
                    return Json(401, """{"code":"unauthorized","message":"invalid access token"}""");
                }
                if (req.Method == HttpMethod.Post && path.EndsWith("/items", StringComparison.Ordinal))
                {
                    Assert.Contains("fresh", auth, StringComparison.Ordinal);
                    return Json(201, """{"id":"MLB999","status":"active","permalink":"https://mercadolivre.com.br/MLB999"}""");
                }
                return Json(404, "{}");
            }
        };
        var runner = new ListingPublishRunner(db, new ListingPublishLogService(db), Protector(), new StubFactory(handler));
        await runner.PublishAsync(ad, listing, Guid.NewGuid(), default);
        Assert.Equal(ListingStatuses.Published, listing.Status);
        Assert.Equal("MLB999", listing.RemoteId);
        Assert.Contains(calls, c => c.Contains("/oauth/token", StringComparison.Ordinal));
        Assert.Contains("POST /items", calls);
    }

    static async Task<(Advertisement Ad, Listing Listing, Company Company)> SeedAsync(AppDbContext db, bool withRefresh = false)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            LegalName = "Vilmo Teste LTDA",
            TradeName = "VilmoTeste",
            Cnpj = "68431371000161",
            Street = "R VITOR KONDER",
            Number = "223",
            Neighborhood = "CENTRO",
            City = "FLORIANOPOLIS",
            Uf = "SC",
            Cep = "88015400",
            Phone = "5180227183"
        };
        db.Companies.Add(company);
        db.Marketplaces.Add(new Marketplace
        {
            Code = "MercadoLivre",
            DisplayName = "Mercado Livre",
            AuthProtocolCode = "OAuth2",
            BaseUrl = "https://api.mercadolibre.com",
            IsActive = true
        });
        var cfg = new CompanyMarketplaceConfig
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            MarketplaceCode = "MercadoLivre",
            IsEnabled = true,
            LinkStatus = LinkStatuses.Linked
        };
        db.CompanyMarketplaceConfigs.Add(cfg);
        db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
        {
            Id = Guid.NewGuid(),
            ConfigId = cfg.Id,
            ParameterKey = "AccessToken",
            ParameterValue = "APP_USR-test-token",
            IsSecret = false
        });
        if (withRefresh)
        {
            db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
            {
                Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "RefreshToken",
                ParameterValue = "TG-refresh", IsSecret = false
            });
            db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
            {
                Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "ClientId",
                ParameterValue = "ml-client", IsSecret = false
            });
            db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
            {
                Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = "ClientSecret",
                ParameterValue = "ml-secret", IsSecret = false
            });
        }
        var ad = new Advertisement
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            VendorUserId = Guid.NewGuid(),
            Kind = "Product",
            Sku = "AD-ADDR-1",
            Title = "Camiseta",
            FamilyName = "Vestuário",
            Price = 29.9m,
            AvailableQuantity = 1,
            Condition = "new"
        };
        db.Advertisements.Add(ad);
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            VendorUserId = ad.VendorUserId,
            AdvertisementId = ad.Id,
            Sku = ad.Sku,
            MarketplaceCode = "MercadoLivre",
            Status = ListingStatuses.Draft
        };
        db.Listings.Add(listing);
        await db.SaveChangesAsync();
        return (ad, listing, company);
    }

    static HttpResponseMessage Json(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
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
        public HttpClient CreateClient(string name)
        {
            var c = new HttpClient(handler, disposeHandler: false);
            c.BaseAddress = new Uri("https://api.mercadolibre.com");
            return c;
        }
    }
}
