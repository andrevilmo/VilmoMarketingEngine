using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

public sealed class InventoryService(AppDbContext db)
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<List<object>> ListAsync(Guid companyId, CancellationToken ct)
    {
        var q =
            from p in db.Products.AsNoTracking()
            where p.CompanyId == companyId
            join b in db.InventoryBalances.AsNoTracking()
                on new { p.CompanyId, p.Sku } equals new { b.CompanyId, b.Sku } into gj
            from b in gj.DefaultIfEmpty()
            orderby p.Sku
            select new { p.Sku, p.Name, p.Ean, p.Ncm, p.SalePrice, onHand = b == null ? 0m : b.OnHand };
        return (await q.ToListAsync(ct)).Cast<object>().ToList();
    }

    public async Task SetSalePriceAsync(Guid companyId, string sku, decimal price, CancellationToken ct)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Sku == sku, ct)
                ?? throw new KeyNotFoundException("ProductNotFound");
        p.SalePrice = price;
        await db.SaveChangesAsync(ct);
    }

    public async Task<(bool Ok, string? Error)> ApplySalePaidAsync(Sale sale, CancellationToken ct)
    {
        var changedSkus = new List<string>();
        foreach (var item in sale.Items)
        {
            var exists = await db.InventoryMovements.AnyAsync(m =>
                m.CompanyId == sale.CompanyId && m.SaleId == sale.Id && m.Sku == item.Sku && m.Kind == InventoryMovementKinds.SalePaid, ct);
            if (exists) continue;

            var bal = await db.InventoryBalances.FirstOrDefaultAsync(b => b.CompanyId == sale.CompanyId && b.Sku == item.Sku, ct);
            var onHand = bal?.OnHand ?? 0;
            if (onHand < item.Quantity)
            {
                sale.StockShort = item.Sku;
                sale.Status = SaleStatuses.PendingPayment;
                await db.SaveChangesAsync(ct);
                return (false, "stock_short");
            }

            db.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = sale.CompanyId,
                Sku = item.Sku,
                Kind = InventoryMovementKinds.SalePaid,
                Quantity = -item.Quantity,
                SaleId = sale.Id
            });
            if (bal is null)
            {
                bal = new InventoryBalance { Id = Guid.NewGuid(), CompanyId = sale.CompanyId, Sku = item.Sku, OnHand = 0 };
                db.InventoryBalances.Add(bal);
            }
            bal.OnHand -= item.Quantity;
            changedSkus.Add(item.Sku);
        }
        sale.StockShort = null;
        sale.Status = SaleStatuses.Paid;
        sale.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var sku in changedSkus.Distinct())
            EnqueueStockPublish(sale.CompanyId, sku, sale.MarketplaceCode, sale.VendorUserId, sale.Id);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task ApplyInboundAsync(Guid companyId, string sku, decimal qty, string chave, int nItem, CancellationToken ct)
    {
        var dup = await db.InventoryMovements.AnyAsync(m =>
            m.CompanyId == companyId && m.ChaveAcesso == chave && m.NItem == nItem && m.Kind == InventoryMovementKinds.NfeInbound, ct);
        if (dup) return;

        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Sku = sku,
            Kind = InventoryMovementKinds.NfeInbound,
            Quantity = qty,
            ChaveAcesso = chave,
            NItem = nItem
        });
        var bal = await db.InventoryBalances.FirstOrDefaultAsync(b => b.CompanyId == companyId && b.Sku == sku, ct);
        if (bal is null)
        {
            db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = companyId, Sku = sku, OnHand = qty });
        }
        else bal.OnHand += qty;
        EnqueueStockPublish(companyId, sku, excludeMarketplace: null, excludeVendorUserId: null, saleId: null);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReverseSalePaidAsync(Sale sale, CancellationToken ct)
    {
        var reversed = new List<string>();
        foreach (var item in sale.Items)
        {
            var paid = await db.InventoryMovements.AnyAsync(m =>
                m.CompanyId == sale.CompanyId && m.SaleId == sale.Id && m.Sku == item.Sku && m.Kind == InventoryMovementKinds.SalePaid, ct);
            var already = await db.InventoryMovements.AnyAsync(m =>
                m.CompanyId == sale.CompanyId && m.SaleId == sale.Id && m.Sku == item.Sku && m.Kind == InventoryMovementKinds.SalePaidReversal, ct);
            if (!paid || already) continue;
            db.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = sale.CompanyId,
                Sku = item.Sku,
                Kind = InventoryMovementKinds.SalePaidReversal,
                Quantity = item.Quantity,
                SaleId = sale.Id
            });
            var bal = await db.InventoryBalances.FirstAsync(b => b.CompanyId == sale.CompanyId && b.Sku == item.Sku, ct);
            bal.OnHand += item.Quantity;
            reversed.Add(item.Sku);
        }
        foreach (var sku in reversed.Distinct())
            EnqueueStockPublish(sale.CompanyId, sku, sale.MarketplaceCode, sale.VendorUserId, sale.Id);
        await db.SaveChangesAsync(ct);
    }

    void EnqueueStockPublish(Guid companyId, string sku, string? excludeMarketplace, Guid? excludeVendorUserId, Guid? saleId)
    {
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = WorkKinds.StockPublish,
            PayloadJson = JsonSerializer.Serialize(new
            {
                sku,
                excludeMarketplace,
                excludeVendorUserId,
                saleId
            }, JsonOpts)
        });
    }
}
