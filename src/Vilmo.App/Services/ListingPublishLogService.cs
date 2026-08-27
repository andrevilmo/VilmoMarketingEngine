using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;

namespace Vilmo.Services;

public static class ListingPublishLogSteps
{
    public const string Saved = "saved";
    public const string Received = "received";
    public const string Validated = "validated";
    public const string Credentials = "credentials";
    public const string Queued = "queued";
    public const string WorkerStarted = "worker_started";
    public const string Calling = "calling";
    public const string Callback = "callback";
    public const string Applied = "applied";
    public const string Failed = "failed";
}

public sealed class ListingPublishLogService(AppDbContext db)
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task WriteAsync(
        Guid companyId, Guid runId, Guid advertisementId, Guid listingId, string marketplaceCode,
        string action, string stepCode, string level, string userMessage, object? technical, CancellationToken ct)
    {
        db.ListingPublishLogs.Add(new ListingPublishLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            RunId = runId,
            AdvertisementId = advertisementId,
            ListingId = listingId,
            MarketplaceCode = marketplaceCode,
            Action = action,
            StepCode = stepCode,
            Level = level,
            UserMessage = userMessage,
            TechnicalJson = JsonSerializer.Serialize(technical ?? new { }, JsonOpts),
            CreatedAt = DateTimeOffset.UtcNow
        });
        return db.SaveChangesAsync(ct);
    }

    public async Task<List<ListingPublishLog>> ListAsync(Guid companyId, Guid listingId, CancellationToken ct)
    {
        var q = db.ListingPublishLogs.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.ListingId == listingId);
        if (db.Database.IsNpgsql())
            return await q.OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync(ct);
        return (await q.ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).Take(50).ToList();
    }

    public async Task<Dictionary<Guid, List<ListingPublishLog>>> ListForListingsAsync(
        Guid companyId, IReadOnlyCollection<Guid> listingIds, CancellationToken ct)
    {
        if (listingIds.Count == 0) return new Dictionary<Guid, List<ListingPublishLog>>();
        var q = db.ListingPublishLogs.AsNoTracking()
            .Where(l => l.CompanyId == companyId && listingIds.Contains(l.ListingId));
        List<ListingPublishLog> rows;
        if (db.Database.IsNpgsql())
            rows = await q.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        else
            rows = (await q.ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();
        return rows.GroupBy(l => l.ListingId)
            .ToDictionary(g => g.Key, g => g.Take(40).ToList());
    }

    public static object Map(ListingPublishLog l) => new
    {
        l.Id,
        l.RunId,
        l.MarketplaceCode,
        l.Action,
        l.StepCode,
        l.Level,
        l.UserMessage,
        l.TechnicalJson,
        l.CreatedAt
    };
}
