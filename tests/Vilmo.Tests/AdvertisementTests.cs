using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Services;

namespace Vilmo.Tests;

public class AdvertisementApiTests : IClassFixture<ApiFactory>
{
    readonly ApiFactory _factory;
    readonly HttpClient _client;

    public AdvertisementApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    async Task<string> LoginAsync(string email = "admin@vilmomkt.com")
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new { email, password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    async Task<(string Token, Guid CompanyId)> AdminAsync()
    {
        var token = await LoginAsync();
        var me = await _client.SendAsync(Authed(HttpMethod.Get, "/me", token));
        me.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var companyId = doc.RootElement.GetProperty("memberships")[0].GetProperty("companyId").GetGuid();
        return (token, companyId);
    }

    HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null, Guid? companyId = null)
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
    public async Task Listing_fields_include_common_and_channel_extras()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/marketplaces/listing-fields", token, companyId: companyId));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var keys = doc.RootElement.GetProperty("common").EnumerateArray().Select(x => x.GetProperty("fieldKey").GetString()).ToList();
        Assert.Contains("title", keys);
        Assert.Contains("familyName", keys);
        Assert.Contains("price", keys);
        Assert.Contains("availableQuantity", keys);
        Assert.True(doc.RootElement.GetProperty("byMarketplace").TryGetProperty("MercadoLivre", out var ml));
        Assert.Contains(ml.EnumerateArray(), x => x.GetProperty("fieldKey").GetString() == "categoryId");
    }

    [Fact]
    public async Task Vendor_can_list_products_for_anuncios_form()
    {
        var token = await LoginAsync("vendedor@vilmomkt.com");
        var res = await _client.SendAsync(Authed(HttpMethod.Get, "/products", token));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("CAMISETA-001", body);
        Assert.Contains("CALCA-001", body);
        var post = await _client.SendAsync(Authed(HttpMethod.Post, "/products", token, new { sku = "X", name = "X", salePrice = 1 }));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Publish_kit_for_selected_company_and_paid_decrements_component_qty()
    {
        var (token, companyId) = await AdminAsync();
        var sku = $"KIT-API-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var pub = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements", token, new
        {
            kind = "Kit",
            sku,
            title = "Kit camiseta e calça",
            familyName = "Kit camiseta e calça",
            description = "Conjunto",
            price = 199.90m,
            availableQuantity = 3,
            condition = "new",
            brand = "Vilmo",
            marketplaceCodes = new[] { "MercadoLivre" },
            items = new[]
            {
                new { sku = "CAMISETA-001", quantity = 2m },
                new { sku = "CALCA-001", quantity = 1m }
            },
            attributes = new[]
            {
                new { marketplaceCode = "MercadoLivre", fieldName = "categoryId", fieldValue = "MLB123" }
            }
        }, companyId));
        Assert.Equal(HttpStatusCode.Created, pub.StatusCode);
        using var created = JsonDocument.Parse(await pub.Content.ReadAsStringAsync());
        var ad = created.RootElement.GetProperty("advertisement");
        Assert.Equal("Kit", ad.GetProperty("kind").GetString());
        Assert.Equal(sku, ad.GetProperty("sku").GetString());
        Assert.Equal("Kit camiseta e calça", ad.GetProperty("familyName").GetString());
        Assert.Equal(2, ad.GetProperty("items").GetArrayLength());
        Assert.Contains(ad.GetProperty("channels").EnumerateArray(), c => c.GetProperty("marketplaceCode").GetString() == "MercadoLivre");
        Assert.Contains(ad.GetProperty("channels").EnumerateArray(), c => c.GetProperty("status").GetString() == ListingStatuses.Draft);

        var list = await _client.SendAsync(Authed(HttpMethod.Get, "/advertisements", token, companyId: companyId));
        list.EnsureSuccessStatusCode();
        Assert.Contains(sku, await list.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shirt = await db.InventoryBalances.FirstAsync(b => b.CompanyId == companyId && b.Sku == "CAMISETA-001");
        var pants = await db.InventoryBalances.FirstAsync(b => b.CompanyId == companyId && b.Sku == "CALCA-001");
        var shirtBefore = shirt.OnHand;
        var pantsBefore = pants.OnHand;
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = $"kit-{sku}",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = sku, Name = "Kit", Quantity = 2, UnitPrice = 199.90m }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        var pay = await _client.SendAsync(Authed(HttpMethod.Post, $"/sales/{sale.Id}/commit-stock", token, new { }, companyId));
        pay.EnsureSuccessStatusCode();
        await db.Entry(shirt).ReloadAsync();
        await db.Entry(pants).ReloadAsync();
        Assert.Equal(shirtBefore - 4, shirt.OnHand);
        Assert.Equal(pantsBefore - 2, pants.OnHand);
        var movements = await db.InventoryMovements.Where(m => m.SaleId == sale.Id && m.Kind == InventoryMovementKinds.SalePaid).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, m => m.Sku == "CAMISETA-001" && m.Quantity == -4);
        Assert.Contains(movements, m => m.Sku == "CALCA-001" && m.Quantity == -2);

        var replay = await _client.SendAsync(Authed(HttpMethod.Post, $"/sales/{sale.Id}/commit-stock", token, new { }, companyId));
        replay.EnsureSuccessStatusCode();
        await db.Entry(shirt).ReloadAsync();
        Assert.Equal(shirtBefore - 4, shirt.OnHand);
    }

    [Fact]
    public async Task Publish_is_scoped_to_selected_company()
    {
        var (token, companyA) = await AdminAsync();
        var created = await _client.SendAsync(Authed(HttpMethod.Post, "/companies", token, new
        {
            legalName = "Outra Empresa Kit LTDA",
            tradeName = "Outra Kit",
            cnpj = Cnpj.Complete("223334440001"),
            street = "Rua B",
            number = "2",
            neighborhood = "Centro",
            city = "Florianopolis",
            uf = "SC",
            cep = "88010000"
        }));
        created.EnsureSuccessStatusCode();
        using var companyDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var companyB = companyDoc.RootElement.GetProperty("id").GetGuid();

        var product = await _client.SendAsync(Authed(HttpMethod.Post, "/products", token, new
        {
            sku = "OUTRA-SKU",
            name = "Produto B",
            salePrice = 10m
        }, companyB));
        product.EnsureSuccessStatusCode();

        var sku = $"KIT-B-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var pub = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements", token, new
        {
            kind = "Product",
            sku,
            title = "Só na empresa B",
            familyName = "Produto B",
            price = 10m,
            availableQuantity = 1,
            marketplaceCodes = new[] { "MercadoLivre" },
            items = new[] { new { sku = "OUTRA-SKU", quantity = 1m } }
        }, companyB));
        Assert.Equal(HttpStatusCode.Created, pub.StatusCode);

        var listA = await _client.SendAsync(Authed(HttpMethod.Get, "/advertisements", token, companyId: companyA));
        var listB = await _client.SendAsync(Authed(HttpMethod.Get, "/advertisements", token, companyId: companyB));
        Assert.DoesNotContain(sku, await listA.Content.ReadAsStringAsync());
        Assert.Contains(sku, await listB.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Publish_unknown_product_is_404()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements", token, new
        {
            kind = "Product",
            title = "Missing",
            price = 1m,
            items = new[] { new { sku = "NOPE-SKU", quantity = 1m } }
        }, companyId));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Empty_marketplace_codes_is_400()
    {
        var (token, companyId) = await AdminAsync();
        var res = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements", token, new
        {
            kind = "Product",
            title = "Sem canal",
            price = 1m,
            items = new[] { new { sku = "CAMISETA-001", quantity = 1m } },
            marketplaceCodes = Array.Empty<string>()
        }, companyId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("MarketplaceRequired", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Channel_publish_cancel_and_refresh_online_snapshot()
    {
        var (token, companyId) = await AdminAsync();
        var sku = $"AD-CH-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var pub = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements", token, new
        {
            kind = "Product",
            sku,
            title = "Camiseta canais",
            familyName = "Camiseta canal teste",
            price = 89.90m,
            availableQuantity = 4,
            marketplaceCodes = new[] { "MercadoLivre", "Shopee" },
            items = new[] { new { sku = "CAMISETA-001", quantity = 1m } }
        }, companyId));
        Assert.Equal(HttpStatusCode.Created, pub.StatusCode);
        using var created = JsonDocument.Parse(await pub.Content.ReadAsStringAsync());
        var ad = created.RootElement.GetProperty("advertisement");
        var id = ad.GetProperty("id").GetGuid();
        Assert.Equal(2, ad.GetProperty("channels").GetArrayLength());
        Assert.All(ad.GetProperty("channels").EnumerateArray(), c =>
            Assert.Equal(ListingStatuses.Draft, c.GetProperty("status").GetString()));

        var proceed = await _client.SendAsync(Authed(HttpMethod.Post,
            $"/advertisements/{id}/channels/MercadoLivre/publish", token, new { }, companyId));
        proceed.EnsureSuccessStatusCode();
        using var live = JsonDocument.Parse(await proceed.Content.ReadAsStringAsync());
        var ml = live.RootElement.GetProperty("advertisement").GetProperty("channels").EnumerateArray()
            .First(c => c.GetProperty("marketplaceCode").GetString() == "MercadoLivre");
        Assert.Equal(ListingStatuses.Published, ml.GetProperty("status").GetString());
        Assert.Equal("Publicado", ml.GetProperty("statusPt").GetString());
        Assert.False(string.IsNullOrWhiteSpace(ml.GetProperty("remoteId").GetString()));
        Assert.Equal("Camiseta canais", ml.GetProperty("remoteTitle").GetString());
        Assert.Equal(89.90m, ml.GetProperty("remotePrice").GetDecimal());
        Assert.Equal("active", ml.GetProperty("remoteStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(ml.GetProperty("remotePermalink").GetString()));
        Assert.True(ml.GetProperty("lastSyncedAt").ValueKind == JsonValueKind.String);
        var log = ml.GetProperty("publishLog");
        Assert.True(log.GetArrayLength() >= 3);
        Assert.Contains(log.EnumerateArray(), x => x.GetProperty("stepCode").GetString() == "callback");
        var callback = log.EnumerateArray().First(x => x.GetProperty("stepCode").GetString() == "callback");
        var callbackTech = callback.GetProperty("technicalJson").GetString()!;
        Assert.Contains("callbackResponse", callbackTech);
        Assert.Contains("\"request\"", callbackTech);
        Assert.Contains("\"response\"", callbackTech);
        Assert.Contains("family_name", callbackTech);
        Assert.Contains("Camiseta canal teste", callbackTech);

        var shopeeDraft = live.RootElement.GetProperty("advertisement").GetProperty("channels").EnumerateArray()
            .First(c => c.GetProperty("marketplaceCode").GetString() == "Shopee");
        Assert.Equal(ListingStatuses.Draft, shopeeDraft.GetProperty("status").GetString());

        var cancel = await _client.SendAsync(Authed(HttpMethod.Post,
            $"/advertisements/{id}/channels/Shopee/cancel", token, new { }, companyId));
        cancel.EnsureSuccessStatusCode();
        using var stopped = JsonDocument.Parse(await cancel.Content.ReadAsStringAsync());
        var shopee = stopped.RootElement.GetProperty("advertisement").GetProperty("channels").EnumerateArray()
            .First(c => c.GetProperty("marketplaceCode").GetString() == "Shopee");
        Assert.Equal(ListingStatuses.Cancelled, shopee.GetProperty("status").GetString());
        Assert.Equal("Cancelado", shopee.GetProperty("statusPt").GetString());
        Assert.Equal("paused", shopee.GetProperty("remoteStatus").GetString());

        var refresh = await _client.SendAsync(Authed(HttpMethod.Post,
            $"/advertisements/{id}/refresh", token, new { }, companyId));
        refresh.EnsureSuccessStatusCode();
        using var synced = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        var channels = synced.RootElement.GetProperty("advertisement").GetProperty("channels").EnumerateArray().ToList();
        var mlSync = channels.First(c => c.GetProperty("marketplaceCode").GetString() == "MercadoLivre");
        var shSync = channels.First(c => c.GetProperty("marketplaceCode").GetString() == "Shopee");
        Assert.Equal(ListingStatuses.Published, mlSync.GetProperty("status").GetString());
        Assert.Equal("active", mlSync.GetProperty("remoteStatus").GetString());
        Assert.True(mlSync.GetProperty("lastSyncedAt").ValueKind == JsonValueKind.String);
        Assert.Equal(ListingStatuses.Cancelled, shSync.GetProperty("status").GetString());
        Assert.Equal("paused", shSync.GetProperty("remoteStatus").GetString());
        Assert.Contains("callbackResponse", shSync.GetProperty("lastSyncJson").GetString()!);
        Assert.Contains(shSync.GetProperty("publishLog").EnumerateArray(),
            x => x.GetProperty("action").GetString() == "refresh");

        var get = await _client.SendAsync(Authed(HttpMethod.Get, $"/advertisements/{id}", token, companyId: companyId));
        get.EnsureSuccessStatusCode();
        Assert.Contains(sku, await get.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Family_name_falls_back_to_title_and_rejects_too_long()
    {
        var (token, companyId) = await AdminAsync();
        var sku = $"AD-FAM-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var pub = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements", token, new
        {
            kind = "Product",
            sku,
            title = "Título vira família",
            price = 10m,
            availableQuantity = 1,
            marketplaceCodes = new[] { "MercadoLivre" },
            items = new[] { new { sku = "CAMISETA-001", quantity = 1m } }
        }, companyId));
        Assert.Equal(HttpStatusCode.Created, pub.StatusCode);
        using var created = JsonDocument.Parse(await pub.Content.ReadAsStringAsync());
        Assert.Equal("Título vira família", created.RootElement.GetProperty("advertisement").GetProperty("familyName").GetString());

        var tooLong = await _client.SendAsync(Authed(HttpMethod.Post, "/advertisements", token, new
        {
            kind = "Product",
            sku = $"{sku}-L",
            title = "Ok",
            familyName = new string('A', 61),
            price = 10m,
            items = new[] { new { sku = "CAMISETA-001", quantity = 1m } },
            marketplaceCodes = new[] { "MercadoLivre" }
        }, companyId));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("FamilyNameTooLong", await tooLong.Content.ReadAsStringAsync());
    }
}

public class KitInventoryTests
{
    static AppDbContext Db()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"kit-{Guid.NewGuid():N}.db")}")
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    static Advertisement Kit(Guid company, Guid vendor, string sku, params (string Sku, decimal Qty)[] items)
    {
        var ad = new Advertisement
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            VendorUserId = vendor,
            Kind = AdvertisementKinds.Kit,
            Sku = sku,
            Title = sku,
            Price = 50,
            AvailableQuantity = 5,
            Condition = "new"
        };
        var i = 0;
        foreach (var (s, q) in items)
            ad.Items.Add(new AdvertisementItem { Id = Guid.NewGuid(), AdvertisementId = ad.Id, Sku = s, Quantity = q, SortOrder = i++ });
        return ad;
    }

    [Fact]
    public async Task Paid_kit_decrements_each_component_times_sale_qty()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        var vendor = Guid.NewGuid();
        db.InventoryBalances.AddRange(
            new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 10 },
            new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "B", OnHand = 8 });
        db.Advertisements.Add(Kit(company, vendor, "KIT-1", ("A", 2), ("B", 1)));
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            VendorUserId = vendor,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = "k1",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "KIT-1", Name = "Kit", Quantity = 2, UnitPrice = 50 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        var result = await inv.ApplySalePaidAsync(sale, default);
        Assert.True(result.Ok);
        Assert.Equal(6, (await db.InventoryBalances.FirstAsync(b => b.Sku == "A")).OnHand);
        Assert.Equal(6, (await db.InventoryBalances.FirstAsync(b => b.Sku == "B")).OnHand);
        Assert.Equal(2, await db.InventoryMovements.CountAsync(m => m.SaleId == sale.Id && m.Kind == InventoryMovementKinds.SalePaid));
        var second = await inv.ApplySalePaidAsync(sale, default);
        Assert.True(second.Ok);
        Assert.Equal(6, (await db.InventoryBalances.FirstAsync(b => b.Sku == "A")).OnHand);
    }

    [Fact]
    public async Task Paid_kit_stock_short_does_not_partial_decrement()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        db.InventoryBalances.AddRange(
            new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 10 },
            new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "B", OnHand = 1 });
        db.Advertisements.Add(Kit(company, Guid.NewGuid(), "KIT-2", ("A", 2), ("B", 1)));
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "Shopee",
            RemoteOrderId = "k2",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "KIT-2", Name = "Kit", Quantity = 2, UnitPrice = 50 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var result = await new InventoryService(db).ApplySalePaidAsync(sale, default);
        Assert.False(result.Ok);
        Assert.Equal("stock_short", result.Error);
        Assert.Equal("B", sale.StockShort);
        Assert.Equal(10, (await db.InventoryBalances.FirstAsync(b => b.Sku == "A")).OnHand);
        Assert.Equal(1, (await db.InventoryBalances.FirstAsync(b => b.Sku == "B")).OnHand);
        Assert.Equal(SaleStatuses.PendingPayment, sale.Status);
        Assert.Empty(await db.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task Paid_product_ad_still_decrements_the_sku()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 5 });
        var ad = Kit(company, Guid.NewGuid(), "A", ("A", 1));
        ad.Kind = AdvertisementKinds.Product;
        db.Advertisements.Add(ad);
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "Magalu",
            RemoteOrderId = "p1",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "A", Name = "A", Quantity = 2, UnitPrice = 10 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var result = await new InventoryService(db).ApplySalePaidAsync(sale, default);
        Assert.True(result.Ok);
        Assert.Equal(3, (await db.InventoryBalances.FirstAsync()).OnHand);
    }

    [Fact]
    public async Task Reverse_paid_kit_restores_component_qty()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        db.InventoryBalances.AddRange(
            new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 10 },
            new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "B", OnHand = 8 });
        db.Advertisements.Add(Kit(company, Guid.NewGuid(), "KIT-3", ("A", 2), ("B", 3)));
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = "k3",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "KIT-3", Name = "Kit", Quantity = 1, UnitPrice = 50 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        Assert.True((await inv.ApplySalePaidAsync(sale, default)).Ok);
        await inv.ReverseSalePaidAsync(sale, default);
        Assert.Equal(10, (await db.InventoryBalances.FirstAsync(b => b.Sku == "A")).OnHand);
        Assert.Equal(8, (await db.InventoryBalances.FirstAsync(b => b.Sku == "B")).OnHand);
    }
}
