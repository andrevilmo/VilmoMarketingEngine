using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

public sealed class ProductService(AppDbContext db, AdvertisementService advertisements)
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
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            sku,
            kind = AdvertisementKinds.Product,
            marketplaceCodes = codes,
            vendorUserId,
            items = new[] { new { sku, quantity = 1m } }
        }));
        return await advertisements.PublishAsync(companyId, vendorUserId, actorIsVendor: false, doc.RootElement.Clone(), ct);
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
