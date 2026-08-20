using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class MarketplaceService(AppDbContext db, SecretProtector protector)
{
    public async Task<List<object>> CatalogAsync(bool includeInactive, CancellationToken ct)
    {
        var q = db.Marketplaces.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(m => m.IsActive);
        return (await q.OrderBy(m => m.Code).ToListAsync(ct))
            .Select(m => (object)new { m.Code, m.DisplayName, m.AuthProtocolCode, m.BaseUrl, m.IsActive })
            .ToList();
    }

    public async Task<object> GetCompanyMarketplacesAsync(Guid companyId, CancellationToken ct)
    {
        var defs = await db.MarketplaceParameterDefinitions.AsNoTracking()
            .Where(d => d.Scope == "company").OrderBy(d => d.SortOrder).ToListAsync(ct);
        var configs = await db.CompanyMarketplaceConfigs.AsNoTracking()
            .Where(c => c.CompanyId == companyId).ToListAsync(ct);
        var ids = configs.Select(c => c.Id).ToList();
        var parms = await db.CompanyMarketplaceParameters.AsNoTracking()
            .Where(p => ids.Contains(p.ConfigId)).ToListAsync(ct);
        var markets = await db.Marketplaces.AsNoTracking().ToListAsync(ct);

        return markets.Select(m =>
        {
            var cfg = configs.FirstOrDefault(c => c.MarketplaceCode == m.Code);
            var fields = defs.Where(d => d.MarketplaceCode == m.Code).Select(d =>
            {
                var stored = cfg is null ? null : parms.FirstOrDefault(p => p.ConfigId == cfg.Id && p.ParameterKey == d.ParameterKey);
                var raw = stored?.ParameterValue ?? "";
                if (d.IsSecret && stored is not null && !string.IsNullOrEmpty(raw))
                {
                    try { raw = protector.Unprotect(raw); } catch { /* already plain in tests */ }
                    raw = protector.Mask(raw);
                }
                return new
                {
                    d.ParameterKey,
                    d.Label,
                    d.IsSecret,
                    d.FilledByOauth,
                    value = raw
                };
            });
            return (object)new
            {
                m.Code,
                m.DisplayName,
                m.IsActive,
                isEnabled = cfg?.IsEnabled ?? false,
                linkStatus = cfg?.LinkStatus ?? LinkStatuses.PendingConnect,
                fields
            };
        }).ToList();
    }

    public async Task UpsertCompanyAsync(Guid companyId, string code, bool enabled, Dictionary<string, string?> values, CancellationToken ct)
    {
        if (!await db.Marketplaces.AnyAsync(m => m.Code == code, ct))
            throw new KeyNotFoundException("MarketplaceNotFound");
        var cfg = await db.CompanyMarketplaceConfigs.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == code, ct);
        if (cfg is null)
        {
            cfg = new CompanyMarketplaceConfig
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                MarketplaceCode = code,
                IsEnabled = enabled,
                LinkStatus = LinkStatuses.PendingConnect
            };
            db.CompanyMarketplaceConfigs.Add(cfg);
        }
        else cfg.IsEnabled = enabled;

        var defs = await db.MarketplaceParameterDefinitions.Where(d => d.MarketplaceCode == code && d.Scope == "company").ToListAsync(ct);
        foreach (var def in defs.Where(d => !d.FilledByOauth))
        {
            if (!values.TryGetValue(def.ParameterKey, out var raw) || raw is null) continue;
            if (def.IsSecret && (raw.StartsWith("••••") || string.IsNullOrWhiteSpace(raw))) continue;
            var row = await db.CompanyMarketplaceParameters.FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == def.ParameterKey, ct);
            var stored = def.IsSecret ? protector.Protect(raw) : raw;
            if (row is null)
                db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
                {
                    Id = Guid.NewGuid(),
                    ConfigId = cfg.Id,
                    ParameterKey = def.ParameterKey,
                    ParameterValue = stored,
                    IsSecret = def.IsSecret
                });
            else row.ParameterValue = stored;
        }

        var hasToken = await db.CompanyMarketplaceParameters.AnyAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == "AccessToken", ct);
        if (hasToken) cfg.LinkStatus = LinkStatuses.Linked;
        await db.SaveChangesAsync(ct);
        await RecomputeReadinessAsync(companyId, ct);
    }

    public async Task RecomputeReadinessAsync(Guid companyId, CancellationToken ct)
    {
        var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);
        var linked = await db.CompanyMarketplaceConfigs.CountAsync(c => c.CompanyId == companyId && c.IsEnabled && c.LinkStatus == LinkStatuses.Linked, ct);
        var enabled = await db.CompanyMarketplaceConfigs.CountAsync(c => c.CompanyId == companyId && c.IsEnabled, ct);
        var cert = await db.CompanyCertificates.AnyAsync(c => c.CompanyId == companyId && c.IsActive, ct);
        company.ReadyToList = linked > 0 && !string.IsNullOrWhiteSpace(company.Cep);
        company.ReadyToSyncSales = linked > 0;
        company.ReadyToInvoice = cert && !string.IsNullOrWhiteSpace(company.Ie);
        if (enabled > 0 && company.Status == CompanyStatuses.Draft) company.Status = CompanyStatuses.Active;
        await db.SaveChangesAsync(ct);
    }

    public async Task<object> ConnectUrlAsync(Guid companyId, string code, string publicBase)
    {
        var callback = $"{publicBase.TrimEnd('/')}/oauth/{code}/callback";
        return new { marketplace = code, authorizationUrl = callback + $"?companyId={companyId}&demo=1", callback };
    }

    public async Task HandleOAuthCallbackAsync(string code, Guid companyId, CancellationToken ct)
    {
        var cfg = await db.CompanyMarketplaceConfigs.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == code, ct);
        if (cfg is null)
        {
            cfg = new CompanyMarketplaceConfig { Id = Guid.NewGuid(), CompanyId = companyId, MarketplaceCode = code, IsEnabled = true };
            db.CompanyMarketplaceConfigs.Add(cfg);
        }
        cfg.LinkStatus = LinkStatuses.Linked;
        async Task Upsert(string key, string value, bool secret)
        {
            var row = await db.CompanyMarketplaceParameters.FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == key, ct);
            var stored = secret ? protector.Protect(value) : value;
            if (row is null)
                db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
                {
                    Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = key, ParameterValue = stored, IsSecret = secret
                });
            else row.ParameterValue = stored;
        }
        await Upsert("AccessToken", "demo-access-token", true);
        await Upsert("RefreshToken", "demo-refresh-token", true);
        await db.SaveChangesAsync(ct);
        await RecomputeReadinessAsync(companyId, ct);
    }

    public async Task<object> HandleWebhookAsync(string code, JsonElement body, CancellationToken ct)
    {
        var eventId = body.TryGetProperty("id", out var idEl) ? idEl.ToString() : Guid.NewGuid().ToString();
        var resource = body.TryGetProperty("resource", out var r) ? r.GetString() : null;
        var shop = body.TryGetProperty("user_id", out var u) ? u.ToString() : body.TryGetProperty("shop_id", out var s) ? s.ToString() : "";
        var vendor = await db.UserDetailMarketplaces.FirstOrDefaultAsync(v => v.MarketplaceCode == code && v.ParametersJson.Contains(shop) && shop != "", ct);
        var companyId = vendor?.CompanyId
                        ?? (await db.CompanyMarketplaceConfigs.FirstOrDefaultAsync(c => c.MarketplaceCode == code && c.IsEnabled, ct))?.CompanyId;
        if (companyId is null) return new { ack = true, dropped = true };
        var exists = await db.WebhookEvents.AnyAsync(e => e.CompanyId == companyId && e.MarketplaceCode == code && e.EventId == eventId, ct);
        if (!exists)
        {
            db.WebhookEvents.Add(new WebhookEvent
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId.Value,
                MarketplaceCode = code,
                EventId = eventId,
                PayloadJson = body.GetRawText()
            });
            db.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId.Value,
                Kind = WorkKinds.SaleImport,
                PayloadJson = JsonSerializer.Serialize(new { eventId, resource, marketplace = code, vendorUserId = vendor?.UserId })
            });
            await db.SaveChangesAsync(ct);
        }
        return new { ack = true };
    }
}
