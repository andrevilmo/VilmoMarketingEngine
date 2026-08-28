using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Marketplaces;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class ListingImportService(
    AppDbContext db,
    ListingImportLogService logs,
    SecretProtector protector,
    IEnumerable<IMarketplaceListingCatalogAdapter> adapters,
    IHttpClientFactory httpFactory)
{
    static readonly Regex NonDigit = new("[^0-9]", RegexOptions.Compiled);

    public async Task<object> EnqueueAsync(CompanyContext ctx, string marketplaceCode, Guid? vendorUserId, CancellationToken ct)
    {
        var companyId = ctx.RequireCompany();
        var code = string.IsNullOrWhiteSpace(marketplaceCode) ? MercadoLivreListingCatalogAdapter.Code : marketplaceCode.Trim();
        var vendorId = ctx.IsVendor ? ctx.UserId : vendorUserId ?? ctx.UserId;
        var runId = Guid.NewGuid();

        await logs.WriteAsync(companyId, runId, code, ListingImportLogSteps.Received, "info",
            "Pedido de importação recebido.",
            new { marketplaceCode = code, vendorUserId = vendorId }, null, ct);

        var adapter = Adapter(code);
        if (adapter is null)
        {
            await logs.WriteAsync(companyId, runId, code, ListingImportLogSteps.Failed, "error",
                "Este marketplace ainda não importa anúncios. Começamos pelo Mercado Livre.",
                new { error = "AdapterNotFound" }, null, ct);
            throw new ArgumentException("ImportNotSupported");
        }

        var gate = await ReadCredentialsAsync(companyId, code, ct);
        if (gate is null)
        {
            await logs.WriteAsync(companyId, runId, code, ListingImportLogSteps.NotLinked, "error",
                "Conecte o Mercado Livre em Marketplaces antes de importar.",
                new { error = "MarketplaceNotLinked" }, null, ct);
            throw new InvalidOperationException("MarketplaceNotLinked");
        }
        if (IsDemoToken(gate.Value.Token))
        {
            await logs.WriteAsync(companyId, runId, code, ListingImportLogSteps.DemoToken, "error",
                "O token deste canal é de demonstração. Reconecte o Mercado Livre com OAuth real para importar os anúncios da conta.",
                new { error = "DemoToken" }, null, ct);
            throw new InvalidOperationException("DemoToken");
        }

        var workId = Guid.NewGuid();
        db.WorkItems.Add(new WorkItem
        {
            Id = workId,
            CompanyId = companyId,
            Kind = WorkKinds.ListingImport,
            PayloadJson = JsonSerializer.Serialize(new
            {
                runId,
                marketplaceCode = code,
                vendorUserId = vendorId,
                actorUserId = ctx.UserId
            })
        });
        await db.SaveChangesAsync(ct);
        await logs.WriteAsync(companyId, runId, code, ListingImportLogSteps.Queued, "info",
            "A importação entrou na fila. Os anúncios do Mercado Livre aparecem abaixo em instantes.",
            new { workItemId = workId }, null, ct);
        return new { runId, status = "Queued", marketplaceCode = code };
    }

    public async Task ProcessQueuedAsync(WorkItem work, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(string.IsNullOrWhiteSpace(work.PayloadJson) ? "{}" : work.PayloadJson);
        var root = payload.RootElement;
        var runId = Guid.TryParse(Str(root, "runId"), out var rid) ? rid : work.Id;
        var code = Str(root, "marketplaceCode") ?? MercadoLivreListingCatalogAdapter.Code;
        var vendorId = Guid.TryParse(Str(root, "vendorUserId"), out var vid) ? vid : Guid.Empty;

        await logs.WriteAsync(work.CompanyId, runId, code, ListingImportLogSteps.WorkerStarted, "info",
            "O serviço começou a buscar os anúncios no Mercado Livre.",
            new { workItemId = work.Id }, null, ct);

        var adapter = Adapter(code) ?? throw new InvalidOperationException("AdapterNotFound");
        var gate = await ReadCredentialsAsync(work.CompanyId, code, ct)
            ?? throw new InvalidOperationException("MarketplaceNotLinked");
        if (IsDemoToken(gate.Token))
            throw new InvalidOperationException("DemoToken");

        var marketplace = await db.Marketplaces.AsNoTracking().FirstOrDefaultAsync(m => m.Code == code, ct);
        var catalog = new MarketplaceCatalogContext(
            work.CompanyId,
            gate.Token,
            string.IsNullOrWhiteSpace(marketplace?.BaseUrl) ? "https://api.mercadolibre.com" : marketplace.BaseUrl,
            gate.SellerId);

        await logs.WriteAsync(work.CompanyId, runId, code, ListingImportLogSteps.Scanning, "info",
            "Consultando a lista de anúncios no Mercado Livre.",
            new { sellerId = catalog.SellerId }, null, ct);

        IReadOnlyList<RemoteMarketplaceAd> remote;
        try
        {
            remote = await adapter.ListAdsAsync(catalog, ct);
        }
        catch (MarketplaceHttpException ex) when (ex.StatusCode == 401)
        {
            await logs.WriteAsync(work.CompanyId, runId, code, ListingImportLogSteps.Scanning, "warning",
                "Token expirado. Tentando renovar o acesso ao Mercado Livre.",
                new { httpStatus = ex.StatusCode, path = ex.Path }, null, ct);
            var refreshed = await TryRefreshMercadoLivreTokenAsync(work.CompanyId, ct);
            if (string.IsNullOrWhiteSpace(refreshed))
                throw;
            catalog = catalog with { AccessToken = refreshed };
            remote = await adapter.ListAdsAsync(catalog, ct);
        }
        var products = await db.Products.AsNoTracking().Where(p => p.CompanyId == work.CompanyId).ToListAsync(ct);
        var ads = await db.Advertisements.AsNoTracking()
            .Where(a => a.CompanyId == work.CompanyId && a.VendorUserId == vendorId)
            .ToListAsync(ct);
        var listings = await db.Listings.AsNoTracking()
            .Where(l => l.CompanyId == work.CompanyId && l.MarketplaceCode == code)
            .ToListAsync(ct);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RemoteAdMatchStatuses.AlreadyLinked] = 0,
            [RemoteAdMatchStatuses.Suggested] = 0,
            [RemoteAdMatchStatuses.Unmatched] = 0,
            [RemoteAdMatchStatuses.Linked] = 0,
            [RemoteAdMatchStatuses.Ignored] = 0
        };

        foreach (var item in remote)
        {
            var row = await db.MarketplaceRemoteAds.FirstOrDefaultAsync(x =>
                x.CompanyId == work.CompanyId && x.MarketplaceCode == code && x.RemoteId == item.RemoteId, ct);
            if (row is null)
            {
                row = new MarketplaceRemoteAd
                {
                    Id = Guid.NewGuid(),
                    CompanyId = work.CompanyId,
                    VendorUserId = vendorId,
                    MarketplaceCode = code,
                    RemoteId = item.RemoteId,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.MarketplaceRemoteAds.Add(row);
            }

            ApplySnapshot(row, item, runId, vendorId);
            if (row.MatchStatus is RemoteAdMatchStatuses.Linked or RemoteAdMatchStatuses.Ignored)
            {
                counts[row.MatchStatus] = counts.GetValueOrDefault(row.MatchStatus) + 1;
                continue;
            }

            var match = Classify(item, products, ads, listings);
            row.MatchStatus = match.Status;
            row.SuggestedSku = match.Sku;
            row.SuggestedReason = match.Reason;
            row.AdvertisementId = match.AdvertisementId ?? row.AdvertisementId;
            counts[match.Status] = counts.GetValueOrDefault(match.Status) + 1;
        }

        await db.SaveChangesAsync(ct);
        await logs.WriteAsync(work.CompanyId, runId, code, ListingImportLogSteps.Classified, "info",
            $"Encontramos {remote.Count} anúncio(s). Já vinculados: {counts[RemoteAdMatchStatuses.AlreadyLinked]}. Com sugestão: {counts[RemoteAdMatchStatuses.Suggested]}. Pendentes: {counts[RemoteAdMatchStatuses.Unmatched]}.",
            new { fetched = remote.Count, counts }, null, ct);
        await logs.WriteAsync(work.CompanyId, runId, code, ListingImportLogSteps.Done, "info",
            "Importação concluída. Vincule os pendentes a um SKU do estoque. A quantidade do Mercado Livre não altera o estoque local.",
            new { fetched = remote.Count, counts }, null, ct);
        work.Status = "Done";
    }

    public async Task LogWorkFailureAsync(WorkItem work, Exception ex, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(string.IsNullOrWhiteSpace(work.PayloadJson) ? "{}" : work.PayloadJson);
        var runId = Guid.TryParse(Str(payload.RootElement, "runId"), out var rid) ? rid : work.Id;
        var code = Str(payload.RootElement, "marketplaceCode") ?? MercadoLivreListingCatalogAdapter.Code;
        var msg = UserMessage(ex);
        await logs.WriteAsync(work.CompanyId, runId, code, ListingImportLogSteps.Failed, "error",
            msg,
            new { error = ex.GetType().Name, message = ex.Message, workItemId = work.Id, httpStatus = (ex as MarketplaceHttpException)?.StatusCode }, null, ct);
    }

    public async Task<object> ListAsync(CompanyContext ctx, string? marketplaceCode, string? matchStatus, Guid? runId, CancellationToken ct)
    {
        var companyId = ctx.RequireCompany();
        var q = db.MarketplaceRemoteAds.AsNoTracking().Where(x => x.CompanyId == companyId);
        if (ctx.IsVendor) q = q.Where(x => x.VendorUserId == ctx.UserId);
        if (!string.IsNullOrWhiteSpace(marketplaceCode)) q = q.Where(x => x.MarketplaceCode == marketplaceCode);
        if (!string.IsNullOrWhiteSpace(matchStatus))
        {
            var statuses = matchStatus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            q = q.Where(x => statuses.Contains(x.MatchStatus));
        }
        if (runId is Guid rid) q = q.Where(x => x.RunId == rid);
        List<MarketplaceRemoteAd> rows;
        if (db.Database.IsNpgsql())
            rows = await q.OrderBy(x => x.MatchStatus).ThenBy(x => x.Title).Take(400).ToListAsync(ct);
        else
            rows = (await q.ToListAsync(ct)).OrderBy(x => x.MatchStatus).ThenBy(x => x.Title).Take(400).ToList();

        var all = db.MarketplaceRemoteAds.AsNoTracking().Where(x => x.CompanyId == companyId);
        if (ctx.IsVendor) all = all.Where(x => x.VendorUserId == ctx.UserId);
        if (!string.IsNullOrWhiteSpace(marketplaceCode)) all = all.Where(x => x.MarketplaceCode == marketplaceCode);
        var grouped = await all.GroupBy(x => x.MatchStatus).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var counts = grouped.ToDictionary(x => x.Key, x => x.Count, StringComparer.Ordinal);
        return new
        {
            items = rows.Select(Map),
            counts = new
            {
                alreadyLinked = counts.GetValueOrDefault(RemoteAdMatchStatuses.AlreadyLinked),
                suggested = counts.GetValueOrDefault(RemoteAdMatchStatuses.Suggested),
                unmatched = counts.GetValueOrDefault(RemoteAdMatchStatuses.Unmatched),
                linked = counts.GetValueOrDefault(RemoteAdMatchStatuses.Linked),
                ignored = counts.GetValueOrDefault(RemoteAdMatchStatuses.Ignored),
                total = counts.Values.Sum()
            }
        };
    }

    public async Task<object> ListLogsAsync(CompanyContext ctx, Guid? runId, int? limit, CancellationToken ct)
    {
        var companyId = ctx.RequireCompany();
        var take = Math.Clamp(limit ?? 80, 1, 200);
        var q = db.ListingImportLogs.AsNoTracking().Where(l => l.CompanyId == companyId);
        if (runId is Guid rid) q = q.Where(l => l.RunId == rid);
        List<ListingImportLog> items;
        if (db.Database.IsNpgsql())
            items = await q.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Take(take).ToListAsync(ct);
        else
            items = (await q.ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Take(take).ToList();
        return new
        {
            items = items.Select(l => new
            {
                l.Id,
                l.RunId,
                l.MarketplaceCode,
                l.RemoteId,
                l.StepCode,
                l.Level,
                l.UserMessage,
                l.TechnicalJson,
                l.CreatedAt
            })
        };
    }

    public async Task<object> LinkAsync(CompanyContext ctx, Guid remoteAdId, string skuRaw, CancellationToken ct)
    {
        var companyId = ctx.RequireCompany();
        var sku = (skuRaw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SkuRequired");
        var row = await db.MarketplaceRemoteAds.FirstOrDefaultAsync(x => x.Id == remoteAdId && x.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("RemoteAdNotFound");
        if (ctx.IsVendor && row.VendorUserId != ctx.UserId) throw new KeyNotFoundException("RemoteAdNotFound");
        if (row.MatchStatus is RemoteAdMatchStatuses.Linked or RemoteAdMatchStatuses.AlreadyLinked)
            throw new InvalidOperationException("AlreadyLinked");
        var product = await db.Products.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Sku == sku, ct)
            ?? throw new KeyNotFoundException("ProductNotFound");

        var other = await db.Listings.FirstOrDefaultAsync(l =>
            l.CompanyId == companyId && l.MarketplaceCode == row.MarketplaceCode && l.RemoteId == row.RemoteId, ct);
        if (other is not null && !string.Equals(other.Sku, sku, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("RemoteIdAlreadyLinked");

        var listing = await db.Listings.FirstOrDefaultAsync(l =>
            l.CompanyId == companyId && l.VendorUserId == row.VendorUserId
            && l.Sku == sku && l.MarketplaceCode == row.MarketplaceCode, ct);
        if (listing is not null
            && !string.IsNullOrWhiteSpace(listing.RemoteId)
            && !listing.RemoteId.StartsWith("demo-", StringComparison.OrdinalIgnoreCase)
            && !listing.RemoteId.Equals(row.RemoteId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SkuAlreadyLinked");

        var ad = await db.Advertisements
            .Include(a => a.Items)
            .Include(a => a.Attributes)
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.VendorUserId == row.VendorUserId && a.Sku == sku, ct);
        if (ad is null)
        {
            ad = new Advertisement
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                VendorUserId = row.VendorUserId,
                Kind = AdvertisementKinds.Product,
                Sku = sku,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Advertisements.Add(ad);
            db.AdvertisementItems.Add(new AdvertisementItem
            {
                Id = Guid.NewGuid(),
                AdvertisementId = ad.Id,
                Sku = sku,
                Quantity = 1,
                SortOrder = 0
            });
        }

        ad.Title = string.IsNullOrWhiteSpace(row.Title) ? product.Name : row.Title;
        if (string.IsNullOrWhiteSpace(ad.FamilyName))
            ad.FamilyName = ad.Title.Length > 60 ? ad.Title[..60] : ad.Title;
        if (row.Price is { } price && price >= 0) ad.Price = price;
        else if (ad.Price <= 0) ad.Price = product.SalePrice;
        if (row.Quantity is { } qty && qty > 0) ad.AvailableQuantity = qty;
        if (string.IsNullOrWhiteSpace(ad.Gtin)) ad.Gtin = row.Gtin ?? product.Ean;
        if (string.IsNullOrWhiteSpace(ad.Description)) ad.Description = row.Title;

        UpsertAttribute(ad, row.MarketplaceCode, "categoryId", row.CategoryId);
        UpsertAttribute(ad, row.MarketplaceCode, "listingTypeId", row.ListingTypeId);
        UpsertAttribute(ad, row.MarketplaceCode, "buyingMode", row.BuyingMode);
        UpsertAttribute(ad, row.MarketplaceCode, "shippingMode", row.ShippingMode);
        var pics = ParsePictures(row.PicturesJson);
        if (pics.Count > 0)
            UpsertAttribute(ad, row.MarketplaceCode, "pictures", MercadoLivrePictures.Serialize(pics));

        if (listing is null)
        {
            listing = new Listing
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                VendorUserId = row.VendorUserId,
                Sku = sku,
                MarketplaceCode = row.MarketplaceCode,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Listings.Add(listing);
        }
        listing.AdvertisementId = ad.Id;
        listing.Sku = sku;
        listing.Status = ListingStatuses.Published;
        listing.RemoteId = row.RemoteId;
        listing.RemoteTitle = row.Title;
        listing.RemotePrice = row.Price;
        listing.RemoteQuantity = row.Quantity;
        listing.RemoteStatus = row.RemoteStatus;
        listing.RemotePermalink = row.Permalink;
        listing.LastSyncedAt = DateTimeOffset.UtcNow;
        listing.LastSyncJson = row.SnapshotJson;

        row.MatchStatus = RemoteAdMatchStatuses.Linked;
        row.SuggestedSku = sku;
        row.SuggestedReason = "manual";
        row.AdvertisementId = ad.Id;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return new
        {
            remoteAd = Map(row),
            advertisementId = ad.Id,
            listingId = listing.Id,
            stockUnchanged = true
        };
    }

    public async Task<object> IgnoreAsync(CompanyContext ctx, Guid remoteAdId, CancellationToken ct)
    {
        var companyId = ctx.RequireCompany();
        var row = await db.MarketplaceRemoteAds.FirstOrDefaultAsync(x => x.Id == remoteAdId && x.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("RemoteAdNotFound");
        if (ctx.IsVendor && row.VendorUserId != ctx.UserId) throw new KeyNotFoundException("RemoteAdNotFound");
        if (row.MatchStatus is RemoteAdMatchStatuses.Linked or RemoteAdMatchStatuses.AlreadyLinked)
            throw new InvalidOperationException("AlreadyLinked");
        row.MatchStatus = RemoteAdMatchStatuses.Ignored;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    IMarketplaceListingCatalogAdapter? Adapter(string code) =>
        adapters.FirstOrDefault(a => a.MarketplaceCode.Equals(code, StringComparison.OrdinalIgnoreCase));

    async Task<(string Token, string? SellerId)?> ReadCredentialsAsync(Guid companyId, string code, CancellationToken ct)
    {
        var cfg = await db.CompanyMarketplaceConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == code, ct);
        if (cfg is not { IsEnabled: true, LinkStatus: LinkStatuses.Linked }) return null;
        var tokenRow = await db.CompanyMarketplaceParameters.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == "AccessToken", ct);
        if (tokenRow is null || string.IsNullOrWhiteSpace(tokenRow.ParameterValue)) return null;
        var token = tokenRow.ParameterValue;
        if (tokenRow.IsSecret)
        {
            try { token = protector.Unprotect(token); }
            catch { /* packed value */ }
        }
        var userRow = await db.CompanyMarketplaceParameters.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == "UserId", ct);
        return (token, userRow?.ParameterValue);
    }

    static bool IsDemoToken(string token) =>
        token.Contains("demo", StringComparison.OrdinalIgnoreCase);

    static string UserMessage(Exception ex) =>
        ex is MarketplaceHttpException http
            ? (http.StatusCode == 401
                ? "Token do Mercado Livre inválido ou expirado. Reconecte o canal em Marketplaces e importe de novo."
                : MercadoLivreSellerListing.UserMessageFromHttp(http.StatusCode, http.Body))
            : "A importação falhou. Tente de novo em instantes.";

    async Task<string?> TryRefreshMercadoLivreTokenAsync(Guid companyId, CancellationToken ct)
    {
        var cfg = await db.CompanyMarketplaceConfigs
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == MercadoLivreListingCatalogAdapter.Code, ct);
        if (cfg is null) return null;
        var refresh = await ReadParamAsync(cfg.Id, "RefreshToken", ct);
        var clientId = await ReadParamAsync(cfg.Id, "ClientId", ct);
        var clientSecret = await ReadParamAsync(cfg.Id, "ClientSecret", ct);
        if (string.IsNullOrWhiteSpace(refresh) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return null;
        if (refresh.Contains("demo", StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            var client = httpFactory.CreateClient("marketplace");
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadolibre.com/oauth/token");
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refresh
            });
            using var resp = await client.SendAsync(req, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            var access = doc.RootElement.TryGetProperty("access_token", out var a) ? a.GetString() : null;
            if (string.IsNullOrWhiteSpace(access)) return null;
            var newRefresh = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
            await UpsertParamAsync(cfg.Id, "AccessToken", access, true, ct);
            if (!string.IsNullOrWhiteSpace(newRefresh))
                await UpsertParamAsync(cfg.Id, "RefreshToken", newRefresh, true, ct);
            return access;
        }
        catch
        {
            return null;
        }
    }

    async Task<string?> ReadParamAsync(Guid configId, string key, CancellationToken ct)
    {
        var row = await db.CompanyMarketplaceParameters
            .FirstOrDefaultAsync(p => p.ConfigId == configId && p.ParameterKey == key, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.ParameterValue)) return null;
        if (!row.IsSecret) return row.ParameterValue;
        try { return protector.Unprotect(row.ParameterValue); }
        catch { return row.ParameterValue; }
    }

    async Task UpsertParamAsync(Guid configId, string key, string value, bool secret, CancellationToken ct)
    {
        var stored = secret ? protector.Protect(value) : value;
        var row = await db.CompanyMarketplaceParameters
            .FirstOrDefaultAsync(p => p.ConfigId == configId && p.ParameterKey == key, ct);
        if (row is null)
        {
            db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
            {
                Id = Guid.NewGuid(),
                ConfigId = configId,
                ParameterKey = key,
                ParameterValue = stored,
                IsSecret = secret
            });
        }
        else
        {
            row.ParameterValue = stored;
            row.IsSecret = secret;
        }
        await db.SaveChangesAsync(ct);
    }

    static void ApplySnapshot(MarketplaceRemoteAd row, RemoteMarketplaceAd item, Guid runId, Guid vendorId)
    {
        row.RunId = runId;
        row.VendorUserId = vendorId;
        row.Title = item.Title;
        row.Price = item.Price;
        row.Quantity = item.Quantity;
        row.RemoteStatus = item.Status;
        row.Permalink = item.Permalink;
        row.Thumbnail = item.Thumbnail;
        row.SellerCustomField = item.SellerCustomField;
        row.Gtin = item.Gtin;
        row.CategoryId = item.CategoryId;
        row.ListingTypeId = item.ListingTypeId;
        row.PicturesJson = JsonSerializer.Serialize(item.PictureUrls);
        row.BuyingMode = item.BuyingMode;
        row.ShippingMode = item.ShippingMode;
        row.SnapshotJson = item.SnapshotJson;
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static (string Status, string? Sku, string? Reason, Guid? AdvertisementId) Classify(
        RemoteMarketplaceAd item,
        IReadOnlyList<Product> products,
        IReadOnlyList<Advertisement> ads,
        IReadOnlyList<Listing> listings)
    {
        var existing = listings.FirstOrDefault(l =>
            !string.IsNullOrWhiteSpace(l.RemoteId)
            && l.RemoteId.Equals(item.RemoteId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return (RemoteAdMatchStatuses.AlreadyLinked, existing.Sku, "remote_id", existing.AdvertisementId);

        var skuHint = (item.SellerCustomField ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(skuHint) && !skuHint.StartsWith("MLB", StringComparison.OrdinalIgnoreCase))
        {
            var product = products.FirstOrDefault(p => p.Sku.Equals(skuHint, StringComparison.OrdinalIgnoreCase));
            if (product is not null)
                return (RemoteAdMatchStatuses.Suggested, product.Sku, "seller_custom_field",
                    ads.FirstOrDefault(a => a.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase))?.Id);
            var ad = ads.FirstOrDefault(a => a.Sku.Equals(skuHint, StringComparison.OrdinalIgnoreCase));
            if (ad is not null)
                return (RemoteAdMatchStatuses.Suggested, ad.Sku, "seller_custom_field", ad.Id);
        }

        var gtin = Digits(item.Gtin);
        if (gtin.Length >= 8)
        {
            var eanHits = products.Where(p => Digits(p.Ean) == gtin).ToList();
            if (eanHits.Count == 1)
                return (RemoteAdMatchStatuses.Suggested, eanHits[0].Sku, "ean",
                    ads.FirstOrDefault(a => a.Sku.Equals(eanHits[0].Sku, StringComparison.OrdinalIgnoreCase))?.Id);
            if (eanHits.Count > 1)
                return (RemoteAdMatchStatuses.Unmatched, null, "ean_ambiguous", null);
        }
        return (RemoteAdMatchStatuses.Unmatched, null, null, null);
    }

    void UpsertAttribute(Advertisement ad, string marketplace, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var existing = ad.Attributes.FirstOrDefault(a =>
            a.MarketplaceCode == marketplace && a.FieldName.Equals(field, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.FieldValue = value;
            return;
        }
        var row = new AdvertisementAttribute
        {
            Id = Guid.NewGuid(),
            AdvertisementId = ad.Id,
            MarketplaceCode = marketplace,
            FieldName = field,
            FieldValue = value
        };
        ad.Attributes.Add(row);
        db.AdvertisementAttributes.Add(row);
    }

    static IReadOnlyList<string> ParsePictures(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static object Map(MarketplaceRemoteAd x) => new
    {
        x.Id,
        x.MarketplaceCode,
        x.RemoteId,
        x.Title,
        x.Price,
        x.Quantity,
        x.RemoteStatus,
        x.Permalink,
        x.Thumbnail,
        x.SellerCustomField,
        x.Gtin,
        x.CategoryId,
        x.ListingTypeId,
        pictures = ParsePictures(x.PicturesJson),
        x.MatchStatus,
        matchStatusPt = RemoteAdMatchStatuses.Pt.GetValueOrDefault(x.MatchStatus, x.MatchStatus),
        x.SuggestedSku,
        x.SuggestedReason,
        x.AdvertisementId,
        x.RunId,
        x.UpdatedAt
    };

    static string Digits(string? value) => string.IsNullOrWhiteSpace(value) ? "" : NonDigit.Replace(value, "");

    static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
}
