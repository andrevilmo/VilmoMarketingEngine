using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class AdvertisementService(AppDbContext db)
{
    public async Task<object> ListAsync(CompanyContext ctx, CancellationToken ct)
    {
        var q = db.Advertisements.AsNoTracking()
            .Include(a => a.Items)
            .Include(a => a.Attributes)
            .Where(a => a.CompanyId == ctx.CompanyId);
        if (ctx.IsVendor) q = q.Where(a => a.VendorUserId == ctx.UserId);
        List<Advertisement> ads;
        if (db.Database.IsNpgsql())
        {
            ads = await q.OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync(ct);
        }
        else
        {
            ads = (await q.ToListAsync(ct))
                .OrderByDescending(a => a.CreatedAt)
                .Take(200)
                .ToList();
        }
        var ids = ads.Select(a => a.Id).ToList();
        var listings = await db.Listings.AsNoTracking()
            .Where(l => l.CompanyId == ctx.CompanyId && l.AdvertisementId != null && ids.Contains(l.AdvertisementId.Value))
            .ToListAsync(ct);
        return ads.Select(a => Map(a, listings.Where(l => l.AdvertisementId == a.Id).ToList())).ToList();
    }

    public async Task<object> ListingFieldsAsync(CancellationToken ct)
    {
        var defs = await db.MarketplaceListingFieldDefinitions.AsNoTracking()
            .OrderBy(d => d.SortOrder).ThenBy(d => d.FieldKey).ToListAsync(ct);
        var common = defs.Where(d => d.IsCommon).Select(MapField);
        var byMarketplace = defs.Where(d => !d.IsCommon)
            .GroupBy(d => d.MarketplaceCode)
            .ToDictionary(g => g.Key, g => g.Select(MapField).ToList());
        return new { common, byMarketplace };
    }

    public async Task<object> PublishAsync(Guid companyId, Guid actorUserId, bool actorIsVendor, JsonElement body, CancellationToken ct)
    {
        var draft = Parse(body);
        var vendorId = actorIsVendor
            ? actorUserId
            : draft.VendorUserId ?? actorUserId;
        var items = draft.Items;
        if (items.Count == 0 && !string.IsNullOrWhiteSpace(draft.Sku))
            items = [new AdvertisementItemDraft(draft.Sku.Trim(), 1)];
        if (items.Count == 0)
            throw new ArgumentException("ItemsRequired");
        if (items.Any(i => string.IsNullOrWhiteSpace(i.Sku) || i.Quantity <= 0))
            throw new ArgumentException("InvalidItem");
        if (items.GroupBy(i => i.Sku, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException("DuplicateSku");

        var skus = items.Select(i => i.Sku).ToList();
        var products = await db.Products.Where(p => p.CompanyId == companyId && skus.Contains(p.Sku)).ToListAsync(ct);
        if (products.Count != skus.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new KeyNotFoundException("ProductNotFound");

        var kind = draft.Kind;
        if (!string.IsNullOrWhiteSpace(kind) && kind is not (AdvertisementKinds.Product or AdvertisementKinds.Kit))
            throw new ArgumentException("InvalidKind");
        if (items.Count > 1 || items[0].Quantity > 1)
            kind = AdvertisementKinds.Kit;
        else if (string.IsNullOrWhiteSpace(kind))
            kind = AdvertisementKinds.Product;

        var sku = string.IsNullOrWhiteSpace(draft.Sku)
            ? (kind == AdvertisementKinds.Kit ? $"KIT-{Guid.NewGuid():N}"[..12].ToUpperInvariant() : items[0].Sku)
            : draft.Sku.Trim();
        var primary = products.First(p => p.Sku.Equals(items[0].Sku, StringComparison.OrdinalIgnoreCase));
        var title = string.IsNullOrWhiteSpace(draft.Title) ? primary.Name : draft.Title.Trim();
        var price = draft.Price ?? primary.SalePrice;
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("TitleRequired");
        if (price < 0)
            throw new ArgumentException("InvalidPrice");
        var available = draft.AvailableQuantity ?? 1;
        if (available <= 0)
            throw new ArgumentException("InvalidAvailableQuantity");

        var codes = await ResolveCodesAsync(companyId, vendorId, actorIsVendor, draft.MarketplaceCodes, ct);
        var existing = await db.Advertisements
            .Include(a => a.Items)
            .Include(a => a.Attributes)
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.VendorUserId == vendorId && a.Sku == sku, ct);
        Advertisement ad;
        if (existing is null)
        {
            ad = new Advertisement
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                VendorUserId = vendorId,
                Sku = sku
            };
            db.Advertisements.Add(ad);
        }
        else
        {
            ad = existing;
            db.AdvertisementItems.RemoveRange(ad.Items);
            db.AdvertisementAttributes.RemoveRange(ad.Attributes);
        }

        ad.Kind = kind;
        ad.Title = title;
        ad.Description = draft.Description;
        ad.Price = price;
        ad.Currency = string.IsNullOrWhiteSpace(draft.Currency) ? "BRL" : draft.Currency;
        ad.AvailableQuantity = available;
        ad.Condition = string.IsNullOrWhiteSpace(draft.Condition) ? "new" : draft.Condition;
        ad.Brand = draft.Brand;
        ad.Gtin = draft.Gtin ?? primary.Ean;
        ad.WeightGrams = draft.WeightGrams;
        ad.HeightCm = draft.HeightCm;
        ad.WidthCm = draft.WidthCm;
        ad.LengthCm = draft.LengthCm;
        var order = 0;
        foreach (var item in items)
        {
            db.AdvertisementItems.Add(new AdvertisementItem
            {
                Id = Guid.NewGuid(),
                AdvertisementId = ad.Id,
                Sku = item.Sku,
                Quantity = item.Quantity,
                SortOrder = order++
            });
        }
        foreach (var attr in draft.Attributes)
        {
            if (string.IsNullOrWhiteSpace(attr.FieldName)) continue;
            db.AdvertisementAttributes.Add(new AdvertisementAttribute
            {
                Id = Guid.NewGuid(),
                AdvertisementId = ad.Id,
                MarketplaceCode = attr.MarketplaceCode,
                FieldName = attr.FieldName.Trim(),
                FieldValue = attr.FieldValue ?? ""
            });
        }

        var createdListings = new List<object>();
        foreach (var code in codes)
        {
            var listing = await db.Listings.FirstOrDefaultAsync(l =>
                l.CompanyId == companyId && l.VendorUserId == vendorId && l.Sku == sku && l.MarketplaceCode == code, ct);
            if (listing is null)
            {
                listing = new Listing
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    VendorUserId = vendorId,
                    Sku = sku,
                    MarketplaceCode = code,
                    Status = "Queued"
                };
                db.Listings.Add(listing);
            }
            listing.AdvertisementId = ad.Id;
            listing.Status = "Queued";
            db.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Kind = WorkKinds.PublishListing,
                PayloadJson = $"{{\"listingId\":\"{listing.Id}\",\"advertisementId\":\"{ad.Id}\"}}"
            });
            createdListings.Add(new { listing.Id, code, listing.Status });
        }
        await db.SaveChangesAsync(ct);
        var listings = await db.Listings.AsNoTracking().Where(l => l.AdvertisementId == ad.Id).ToListAsync(ct);
        var loaded = await db.Advertisements.AsNoTracking().Include(a => a.Items).Include(a => a.Attributes)
            .FirstAsync(a => a.Id == ad.Id, ct);
        return new { advertisement = Map(loaded, listings), listings = createdListings };
    }

    async Task<List<string>> ResolveCodesAsync(Guid companyId, Guid vendorId, bool actorIsVendor, IReadOnlyList<string>? requested, CancellationToken ct)
    {
        List<string> allowed;
        if (actorIsVendor)
        {
            allowed = await db.UserDetailMarketplaces
                .Where(v => v.CompanyId == companyId && v.UserId == vendorId)
                .Select(v => v.MarketplaceCode).ToListAsync(ct);
        }
        else
        {
            allowed = await db.CompanyMarketplaceConfigs
                .Where(c => c.CompanyId == companyId && c.IsEnabled)
                .Select(c => c.MarketplaceCode).ToListAsync(ct);
            if (allowed.Count == 0)
                allowed = await db.Marketplaces.Where(m => m.IsActive).Select(m => m.Code).ToListAsync(ct);
        }
        if (requested is { Count: > 0 })
        {
            var unknown = requested.Except(allowed, StringComparer.OrdinalIgnoreCase).ToList();
            if (unknown.Count > 0) throw new ArgumentException("UnknownMarketplace");
            return requested.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        if (allowed.Count == 0)
            allowed = await db.Marketplaces.Where(m => m.IsActive).Select(m => m.Code).ToListAsync(ct);
        return allowed.Distinct().ToList();
    }

    static object Map(Advertisement a, List<Listing> listings) => new
    {
        a.Id,
        a.CompanyId,
        a.VendorUserId,
        a.Kind,
        a.Sku,
        a.Title,
        a.Description,
        a.Price,
        a.Currency,
        a.AvailableQuantity,
        a.Condition,
        a.Brand,
        a.Gtin,
        a.WeightGrams,
        a.HeightCm,
        a.WidthCm,
        a.LengthCm,
        a.CreatedAt,
        items = a.Items.OrderBy(i => i.SortOrder).Select(i => new { i.Sku, i.Quantity }),
        attributes = a.Attributes.Select(x => new { x.MarketplaceCode, x.FieldName, x.FieldValue }),
        channels = listings.Select(l => new { l.MarketplaceCode, l.Status, l.RemoteId })
    };

    static object MapField(MarketplaceListingFieldDefinition d) => new
    {
        d.MarketplaceCode,
        d.FieldKey,
        d.Label,
        d.ValueKind,
        d.IsCommon,
        d.Required
    };

    static AdvertisementParse Parse(JsonElement body)
    {
        var items = new List<AdvertisementItemDraft>();
        if (body.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in arr.EnumerateArray())
            {
                var sku = x.TryGetProperty("sku", out var s) ? s.GetString() ?? "" : "";
                var qty = 1m;
                if (x.TryGetProperty("quantity", out var q))
                {
                    if (q.ValueKind == JsonValueKind.Number) qty = q.GetDecimal();
                    else decimal.TryParse(q.GetString(), out qty);
                }
                items.Add(new AdvertisementItemDraft(sku.Trim(), qty));
            }
        }
        var attrs = new List<AdvertisementAttributeDraft>();
        if (body.TryGetProperty("attributes", out var at) && at.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in at.EnumerateArray())
            {
                attrs.Add(new AdvertisementAttributeDraft(
                    x.TryGetProperty("marketplaceCode", out var mc) ? mc.GetString() ?? "" : "",
                    x.TryGetProperty("fieldName", out var fn) ? fn.GetString() ?? "" : "",
                    x.TryGetProperty("fieldValue", out var fv) ? fv.GetString() ?? "" : ""
                ));
            }
        }
        List<string>? codes = null;
        if (body.TryGetProperty("marketplaceCodes", out var codesEl) && codesEl.ValueKind == JsonValueKind.Array)
            codes = codesEl.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "").ToList();
        Guid? vendor = null;
        if (body.TryGetProperty("vendorUserId", out var v))
        {
            var raw = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText().Trim('"');
            if (Guid.TryParse(raw, out var vid)) vendor = vid;
        }
        return new AdvertisementParse(
            body.TryGetProperty("sku", out var skuEl) ? skuEl.GetString() : null,
            body.TryGetProperty("kind", out var kindEl) ? kindEl.GetString() ?? "" : "",
            body.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "",
            body.TryGetProperty("description", out var descEl) ? descEl.GetString() : null,
            Num(body, "price"),
            body.TryGetProperty("currency", out var curEl) ? curEl.GetString() : "BRL",
            Num(body, "availableQuantity"),
            body.TryGetProperty("condition", out var condEl) ? condEl.GetString() : "new",
            body.TryGetProperty("brand", out var brandEl) ? brandEl.GetString() : null,
            body.TryGetProperty("gtin", out var gtinEl) ? gtinEl.GetString() : null,
            Num(body, "weightGrams"),
            Num(body, "heightCm"),
            Num(body, "widthCm"),
            Num(body, "lengthCm"),
            vendor,
            codes,
            items,
            attrs
        );
    }

    static decimal? Num(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (el.ValueKind == JsonValueKind.Number) return el.GetDecimal();
        return decimal.TryParse(el.GetString(), out var n) ? n : null;
    }

    sealed record AdvertisementParse(
        string? Sku, string Kind, string Title, string? Description, decimal? Price, string? Currency,
        decimal? AvailableQuantity, string? Condition, string? Brand, string? Gtin,
        decimal? WeightGrams, decimal? HeightCm, decimal? WidthCm, decimal? LengthCm,
        Guid? VendorUserId, List<string>? MarketplaceCodes,
        List<AdvertisementItemDraft> Items, List<AdvertisementAttributeDraft> Attributes);

    sealed record AdvertisementItemDraft(string Sku, decimal Quantity);
    sealed record AdvertisementAttributeDraft(string MarketplaceCode, string FieldName, string FieldValue);
}
