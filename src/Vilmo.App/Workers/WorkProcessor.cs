using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Services;

namespace Vilmo.Workers;

public sealed class WorkProcessor(AppDbContext db, NfeIngestService nfe, SalesService sales, ILogger<WorkProcessor> log)
{
    public async Task<int> DrainAsync(string[] kinds, CancellationToken ct)
    {
        var items = await db.WorkItems
            .Where(w => w.Status == "Pending" && kinds.Contains(w.Kind))
            .OrderBy(w => w.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        foreach (var item in items)
        {
            try
            {
                item.Status = "Processing";
                await db.SaveChangesAsync(ct);
                await HandleAsync(item, ct);
                if (item.Status == "Processing") item.Status = "Done";
                item.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "work {Id} {Kind} failed", item.Id, item.Kind);
                item.Status = "Failed";
                item.Error = ex.Message;
                if (item.Kind == WorkKinds.NfeIngest)
                    await nfe.LogWorkFailureAsync(item, ex, ct);
            }
            await db.SaveChangesAsync(ct);
        }
        return items.Count;
    }

    async Task HandleAsync(WorkItem item, CancellationToken ct)
    {
        switch (item.Kind)
        {
            case WorkKinds.NfeIngest:
                await nfe.ProcessQueuedAsync(item, ct);
                break;
            case WorkKinds.NfeEmit:
            {
                var saleId = Guid.Parse(JsonGet(item.PayloadJson, "saleId"));
                await sales.CompleteEmitAsync(saleId, ct);
                break;
            }
            case WorkKinds.SaleImport:
            {
                var marketplace = JsonGet(item.PayloadJson, "marketplace");
                var remote = JsonGet(item.PayloadJson, "resource") ?? $"demo-{item.Id:N}"[..16];
                Guid? vendor = Guid.TryParse(JsonGet(item.PayloadJson, "vendorUserId"), out var v) ? v : null;
                var company = await db.Companies.FirstAsync(c => c.Id == item.CompanyId, ct);
                var product = await db.Products.FirstOrDefaultAsync(p => p.CompanyId == item.CompanyId, ct);
                var sku = product?.Sku ?? "CAMISETA-001";
                await sales.UpsertImportedAsync(
                    item.CompanyId, vendor, string.IsNullOrEmpty(marketplace) ? "MercadoLivre" : marketplace, remote,
                    SaleStatuses.Paid, product?.SalePrice ?? 89.90m, "Cliente Marketplace",
                    new Recipient("Cliente Marketplace", "Rua das Flores", "100", null, "Centro", company.City, company.Uf, company.Cep),
                    [new SaleItem { Sku = sku, Name = product?.Name ?? sku, Quantity = 1, UnitPrice = product?.SalePrice ?? 89.90m, Ncm = product?.Ncm, Cfop = product?.Cfop }],
                    new Dictionary<string, string> { ["source"] = "webhook" },
                    ct);
                break;
            }
            case WorkKinds.PublishListing:
            {
                var listingId = Guid.Parse(JsonGet(item.PayloadJson, "listingId"));
                var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == listingId, ct);
                if (listing is not null)
                {
                    listing.Status = "PublishedDemo";
                    listing.RemoteId = $"demo-{listing.Id:N}"[..12];
                }
                break;
            }
            case WorkKinds.StockPublish:
            case WorkKinds.UploadInvoice:
                break;
        }
    }

    static string JsonGet(string json, string key)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(key, out var v) ? v.ToString() : "";
        }
        catch { return ""; }
    }
}

public sealed class PollingWorker(WorkProcessor processor, ILogger<PollingWorker> log) : BackgroundService
{
    public string[] Kinds { get; init; } = [WorkKinds.SaleImport, WorkKinds.PublishListing, WorkKinds.StockPublish, WorkKinds.UploadInvoice];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("worker kinds {Kinds}", string.Join(",", Kinds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await processor.DrainAsync(Kinds, stoppingToken); }
            catch (Exception ex) { log.LogError(ex, "drain failed"); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
