using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Marketplaces;
using Vilmo.Services;
using Vilmo.Workers;

namespace Vilmo.Tests;

public class StockPublishTests
{
    static AppDbContext Db()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"stock-{Guid.NewGuid():N}.db")}")
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    static (StockPublishService Stock, RecordingMarketplaceHttpClient Http, WorkProcessor Worker) Pipeline(AppDbContext db)
    {
        var http = new RecordingMarketplaceHttpClient();
        var gateway = new MarketplaceStockGateway(
            [
                new MercadoLivreStockAdapter(),
                new ShopeeStockAdapter(),
                new SheinStockAdapter(),
                new MagaluStockAdapter()
            ],
            http);
        var stock = new StockPublishService(db, gateway);
        var nfeLogs = new NfeIngestLogService(db);
        var nfe = new NfeIngestService(db, new InventoryService(db), nfeLogs);
        var sales = new SalesService(db, new InventoryService(db));
        var worker = new WorkProcessor(db, nfe, sales, stock, NullLogger<WorkProcessor>.Instance);
        return (stock, http, worker);
    }

    static Listing Listing(Guid company, Guid vendor, string sku, string marketplace, string remote, decimal qty) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = company,
        VendorUserId = vendor,
        Sku = sku,
        MarketplaceCode = marketplace,
        Status = "PublishedDemo",
        RemoteId = remote,
        AvailableQuantity = qty
    };

    [Fact]
    public async Task Paid_sale_decrements_and_fans_out_other_marketplaces()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        var vendor = Guid.NewGuid();
        db.Products.Add(new Product { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", Name = "A", SalePrice = 10 });
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 5 });
        var ml = Listing(company, vendor, "A", "MercadoLivre", "MLB-ORIGIN", 5);
        var shopee = Listing(company, vendor, "A", "Shopee", "SHP-1", 5);
        var magalu = Listing(company, vendor, "A", "Magalu", "MAG-1", 5);
        db.Listings.AddRange(ml, shopee, magalu);
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            VendorUserId = vendor,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = "paid-1",
            Status = SaleStatuses.PendingPayment,
            Cep = "88015400",
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "A", Name = "A", Quantity = 2, UnitPrice = 10 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        var inv = new InventoryService(db);
        var paid = await inv.ApplySalePaidAsync(sale, default);
        Assert.True(paid.Ok);
        Assert.Equal(3, (await db.InventoryBalances.FirstAsync()).OnHand);
        Assert.Equal(SaleStatuses.Paid, sale.Status);
        Assert.Single(db.WorkItems.Where(w => w.Kind == WorkKinds.StockPublish));

        var (_, http, worker) = Pipeline(db);
        var n = await worker.DrainAsync([WorkKinds.StockPublish], default);
        Assert.Equal(1, n);

        var mlAfter = await db.Listings.FirstAsync(l => l.Id == ml.Id);
        var shopeeAfter = await db.Listings.FirstAsync(l => l.Id == shopee.Id);
        var magaluAfter = await db.Listings.FirstAsync(l => l.Id == magalu.Id);
        Assert.Equal(5, mlAfter.AvailableQuantity);
        Assert.Null(mlAfter.LastStockPayloadJson);
        Assert.Equal(3, shopeeAfter.AvailableQuantity);
        Assert.Equal(3, magaluAfter.AvailableQuantity);
        Assert.Contains("seller_stock", shopeeAfter.LastStockPayloadJson);
        Assert.Contains("\"quantity\":3", magaluAfter.LastStockPayloadJson);
        Assert.Equal(2, http.Sent.Count);
        Assert.DoesNotContain(http.Sent, r => r.MarketplaceCode == "MercadoLivre");
        Assert.Contains(http.Sent, r => r.MarketplaceCode == "Shopee" && r.Path.Contains("update_stock"));
        Assert.Contains(http.Sent, r => r.MarketplaceCode == "Magalu");
    }

    [Fact]
    public async Task Paid_twice_does_not_enqueue_again()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 4 });
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "Shopee",
            RemoteOrderId = "paid-2",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "A", Name = "A", Quantity = 1, UnitPrice = 10 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        Assert.True((await inv.ApplySalePaidAsync(sale, default)).Ok);
        Assert.True((await inv.ApplySalePaidAsync(sale, default)).Ok);
        Assert.Equal(3, (await db.InventoryBalances.FirstAsync()).OnHand);
        Assert.Equal(1, await db.WorkItems.CountAsync(w => w.Kind == WorkKinds.StockPublish));
    }

    [Fact]
    public async Task Stock_short_does_not_fan_out()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 1 });
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = "short-1",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "A", Name = "A", Quantity = 9, UnitPrice = 10 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        var result = await inv.ApplySalePaidAsync(sale, default);
        Assert.False(result.Ok);
        Assert.Equal(0, await db.WorkItems.CountAsync(w => w.Kind == WorkKinds.StockPublish));
    }

    [Fact]
    public async Task Cancel_restores_qty_on_other_marketplaces()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        var vendor = Guid.NewGuid();
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 2 });
        db.Listings.Add(Listing(company, vendor, "A", "MercadoLivre", "MLB-1", 2));
        db.Listings.Add(Listing(company, vendor, "A", "Shopee", "SHP-2", 2));
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            VendorUserId = vendor,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = "cnl-1",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "A", Name = "A", Quantity = 1, UnitPrice = 10 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        Assert.True((await inv.ApplySalePaidAsync(sale, default)).Ok);
        var (_, _, worker) = Pipeline(db);
        await worker.DrainAsync([WorkKinds.StockPublish], default);
        Assert.Equal(1, (await db.Listings.FirstAsync(l => l.MarketplaceCode == "Shopee")).AvailableQuantity);

        await inv.ReverseSalePaidAsync(sale, default);
        Assert.Equal(2, (await db.InventoryBalances.FirstAsync()).OnHand);
        await worker.DrainAsync([WorkKinds.StockPublish], default);
        Assert.Equal(2, (await db.Listings.FirstAsync(l => l.MarketplaceCode == "Shopee")).AvailableQuantity);
        Assert.Equal(2, (await db.Listings.FirstAsync(l => l.MarketplaceCode == "MercadoLivre")).AvailableQuantity);
    }

    [Fact]
    public void Adapters_map_platform_payloads()
    {
        var listing = Listing(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "MercadoLivre", "MLB123", 4);
        var ml = new MercadoLivreStockAdapter().Map(listing, 4);
        Assert.Equal("PUT", ml.Method);
        Assert.Equal("/items/MLB123", ml.Path);
        Assert.Contains("\"available_quantity\":4", ml.BodyJson);

        listing.MarketplaceCode = "Shopee";
        var sh = new ShopeeStockAdapter().Map(listing, 4);
        Assert.Equal("POST", sh.Method);
        Assert.Contains("update_stock", sh.Path);
        Assert.Contains("\"stock\":4", sh.BodyJson);
    }

    [Fact]
    public async Task Inbound_nfe_updates_all_marketplace_ads()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        var vendor = Guid.NewGuid();
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 1 });
        db.Listings.Add(Listing(company, vendor, "A", "MercadoLivre", "MLB-IN", 1));
        db.Listings.Add(Listing(company, vendor, "A", "Shopee", "SHP-IN", 1));
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        await inv.ApplyInboundAsync(company, "A", 3, "42260868431371000161555001000000001123456788", 1, default);
        var (_, http, worker) = Pipeline(db);
        await worker.DrainAsync([WorkKinds.StockPublish], default);
        Assert.Equal(4, (await db.Listings.FirstAsync(l => l.MarketplaceCode == "MercadoLivre")).AvailableQuantity);
        Assert.Equal(4, (await db.Listings.FirstAsync(l => l.MarketplaceCode == "Shopee")).AvailableQuantity);
        Assert.Equal(2, http.Sent.Count);
    }
}
