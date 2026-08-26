using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

public sealed class ProductService(AppDbContext db)
{
    public async Task<List<Product>> ListAsync(Guid companyId, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.CompanyId == companyId).OrderBy(p => p.Sku).ToListAsync(ct);

    public async Task<Product> UpsertAsync(Guid companyId, ProductDraft draft, CancellationToken ct)
    {
        var sku = draft.Sku.Trim();
        var p = await db.Products.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Sku == sku, ct);
        if (p is null)
        {
            p = new Product { Id = Guid.NewGuid(), CompanyId = companyId, Sku = sku };
            db.Products.Add(p);
        }
        p.Name = draft.Name;
        p.Ean = draft.Ean;
        p.Ncm = draft.Ncm;
        p.Cfop = draft.Cfop;
        p.SalePrice = draft.SalePrice;
        p.Description = draft.Description;
        if (!await db.InventoryBalances.AnyAsync(b => b.CompanyId == companyId && b.Sku == sku, ct))
            db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = companyId, Sku = sku, OnHand = 0 });
        await db.SaveChangesAsync(ct);
        return p;
    }

    public async Task<object> PublishAsync(Guid companyId, Guid vendorUserId, string sku, IReadOnlyList<string>? codes, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.CompanyId == companyId && p.Sku == sku, ct))
            throw new KeyNotFoundException("ProductNotFound");
        var selected = codes is { Count: > 0 }
            ? codes.ToList()
            : await db.UserDetailMarketplaces.Where(v => v.CompanyId == companyId && v.UserId == vendorUserId)
                .Select(v => v.MarketplaceCode).ToListAsync(ct);
        var created = new List<object>();
        foreach (var code in selected.Distinct())
        {
            var listing = await db.Listings.FirstOrDefaultAsync(l =>
                l.CompanyId == companyId && l.VendorUserId == vendorUserId && l.Sku == sku && l.MarketplaceCode == code, ct);
            if (listing is null)
            {
                var onHand = await db.InventoryBalances.AsNoTracking()
                    .Where(b => b.CompanyId == companyId && b.Sku == sku)
                    .Select(b => (decimal?)b.OnHand)
                    .FirstOrDefaultAsync(ct);
                listing = new Listing
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    VendorUserId = vendorUserId,
                    Sku = sku,
                    MarketplaceCode = code,
                    Status = "Queued",
                    AvailableQuantity = onHand ?? 0
                };
                db.Listings.Add(listing);
            }
            db.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Kind = WorkKinds.PublishListing,
                PayloadJson = $"{{\"listingId\":\"{listing.Id}\"}}"
            });
            created.Add(new { listing.Id, code, listing.Status });
        }
        await db.SaveChangesAsync(ct);
        return created;
    }
}

public sealed class ProductDraft
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Ean { get; set; }
    public string? Ncm { get; set; }
    public string? Cfop { get; set; }
    public decimal SalePrice { get; set; }
    public string? Description { get; set; }
}
