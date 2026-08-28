using System.Text.Json;
using Vilmo.Data;

namespace Vilmo.Services;

public static class ListingImportLogSteps
{
    public const string Received = "received";
    public const string NotLinked = "not_linked";
    public const string DemoToken = "demo_token";
    public const string Queued = "queued";
    public const string WorkerStarted = "worker_started";
    public const string Scanning = "scanning";
    public const string Classified = "classified";
    public const string Done = "done";
    public const string Failed = "failed";
}

public sealed class ListingImportLogService(AppDbContext db)
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(
        Guid companyId,
        Guid runId,
        string marketplaceCode,
        string stepCode,
        string level,
        string userMessage,
        object? technical,
        string? remoteId,
        CancellationToken ct)
    {
        db.ListingImportLogs.Add(new ListingImportLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            RunId = runId,
            MarketplaceCode = marketplaceCode,
            RemoteId = string.IsNullOrWhiteSpace(remoteId) ? null : remoteId,
            StepCode = stepCode,
            Level = level,
            UserMessage = userMessage,
            TechnicalJson = JsonSerializer.Serialize(technical ?? new { }, JsonOpts),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
