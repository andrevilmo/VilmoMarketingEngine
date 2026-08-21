using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class AdvertisementService(AppDbContext db, ListingPublishLogService publishLogs, ListingPublishRunner publisher)
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
        var names = await MarketplaceNamesAsync(ct);
        var logMap = await publishLogs.ListForListingsAsync(ctx.RequireCompany(), listings.Select(l => l.Id).ToList(), ct);
        return ads.Select(a => Map(a, listings.Where(l => l.AdvertisementId == a.Id).ToList(), names, logMap)).ToList();
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
        var familyName = string.IsNullOrWhiteSpace(draft.FamilyName) ? title : draft.FamilyName.Trim();
        if (string.IsNullOrWhiteSpace(familyName))
            throw new ArgumentException("FamilyNameRequired");
        if (familyName.Length > 60)
            throw new ArgumentException("FamilyNameTooLong");
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
        ad.FamilyName = familyName;
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
            var value = attr.FieldValue ?? "";
            if (attr.MarketplaceCode.Equals("MercadoLivre", StringComparison.OrdinalIgnoreCase)
                && attr.FieldName.Equals("categoryId", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(value))
            {
                if (!MercadoLivreCategoryId.TryNormalize(value, out var mlId))
                    throw new ArgumentException("InvalidCategoryId");
                value = mlId;
            }
            db.AdvertisementAttributes.Add(new AdvertisementAttribute
            {
                Id = Guid.NewGuid(),
                AdvertisementId = ad.Id,
                MarketplaceCode = attr.MarketplaceCode,
                FieldName = attr.FieldName.Trim(),
                FieldValue = value
            });
        }

        var createdListings = new List<object>();
        foreach (var code in codes)
        {
            var listing = await EnsureListingAsync(companyId, vendorId, sku, code, ad.Id, ct);
            if (draft.EnqueuePublish)
            {
                QueuePublish(companyId, ad.Id, listing);
                ApplyPublishedSnapshot(listing, ad);
            }
            else if (listing.Status is ListingStatuses.Queued or ListingStatuses.Published or "PublishedDemo")
            {
                /* keep live channel */
            }
            else
                listing.Status = ListingStatuses.Draft;
            createdListings.Add(new { listing.Id, code, listing.Status });
            await publishLogs.WriteAsync(companyId, Guid.NewGuid(), ad.Id, listing.Id, code, "save",
                ListingPublishLogSteps.Saved, "info",
                draft.EnqueuePublish
                    ? "Anúncio salvo e publicação enfileirada neste canal."
                    : "Rascunho salvo neste canal. Use Publicar neste canal para enviar ao marketplace.",
                new { listing.Id, code, listing.Status, draft.EnqueuePublish }, ct);
        }
        await db.SaveChangesAsync(ct);
        return await GetMappedAsync(ad.Id, ct);
    }

    public async Task<object?> GetAsync(CompanyContext ctx, Guid advertisementId, CancellationToken ct)
    {
        try
        {
            await RequireAdAsync(ctx, advertisementId, ct);
            return await GetMappedAsync(advertisementId, ct);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public async Task<object> ProceedChannelAsync(CompanyContext ctx, Guid advertisementId, string marketplaceCode, CancellationToken ct)
    {
        var ad = await RequireAdAsync(ctx, advertisementId, includeItems: true, ct);
        var codes = await ResolveCodesAsync(ad.CompanyId, ad.VendorUserId, ctx.IsVendor, [marketplaceCode], ct);
        var code = codes[0];
        var listing = await EnsureListingAsync(ad.CompanyId, ad.VendorUserId, ad.Sku, code, ad.Id, ct);
        var runId = Guid.NewGuid();
        await publisher.PublishAsync(ad, listing, runId, ct);
        QueuePublish(ad.CompanyId, ad.Id, listing, overwriteStatus: false);
        await db.SaveChangesAsync(ct);
        return await GetMappedAsync(ad.Id, ct);
    }

    public async Task<object> CancelChannelAsync(CompanyContext ctx, Guid advertisementId, string marketplaceCode, CancellationToken ct)
    {
        var ad = await RequireAdAsync(ctx, advertisementId, includeItems: true, ct);
        var listing = await db.Listings.FirstOrDefaultAsync(l =>
            l.AdvertisementId == ad.Id && l.MarketplaceCode == marketplaceCode, ct)
            ?? throw new KeyNotFoundException("ListingNotFound");
        var runId = Guid.NewGuid();
        await publisher.CancelAsync(ad, listing, runId, ct);
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = ad.CompanyId,
            Kind = WorkKinds.ListingCancel,
            PayloadJson = $"{{\"listingId\":\"{listing.Id}\",\"advertisementId\":\"{ad.Id}\",\"runId\":\"{runId}\"}}"
        });
        await db.SaveChangesAsync(ct);
        return await GetMappedAsync(ad.Id, ct);
    }

    public async Task<object> RefreshOnlineAsync(CompanyContext ctx, Guid advertisementId, CancellationToken ct)
    {
        var ad = await RequireAdAsync(ctx, advertisementId, includeItems: true, ct);
        var listings = await db.Listings.Where(l => l.AdvertisementId == ad.Id).ToListAsync(ct);
        if (listings.Count == 0)
            throw new InvalidOperationException("NoChannels");
        var runId = Guid.NewGuid();
        foreach (var listing in listings)
            await publisher.RefreshAsync(ad, listing, runId, ct);
        await db.SaveChangesAsync(ct);
        return await GetMappedAsync(ad.Id, ct);
    }

    async Task<Advertisement> RequireAdAsync(CompanyContext ctx, Guid id, CancellationToken ct) =>
        await RequireAdAsync(ctx, id, includeItems: false, ct);

    async Task<Advertisement> RequireAdAsync(CompanyContext ctx, Guid id, bool includeItems, CancellationToken ct)
    {
        var q = db.Advertisements.Where(a => a.Id == id && a.CompanyId == ctx.CompanyId);
        if (includeItems) q = q.Include(a => a.Items).Include(a => a.Attributes);
        var ad = await q.FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("AdvertisementNotFound");
        if (ctx.IsVendor && ad.VendorUserId != ctx.UserId) throw new KeyNotFoundException("AdvertisementNotFound");
        return ad;
    }

    async Task<object> GetMappedAsync(Guid advertisementId, CancellationToken ct)
    {
        var loaded = await db.Advertisements.AsNoTracking().Include(a => a.Items).Include(a => a.Attributes)
            .FirstAsync(a => a.Id == advertisementId, ct);
        var listings = await db.Listings.AsNoTracking().Where(l => l.AdvertisementId == loaded.Id).ToListAsync(ct);
        var names = await MarketplaceNamesAsync(ct);
        var logMap = await publishLogs.ListForListingsAsync(loaded.CompanyId, listings.Select(l => l.Id).ToList(), ct);
        return new { advertisement = Map(loaded, listings, names, logMap) };
    }

    public async Task<object> ListChannelLogsAsync(CompanyContext ctx, Guid advertisementId, string marketplaceCode, CancellationToken ct)
    {
        var ad = await RequireAdAsync(ctx, advertisementId, ct);
        var listing = await db.Listings.AsNoTracking().FirstOrDefaultAsync(l =>
            l.AdvertisementId == ad.Id && l.MarketplaceCode == marketplaceCode, ct)
            ?? throw new KeyNotFoundException("ListingNotFound");
        var items = await publishLogs.ListAsync(ad.CompanyId, listing.Id, ct);
        return new { items = items.Select(ListingPublishLogService.Map).ToList() };
    }

    async Task<Dictionary<string, string>> MarketplaceNamesAsync(CancellationToken ct) =>
        await db.Marketplaces.AsNoTracking().ToDictionaryAsync(m => m.Code, m => m.DisplayName, ct);

    async Task<Listing> EnsureListingAsync(Guid companyId, Guid vendorId, string sku, string code, Guid advertisementId, CancellationToken ct)
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
                Status = ListingStatuses.Draft
            };
            db.Listings.Add(listing);
        }
        listing.AdvertisementId = advertisementId;
        return listing;
    }

    void QueuePublish(Guid companyId, Guid advertisementId, Listing listing, bool overwriteStatus = true)
    {
        if (overwriteStatus) listing.Status = ListingStatuses.Queued;
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = WorkKinds.PublishListing,
            PayloadJson = $"{{\"listingId\":\"{listing.Id}\",\"advertisementId\":\"{advertisementId}\"}}"
        });
    }

    static void ApplyPublishedSnapshot(Listing listing, Advertisement ad)
    {
        listing.Status = ListingStatuses.Published;
        listing.RemoteId ??= $"demo-{listing.Id:N}"[..12];
        listing.RemoteTitle = ad.Title;
        listing.RemotePrice = ad.Price;
        listing.RemoteQuantity = ad.AvailableQuantity;
        listing.RemoteStatus = "active";
        listing.RemotePermalink = $"https://demo.vilmomkt.com/{listing.MarketplaceCode}/{listing.RemoteId}";
        listing.LastSyncedAt = DateTimeOffset.UtcNow;
        listing.LastSyncJson = JsonSerializer.Serialize(new
        {
            source = "publish",
            marketplaceCode = listing.MarketplaceCode,
            fetchedAt = listing.LastSyncedAt,
            remoteId = listing.RemoteId,
            title = listing.RemoteTitle,
            price = listing.RemotePrice,
            quantity = listing.RemoteQuantity,
            status = listing.RemoteStatus,
            permalink = listing.RemotePermalink
        });
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
        if (requested is { Count: 0 })
            throw new ArgumentException("MarketplaceRequired");
        if (allowed.Count == 0)
            allowed = await db.Marketplaces.Where(m => m.IsActive).Select(m => m.Code).ToListAsync(ct);
        return allowed.Distinct().ToList();
    }

    static object Map(
        Advertisement a,
        List<Listing> listings,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<Guid, List<ListingPublishLog>> logs) => new
    {
        a.Id,
        a.CompanyId,
        a.VendorUserId,
        a.Kind,
        a.Sku,
        a.Title,
        a.FamilyName,
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
        channels = listings.Select(l => new
        {
            l.Id,
            l.MarketplaceCode,
            displayName = names.TryGetValue(l.MarketplaceCode, out var n) ? n : l.MarketplaceCode,
            l.Status,
            statusPt = ListingStatuses.Pt.GetValueOrDefault(l.Status, l.Status),
            l.RemoteId,
            l.RemoteTitle,
            l.RemotePrice,
            l.RemoteQuantity,
            l.RemoteStatus,
            l.RemotePermalink,
            l.LastSyncedAt,
            l.LastSyncJson,
            lastStep = (logs.TryGetValue(l.Id, out var chLogs) ? chLogs.FirstOrDefault() : null) is { } latest
                ? ListingPublishLogService.Map(latest) : null,
            publishLog = logs.TryGetValue(l.Id, out var steps)
                ? steps.Select(ListingPublishLogService.Map).ToList()
                : []
        })
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
            body.TryGetProperty("familyName", out var famEl) ? famEl.GetString() : null,
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
            body.TryGetProperty("enqueuePublish", out var enq) && enq.ValueKind is JsonValueKind.True,
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
        string? Sku, string Kind, string Title, string? FamilyName, string? Description, decimal? Price, string? Currency,
        decimal? AvailableQuantity, string? Condition, string? Brand, string? Gtin,
        decimal? WeightGrams, decimal? HeightCm, decimal? WidthCm, decimal? LengthCm,
        Guid? VendorUserId, List<string>? MarketplaceCodes, bool EnqueuePublish,
        List<AdvertisementItemDraft> Items, List<AdvertisementAttributeDraft> Attributes);

    sealed record AdvertisementItemDraft(string Sku, decimal Quantity);
    sealed record AdvertisementAttributeDraft(string MarketplaceCode, string FieldName, string FieldValue);
}
