using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Marketplaces;

namespace Vilmo.Services;

public sealed class StockPublishService(AppDbContext db, MarketplaceStockGateway gateway)
{
    public async Task HandleWorkAsync(WorkItem item, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(item.PayloadJson) ? "{}" : item.PayloadJson);
        var root = doc.RootElement;
        var sku = root.TryGetProperty("sku", out var skuEl) ? skuEl.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidOperationException("StockPublishMissingSku");
        var excludeMarketplace = root.TryGetProperty("excludeMarketplace", out var mpEl) ? mpEl.GetString() : null;
        Guid? excludeVendor = null;
        if (root.TryGetProperty("excludeVendorUserId", out var vEl))
        {
            var raw = vEl.ValueKind == JsonValueKind.String ? vEl.GetString() : vEl.ToString();
            if (Guid.TryParse(raw, out var parsed)) excludeVendor = parsed;
        }
        await PublishSkuAsync(item.CompanyId, sku, excludeMarketplace, excludeVendor, ct);
    }

    public async Task PublishSkuAsync(
        Guid companyId,
        string sku,
        string? excludeMarketplace,
        Guid? excludeVendorUserId,
        CancellationToken ct)
    {
        var onHand = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.Sku == sku)
            .Select(b => b.OnHand)
            .FirstOrDefaultAsync(ct);

        var listings = await db.Listings
            .Where(l => l.CompanyId == companyId && l.Sku == sku)
            .ToListAsync(ct);

        foreach (var listing in listings)
        {
            if (IsOriginListing(listing, excludeMarketplace, excludeVendorUserId))
                continue;
            await gateway.ApplyAsync(listing, onHand, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    static bool IsOriginListing(Listing listing, string? excludeMarketplace, Guid? excludeVendorUserId)
    {
        if (string.IsNullOrWhiteSpace(excludeMarketplace)) return false;
        if (!string.Equals(listing.MarketplaceCode, excludeMarketplace, StringComparison.OrdinalIgnoreCase))
            return false;
        if (excludeVendorUserId is null) return true;
        return listing.VendorUserId == excludeVendorUserId;
    }
}
