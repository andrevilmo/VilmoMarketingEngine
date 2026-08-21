using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class MarketplaceService(AppDbContext db, SecretProtector protector, IHttpClientFactory httpFactory, MarketplaceConnectLogService connectLogs)
{
    public sealed record OAuthCallbackResult(bool Ok, string? Error = null);
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
        var logsByCode = await connectLogs.ListForCompanyAsync(companyId, ct);

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
            var channelLogs = logsByCode.TryGetValue(m.Code, out var steps) ? steps : [];
            return (object)new
            {
                m.Code,
                m.DisplayName,
                m.IsActive,
                isEnabled = cfg?.IsEnabled ?? false,
                linkStatus = cfg?.LinkStatus ?? LinkStatuses.PendingConnect,
                fields,
                lastStep = channelLogs.FirstOrDefault(),
                connectLog = channelLogs
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

    public async Task<object> ConnectUrlAsync(Guid companyId, string marketplaceCode, string publicBase, CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        var callback = $"{publicBase.TrimEnd('/')}/oauth/{marketplaceCode}/callback";
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Received, "info",
            "Pedido de conexão recebido. Vamos montar o login deste marketplace.",
            new { marketplace = marketplaceCode, callback }, ct);

        var clientId = await ReadParamAsync(companyId, marketplaceCode, "ClientId", ct);
        var hasSecret = !string.IsNullOrWhiteSpace(await ReadParamAsync(companyId, marketplaceCode, "ClientSecret", ct));
        var hasClient = !string.IsNullOrWhiteSpace(clientId);
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Credentials, hasClient ? "info" : "warning",
            hasClient
                ? (hasSecret
                    ? "Client ID e Client Secret encontrados. O canal poderá devolver um AccessToken de verdade."
                    : "Client ID encontrado, mas falta o Client Secret. Salve o secret antes de autorizar.")
                : "Sem Client ID. Vilmo vai usar uma conexão de demonstração (token local, sem login no canal).",
            new { hasClientId = hasClient, hasClientSecret = hasSecret, clientId }, ct);

        var authorize = AuthorizeUrl(marketplaceCode, clientId, callback, companyId);
        var demo = authorize is null;
        var authorizationUrl = authorize ?? $"{callback}?companyId={companyId:D}&demo=1";
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Authorize, demo ? "warning" : "info",
            demo
                ? "URL de demonstração montada. Nada será enviado ao login oficial do marketplace."
                : "URL de autorização montada. O navegador vai abrir o login do marketplace.",
            new
            {
                request = new { method = "GET", url = authorizationUrl, callback, state = companyId.ToString("D"), demo }
            }, ct);
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Redirect, "info",
            "Redirecionando para o login. Depois o marketplace devolve o navegador para Vilmo com um código.",
            new { request = new { method = "GET", url = authorizationUrl } }, ct);

        return new
        {
            marketplace = marketplaceCode,
            authorizationUrl,
            callback,
            runId,
            logs = await connectLogs.ListAsync(companyId, marketplaceCode, ct)
        };
    }

    public async Task<OAuthCallbackResult> HandleOAuthCallbackAsync(
        string marketplaceCode,
        Guid companyId,
        string? oauthCode,
        bool demo,
        string publicBase,
        CancellationToken ct)
    {
        var runId = await connectLogs.ContinueRunAsync(companyId, marketplaceCode, ct);
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Callback, "info",
            demo || string.IsNullOrWhiteSpace(oauthCode)
                ? "Retorno local (demo ou sem código). Nenhum login real do marketplace."
                : "Marketplace devolveu o navegador com um código de autorização.",
            new
            {
                request = new
                {
                    method = "GET",
                    url = $"{publicBase.TrimEnd('/')}/oauth/{marketplaceCode}/callback",
                    query = new { state = companyId.ToString("D"), demo, code = MarketplaceConnectLogService.Mask(oauthCode) }
                }
            }, ct);

        var cfg = await db.CompanyMarketplaceConfigs.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == marketplaceCode, ct);
        if (cfg is null)
        {
            cfg = new CompanyMarketplaceConfig
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                MarketplaceCode = marketplaceCode,
                IsEnabled = true
            };
            db.CompanyMarketplaceConfigs.Add(cfg);
        }

        async Task Upsert(string key, string value, bool secret)
        {
            var row = await db.CompanyMarketplaceParameters.FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == key, ct);
            var stored = secret ? protector.Protect(value) : value;
            if (row is null)
                db.CompanyMarketplaceParameters.Add(new CompanyMarketplaceParameter
                {
                    Id = Guid.NewGuid(), ConfigId = cfg.Id, ParameterKey = key, ParameterValue = stored, IsSecret = secret
                });
            else
            {
                row.ParameterValue = stored;
                row.IsSecret = secret;
            }
        }

        if (demo || string.IsNullOrWhiteSpace(oauthCode))
        {
            cfg.LinkStatus = LinkStatuses.Linked;
            await Upsert("AccessToken", "demo-access-token", true);
            await Upsert("RefreshToken", "demo-refresh-token", true);
            await db.SaveChangesAsync(ct);
            await RecomputeReadinessAsync(companyId, ct);
            await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
                MarketplaceConnectLogSteps.Applied, "warning",
                "Conexão demo gravada. AccessToken local — o marketplace real vai recusar publicações.",
                new { response = new { linkStatus = cfg.LinkStatus, demo = true, userId = (string?)null } }, ct);
            return new OAuthCallbackResult(true);
        }

        var callback = $"{publicBase.TrimEnd('/')}/oauth/{marketplaceCode}/callback";
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Exchanging, "info",
            "Trocando o código pelo AccessToken no servidor do marketplace.",
            new { request = new { grantType = "authorization_code", redirectUri = callback } }, ct);

        var exchanged = await ExchangeAuthorizationCodeAsync(marketplaceCode, companyId, oauthCode, callback, runId, ct);
        if (!exchanged.Ok)
        {
            await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
                MarketplaceConnectLogSteps.Failed, "error",
                $"Não foi possível obter o AccessToken. {exchanged.Error}",
                new { response = new { error = exchanged.Error } }, ct);
            return new OAuthCallbackResult(false, exchanged.Error);
        }

        cfg.IsEnabled = true;
        cfg.LinkStatus = LinkStatuses.Linked;
        await Upsert("AccessToken", exchanged.AccessToken!, true);
        if (!string.IsNullOrWhiteSpace(exchanged.RefreshToken))
            await Upsert("RefreshToken", exchanged.RefreshToken, true);
        if (!string.IsNullOrWhiteSpace(exchanged.UserId))
            await Upsert("UserId", exchanged.UserId, false);
        await db.SaveChangesAsync(ct);
        await RecomputeReadinessAsync(companyId, ct);
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Applied, "info",
            "AccessToken e RefreshToken gravados. O canal está conectado.",
            new
            {
                response = new
                {
                    linkStatus = cfg.LinkStatus,
                    userId = exchanged.UserId,
                    accessToken = MarketplaceConnectLogService.Mask(exchanged.AccessToken),
                    refreshToken = MarketplaceConnectLogService.Mask(exchanged.RefreshToken)
                }
            }, ct);
        return new OAuthCallbackResult(true);
    }

    async Task<TokenExchange> ExchangeAuthorizationCodeAsync(
        string marketplaceCode, Guid companyId, string oauthCode, string redirectUri, Guid runId, CancellationToken ct)
    {
        var tokenUrl = TokenEndpoint(marketplaceCode);
        if (tokenUrl is null)
            return TokenExchange.Fail($"OAuth exchange is not implemented for {marketplaceCode}.");

        var clientId = await ReadParamAsync(companyId, marketplaceCode, "ClientId", ct);
        var clientSecret = await ReadParamAsync(companyId, marketplaceCode, "ClientSecret", ct);
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return TokenExchange.Fail("ClientId and ClientSecret must be saved before connecting.");

        var sent = new
        {
            method = "POST",
            url = tokenUrl,
            headers = new { accept = "application/json", contentType = "application/x-www-form-urlencoded" },
            body = new
            {
                grant_type = "authorization_code",
                client_id = clientId,
                client_secret = "••••",
                code = MarketplaceConnectLogService.Mask(oauthCode),
                redirect_uri = redirectUri
            }
        };
        await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
            MarketplaceConnectLogSteps.Calling, "info",
            $"Enviando o código para {tokenUrl}.",
            new { request = sent }, ct);

        try
        {
            var client = httpFactory.CreateClient("marketplace");
            using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = oauthCode,
                ["redirect_uri"] = redirectUri
            });
            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (body.Length > 16000) body = body[..16000] + "…";
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            var response = new
            {
                httpStatus = (int)resp.StatusCode,
                body = RedactedTokenBody(root)
            };
            await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
                MarketplaceConnectLogSteps.Callback, resp.IsSuccessStatusCode ? "info" : "error",
                resp.IsSuccessStatusCode
                    ? $"Marketplace devolveu o token (HTTP {(int)resp.StatusCode})."
                    : $"Marketplace recusou a troca do código (HTTP {(int)resp.StatusCode}).",
                new { request = sent, response }, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var msg = ReadJsonString(root, "error_description")
                    ?? ReadJsonString(root, "message")
                    ?? ReadJsonString(root, "error")
                    ?? $"HTTP {(int)resp.StatusCode}";
                return TokenExchange.Fail(msg);
            }
            var access = ReadJsonString(root, "access_token");
            if (string.IsNullOrWhiteSpace(access))
                return TokenExchange.Fail("Marketplace token response had no access_token.");
            return new TokenExchange(true, null, access, ReadJsonString(root, "refresh_token"), ReadJsonString(root, "user_id"));
        }
        catch (Exception ex)
        {
            await connectLogs.WriteAsync(companyId, runId, marketplaceCode,
                MarketplaceConnectLogSteps.Failed, "error",
                "Falha de rede ao pedir o AccessToken.",
                new { request = sent, response = new { error = ex.Message } }, ct);
            return TokenExchange.Fail(ex.Message);
        }
    }

    static object RedactedTokenBody(JsonElement root) => new
    {
        token_type = ReadJsonString(root, "token_type"),
        expires_in = root.TryGetProperty("expires_in", out var exp) ? exp.ToString() : null,
        scope = ReadJsonString(root, "scope"),
        user_id = ReadJsonString(root, "user_id"),
        access_token = MarketplaceConnectLogService.Mask(ReadJsonString(root, "access_token")),
        refresh_token = MarketplaceConnectLogService.Mask(ReadJsonString(root, "refresh_token")),
        error = ReadJsonString(root, "error"),
        error_description = ReadJsonString(root, "error_description"),
        message = ReadJsonString(root, "message")
    };

    async Task<string?> ReadParamAsync(Guid companyId, string marketplaceCode, string key, CancellationToken ct)
    {
        var cfg = await db.CompanyMarketplaceConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.MarketplaceCode == marketplaceCode, ct);
        if (cfg is null) return null;
        var row = await db.CompanyMarketplaceParameters.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConfigId == cfg.Id && p.ParameterKey == key, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.ParameterValue)) return null;
        if (!row.IsSecret) return row.ParameterValue;
        try { return protector.Unprotect(row.ParameterValue); }
        catch { return row.ParameterValue; }
    }

    static string? AuthorizeUrl(string marketplaceCode, string? clientId, string callback, Guid companyId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return null;
        var state = companyId.ToString("D");
        return marketplaceCode switch
        {
            "MercadoLivre" =>
                "https://auth.mercadolivre.com.br/authorization"
                + "?response_type=code"
                + "&client_id=" + Uri.EscapeDataString(clientId)
                + "&redirect_uri=" + Uri.EscapeDataString(callback)
                + "&state=" + Uri.EscapeDataString(state),
            "Magalu" =>
                "https://id.magalu.com/oauth/authorize"
                + "?response_type=code"
                + "&client_id=" + Uri.EscapeDataString(clientId)
                + "&redirect_uri=" + Uri.EscapeDataString(callback)
                + "&state=" + Uri.EscapeDataString(state),
            _ => null
        };
    }

    static string? TokenEndpoint(string marketplaceCode) => marketplaceCode switch
    {
        "MercadoLivre" => "https://api.mercadolibre.com/oauth/token",
        "Magalu" => "https://id.magalu.com/oauth/token",
        _ => null
    };

    static string? ReadJsonString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            _ => el.ToString()
        };
    }

    sealed record TokenExchange(bool Ok, string? Error, string? AccessToken = null, string? RefreshToken = null, string? UserId = null)
    {
        public static TokenExchange Fail(string error) => new(false, error);
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
