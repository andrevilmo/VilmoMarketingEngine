using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Services;

namespace Vilmo.Tests;

public class InventoryFlowTests
{
    static AppDbContext Db()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"inv-{Guid.NewGuid():N}.db")}")
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SalePaid_decrements_once_and_not_below_zero()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        db.Products.Add(new Product { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", Name = "A", SalePrice = 10 });
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 5 });
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = "r1",
            Status = SaleStatuses.PendingPayment,
            Cep = "88015400",
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "A", Name = "A", Quantity = 2, UnitPrice = 10 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        var first = await inv.ApplySalePaidAsync(sale, default);
        Assert.True(first.Ok);
        Assert.Equal(3, (await db.InventoryBalances.FirstAsync()).OnHand);
        var second = await inv.ApplySalePaidAsync(sale, default);
        Assert.True(second.Ok);
        Assert.Equal(3, (await db.InventoryBalances.FirstAsync()).OnHand);
    }

    [Fact]
    public async Task SalePaid_stock_short_does_not_go_negative()
    {
        await using var db = Db();
        var company = Guid.NewGuid();
        db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = company, Sku = "A", OnHand = 1 });
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            CompanyId = company,
            MarketplaceCode = "MercadoLivre",
            RemoteOrderId = "r2",
            Status = SaleStatuses.PendingPayment,
            Items = [new SaleItem { Id = Guid.NewGuid(), Sku = "A", Name = "A", Quantity = 5, UnitPrice = 10 }]
        };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var inv = new InventoryService(db);
        var result = await inv.ApplySalePaidAsync(sale, default);
        Assert.False(result.Ok);
        Assert.Equal("stock_short", result.Error);
        Assert.Equal(1, (await db.InventoryBalances.FirstAsync()).OnHand);
        Assert.Equal(SaleStatuses.PendingPayment, sale.Status);
    }
}
