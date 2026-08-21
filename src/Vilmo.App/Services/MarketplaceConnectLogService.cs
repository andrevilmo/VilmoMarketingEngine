using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;

namespace Vilmo.Services;

public static class MarketplaceConnectLogSteps
{
    public const string Received = "received";
    public const string Credentials = "credentials";
    public const string Authorize = "authorize";
    public const string Redirect = "redirect";
    public const string Callback = "callback";
    public const string Exchanging = "exchanging";
    public const string Calling = "calling";
    public const string Applied = "applied";
    public const string Failed = "failed";
}

public sealed class MarketplaceConnectLogService(AppDbContext db)
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task WriteAsync(
        Guid companyId, Guid runId, string marketplaceCode,
        string stepCode, string level, string userMessage, object? technical, CancellationToken ct)
    {
        db.MarketplaceConnectLogs.Add(new MarketplaceConnectLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            RunId = runId,
            MarketplaceCode = marketplaceCode,
            Action = "connect",
            StepCode = stepCode,
            Level = level,
            UserMessage = userMessage,
            TechnicalJson = JsonSerializer.Serialize(technical ?? new { }, JsonOpts),
            CreatedAt = DateTimeOffset.UtcNow
        });
        return db.SaveChangesAsync(ct);
    }

    public async Task<Guid> ContinueRunAsync(Guid companyId, string marketplaceCode, CancellationToken ct)
    {
        var q = db.MarketplaceConnectLogs.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.MarketplaceCode == marketplaceCode);
        MarketplaceConnectLog? last;
        if (db.Database.IsNpgsql())
            last = await q.OrderByDescending(l => l.CreatedAt).FirstOrDefaultAsync(ct);
        else
            last = (await q.ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).FirstOrDefault();
        if (last is null) return Guid.NewGuid();
        if (DateTimeOffset.UtcNow - last.CreatedAt > TimeSpan.FromHours(2)) return Guid.NewGuid();
        if (last.StepCode is MarketplaceConnectLogSteps.Applied or MarketplaceConnectLogSteps.Failed)
            return Guid.NewGuid();
        return last.RunId;
    }

    public async Task<List<object>> ListAsync(Guid companyId, string marketplaceCode, CancellationToken ct)
    {
        var q = db.MarketplaceConnectLogs.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.MarketplaceCode == marketplaceCode);
        List<MarketplaceConnectLog> rows;
        if (db.Database.IsNpgsql())
            rows = await q.OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync(ct);
        else
            rows = (await q.ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).Take(50).ToList();
        return rows.Select(Map).ToList();
    }

    public async Task<Dictionary<string, List<object>>> ListForCompanyAsync(Guid companyId, CancellationToken ct)
    {
        var q = db.MarketplaceConnectLogs.AsNoTracking().Where(l => l.CompanyId == companyId);
        List<MarketplaceConnectLog> rows;
        if (db.Database.IsNpgsql())
            rows = await q.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        else
            rows = (await q.ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();
        return rows.GroupBy(l => l.MarketplaceCode)
            .ToDictionary(g => g.Key, g => g.Take(40).Select(Map).ToList());
    }

    public static object Map(MarketplaceConnectLog l) => new
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

    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains("demo", StringComparison.OrdinalIgnoreCase)) return value;
        if (value.Length <= 8) return "••••";
        return value[..4] + "…" + value[^4..];
    }
}
