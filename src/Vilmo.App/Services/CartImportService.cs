using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

public sealed class CartImportService(
    AppDbContext db,
    ICartImageDownloader images,
    ICartMediaStore media)
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<CartEnqueueResult> EnqueueAsync(Guid companyId, Guid userId, string fileName, string csv, CancellationToken ct)
    {
        IReadOnlyList<CartCsvRow> rows;
        try { rows = CartCsvParser.Parse(csv); }
        catch (ArgumentException)
        {
            return new CartEnqueueResult("CsvMissingSkuOrName", "CSV sem colunas sku/name.", null, null, 0);
        }
        if (rows.Count == 0)
            return new CartEnqueueResult("CsvEmpty", "Nenhuma linha de produto no CSV.", null, null, 0);

        var batch = new CartImportBatch
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            CreatedByUserId = userId,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "cart.csv" : Path.GetFileName(fileName),
            Status = "Queued",
            RowCount = rows.Count,
            CsvText = csv,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.CartImportBatches.Add(batch);
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = WorkKinds.CartImport,
            PayloadJson = JsonSerializer.Serialize(new { batchId = batch.Id }, JsonOpts)
        });
        await db.SaveChangesAsync(ct);
        await LogAsync(companyId, batch.Id, "queued", "info",
            $"CSV recebido com {rows.Count} produto(s). Imagens serão baixadas em seguida.",
            new { batch.FileName, rows.Count }, ct);
        return new CartEnqueueResult(null, null, batch.Id, batch.Status, batch.RowCount);
    }

    public async Task ProcessQueuedAsync(WorkItem work, CancellationToken ct)
    {
        var batchIdRaw = JsonGet(work.PayloadJson, "batchId");
        if (!Guid.TryParse(batchIdRaw, out var batchId))
        {
            work.Status = "Failed";
            work.Error = "BatchNotFound";
            return;
        }
        var batch = await db.CartImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            work.Status = "Failed";
            work.Error = "BatchNotFound";
            return;
        }

        batch.Status = "Downloading";
        await db.SaveChangesAsync(ct);
        await LogAsync(batch.CompanyId, batch.Id, "started", "info",
            "Importação iniciada. Gravando produtos e baixando imagens.",
            new { workItemId = work.Id }, ct);

        try
        {
            var rows = CartCsvParser.Parse(batch.CsvText);
            var imageOk = 0;
            var imageFailed = 0;
            foreach (var row in rows)
            {
                var (ok, fail) = await UpsertRowAsync(batch, row, ct);
                imageOk += ok;
                imageFailed += fail;
            }
            batch.RowCount = rows.Count;
            batch.ImageOk = imageOk;
            batch.ImageFailed = imageFailed;
            batch.Status = "Done";
            batch.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await LogAsync(batch.CompanyId, batch.Id, "done", "info",
                $"Importação concluída: {rows.Count} produto(s), {imageOk} imagem(ns) salva(s)" +
                (imageFailed > 0 ? $", {imageFailed} imagem(ns) falharam." : "."),
                new { rows.Count, imageOk, imageFailed }, ct);
            work.Status = "Done";
        }
        catch (Exception ex)
        {
            batch.Status = "Failed";
            batch.Error = ex.Message;
            batch.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await LogAsync(batch.CompanyId, batch.Id, "failed", "error",
                "Não foi possível concluir a importação.",
                new { error = ex.GetType().Name, message = ex.Message }, ct);
            work.Status = "Failed";
            work.Error = ex.Message;
        }
    }

    public async Task<IReadOnlyList<object>> ListAsync(Guid companyId, CancellationToken ct)
    {
        var items = await db.CartProducts.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        var images = await db.CartProductImages.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.Status == "Saved")
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);
        var byProduct = images.GroupBy(i => i.CartProductId).ToDictionary(g => g.Key, g => g.ToList());
        return items.Select(p => MapCartProduct(p, byProduct.GetValueOrDefault(p.Id) ?? [])).ToList();
    }

    public async Task<object?> GetAsync(Guid companyId, Guid id, CancellationToken ct)
    {
        var p = await db.CartProducts.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == id, ct);
        if (p is null) return null;
        var imgs = await db.CartProductImages.AsNoTracking()
            .Where(i => i.CartProductId == id)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);
        return MapCartProduct(p, imgs);
    }

    public async Task<object?> GetBatchAsync(Guid companyId, Guid batchId, CancellationToken ct)
    {
        var b = await db.CartImportBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == batchId, ct);
        if (b is null) return null;
        var logs = await db.CartImportLogs.AsNoTracking().Where(l => l.BatchId == batchId).ToListAsync(ct);
        logs = logs.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).ToList();
        return new
        {
            b.Id,
            b.FileName,
            b.Status,
            b.RowCount,
            b.ImageOk,
            b.ImageFailed,
            b.Error,
            b.CreatedAt,
            b.ProcessedAt,
            logs = logs.Select(l => new { l.Id, l.StepCode, l.Level, l.UserMessage, l.TechnicalJson, l.CreatedAt })
        };
    }

    public async Task<(CartProductImage? Image, string? Path)> GetImageFileAsync(Guid companyId, Guid cartProductId, Guid imageId, CancellationToken ct)
    {
        var img = await db.CartProductImages.AsNoTracking()
            .FirstOrDefaultAsync(i => i.CompanyId == companyId && i.CartProductId == cartProductId && i.Id == imageId && i.Status == "Saved", ct);
        if (img is null || string.IsNullOrWhiteSpace(img.RelativePath)) return (null, null);
        var abs = media.AbsolutePath(img.RelativePath);
        return File.Exists(abs) ? (img, abs) : (img, null);
    }

    public async Task<object> LinkAsync(Guid companyId, Guid cartProductId, string? productSku, string? nfeChave, int? nItem, CancellationToken ct)
    {
        var cart = await db.CartProducts.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Id == cartProductId, ct)
            ?? throw new KeyNotFoundException("CartProductNotFound");

        Product? product = null;
        NfeDocument? nfe = null;
        ParsedItem? nfeItem = null;
        if (!string.IsNullOrWhiteSpace(nfeChave))
        {
            var digits = ChaveAcesso.Digits(nfeChave);
            nfe = await db.NfeDocuments.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.ChaveAcesso == digits, ct)
                  ?? throw new KeyNotFoundException("NfeNotFound");
            if (!string.IsNullOrWhiteSpace(nfe.Xml))
            {
                var parsed = NfeIngestService.Parse(nfe.Xml);
                nfeItem = nItem is int n
                    ? parsed?.Items.FirstOrDefault(i => i.NItem == n)
                    : parsed?.Items.FirstOrDefault();
            }
        }
        if (!string.IsNullOrWhiteSpace(productSku))
            product = await db.Products.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Sku == productSku.Trim(), ct);

        if (product is null && nfeItem is not null)
        {
            var sku = string.IsNullOrWhiteSpace(nfeItem.CProd) ? cart.Sku : nfeItem.CProd;
            product = await db.Products.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Sku == sku, ct);
            if (product is null)
            {
                product = new Product
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Sku = sku,
                    Name = nfeItem.XProd,
                    Ean = nfeItem.Ean is "SEM GTIN" ? null : nfeItem.Ean,
                    Ncm = nfeItem.Ncm,
                    Cfop = nfeItem.Cfop,
                    SalePrice = nfeItem.VUn
                };
                db.Products.Add(product);
                if (!await db.InventoryBalances.AnyAsync(b => b.CompanyId == companyId && b.Sku == sku, ct))
                    db.InventoryBalances.Add(new InventoryBalance { Id = Guid.NewGuid(), CompanyId = companyId, Sku = sku, OnHand = 0 });
            }
        }
        if (product is null)
            throw new InvalidOperationException("ProductRequired");

        product.Description = cart.Description ?? product.Description;
        product.SourceUrl = cart.SourceUrl ?? product.SourceUrl;
        product.LinkedCartProductId = cart.Id;
        if (!string.IsNullOrWhiteSpace(cart.Name)) product.Name = cart.Name;
        cart.LinkedProductId = product.Id;
        cart.LinkedNfeDocumentId = nfe?.Id;
        cart.LinkedNfeNItem = nfeItem?.NItem ?? nItem;
        cart.UpdatedAt = DateTimeOffset.UtcNow;

        var existing = await db.ProductImages.Where(i => i.ProductId == product.Id).ToListAsync(ct);
        db.ProductImages.RemoveRange(existing);
        var saved = cart.Images.Where(i => i.Status == "Saved").OrderBy(i => i.SortOrder).ToList();
        var order = 0;
        foreach (var img in saved)
        {
            db.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                CompanyId = companyId,
                CartProductImageId = img.Id,
                SortOrder = order++,
                RelativePath = img.RelativePath,
                ContentType = img.ContentType
            });
        }
        await db.SaveChangesAsync(ct);
        return new
        {
            cartProductId = cart.Id,
            productId = product.Id,
            productSku = product.Sku,
            nfeChave = nfe?.ChaveAcesso,
            nItem = cart.LinkedNfeNItem,
            imageCount = saved.Count
        };
    }

    async Task<(int Ok, int Fail)> UpsertRowAsync(CartImportBatch batch, CartCsvRow row, CancellationToken ct)
    {
        var cart = await db.CartProducts.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.CompanyId == batch.CompanyId && p.SourceId == row.SourceId, ct);
        if (cart is null)
        {
            cart = new CartProduct
            {
                Id = Guid.NewGuid(),
                CompanyId = batch.CompanyId,
                SourceId = row.SourceId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.CartProducts.Add(cart);
        }
        cart.BatchId = batch.Id;
        cart.Sku = row.Sku;
        cart.Name = row.Name;
        cart.Quantity = row.Quantity;
        cart.UnitPrice = row.UnitPrice;
        cart.LineTotal = row.LineTotal;
        cart.SourceUrl = row.SourceUrl;
        cart.Description = row.Description;
        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var urls = CartCsvParser.CollectImageUrls(row);
        var ok = 0;
        var fail = 0;
        var sort = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in urls)
        {
            if (!seen.Add(url)) continue;
            var existing = cart.Images.FirstOrDefault(i => i.SourceUrl == url);
            if (existing is { Status: "Saved", ByteLength: > 0 })
            {
                existing.SortOrder = sort++;
                existing.Kind = sort == 1 ? "Cart" : "Gallery";
                ok++;
                continue;
            }
            var img = existing ?? new CartProductImage
            {
                Id = Guid.NewGuid(),
                CartProductId = cart.Id,
                CompanyId = batch.CompanyId,
                SourceUrl = url
            };
            img.Kind = sort == 0 ? "Cart" : "Gallery";
            img.SortOrder = sort;
            img.Status = "Pending";
            if (existing is null) { db.CartProductImages.Add(img); cart.Images.Add(img); }
            try
            {
                var downloaded = await images.DownloadAsync(url, ct);
                if (downloaded is null)
                {
                    img.Status = "Skipped";
                    img.Error = "UnusableOrFailed";
                    fail++;
                }
                else
                {
                    img.RelativePath = await media.SaveAsync(batch.CompanyId, cart.Id, img.Id, downloaded.Extension, downloaded.Bytes, ct);
                    img.ContentType = downloaded.ContentType;
                    img.ByteLength = downloaded.Bytes.Length;
                    img.Status = "Saved";
                    img.Error = null;
                    ok++;
                }
            }
            catch (Exception ex)
            {
                img.Status = "Failed";
                img.Error = ex.Message;
                fail++;
            }
            sort++;
            await db.SaveChangesAsync(ct);
        }
        return (ok, fail);
    }

    async Task LogAsync(Guid companyId, Guid batchId, string step, string level, string message, object? technical, CancellationToken ct)
    {
        db.CartImportLogs.Add(new CartImportLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            BatchId = batchId,
            StepCode = step,
            Level = level,
            UserMessage = message,
            TechnicalJson = JsonSerializer.Serialize(technical ?? new { }, JsonOpts),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    static object MapCartProduct(CartProduct p, IReadOnlyList<CartProductImage> images) => new
    {
        p.Id,
        p.SourceId,
        p.Sku,
        p.Name,
        p.Quantity,
        p.UnitPrice,
        p.LineTotal,
        p.SourceUrl,
        p.Description,
        p.LinkedProductId,
        p.LinkedNfeDocumentId,
        p.LinkedNfeNItem,
        p.BatchId,
        p.UpdatedAt,
        imageCount = images.Count(i => i.Status == "Saved"),
        images = images.Select(i => new
        {
            i.Id,
            i.Kind,
            i.SortOrder,
            i.Status,
            i.ContentType,
            i.ByteLength,
            href = $"/cart-products/{p.Id}/images/{i.Id}"
        })
    };

    static string JsonGet(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(key, out var v) ? v.ToString() : "";
        }
        catch { return ""; }
    }
}

public sealed record CartEnqueueResult(string? Error, string? Message, Guid? Id, string? Status, int RowCount);
