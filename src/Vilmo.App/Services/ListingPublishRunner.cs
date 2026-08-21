using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class ListingPublishRunner(
    AppDbContext db,
    ListingPublishLogService logs,
    SecretProtector protector,
    IHttpClientFactory httpFactory)
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task PublishAsync(Advertisement ad, Listing listing, Guid runId, CancellationToken ct)
    {
        const string action = "publish";
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Received, "info",
            "Pedido de publicação recebido para este marketplace.",
            new { listing.Id, listing.Sku, listing.MarketplaceCode, ad.Title, ad.Price, ad.AvailableQuantity }, ct);

        var marketplace = await db.Marketplaces.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == listing.MarketplaceCode, ct);
        var cfg = await db.CompanyMarketplaceConfigs
            .FirstOrDefaultAsync(c => c.CompanyId == ad.CompanyId && c.MarketplaceCode == listing.MarketplaceCode, ct);
        var token = cfg is null ? null : await ReadTokenAsync(cfg.Id, ct);
        var linked = cfg is { IsEnabled: true, LinkStatus: LinkStatuses.Linked } && !string.IsNullOrWhiteSpace(token);
        var demoToken = IsDemoToken(token);

        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Credentials, linked && !demoToken ? "info" : "warning",
            linked
                ? (demoToken
                    ? "Canal marcado como conectado, mas o token é de demonstração. A API real do marketplace deve recusar."
                    : "Credenciais do marketplace encontradas. Chamando a API do canal.")
                : "Marketplace não conectado ou sem AccessToken. Nada será enviado ao canal real.",
            new
            {
                enabled = cfg?.IsEnabled ?? false,
                linkStatus = cfg?.LinkStatus ?? LinkStatuses.PendingConnect,
                hasAccessToken = !string.IsNullOrWhiteSpace(token),
                demoToken,
                baseUrl = marketplace?.BaseUrl,
                authProtocol = marketplace?.AuthProtocolCode
            }, ct);

        if (linked)
        {
            listing.Status = ListingStatuses.Queued;
            var call = await CallMarketplaceAsync(ad, listing, marketplace, token!, "publish", runId, ct);
            await WriteCallbackAsync(ad, listing, runId, action, call, ct);
            if (call.Ok)
            {
                ApplyFromCallback(listing, ad, call);
                await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
                    ListingPublishLogSteps.Applied, "info",
                    "Anúncio publicado neste marketplace.",
                    new { listing.RemoteId, listing.RemoteStatus, listing.RemotePermalink, httpStatus = call.StatusCode }, ct);
            }
            else
            {
                listing.Status = ListingStatuses.Error;
                listing.RemoteStatus = "error";
                listing.LastSyncedAt = DateTimeOffset.UtcNow;
                listing.LastSyncJson = call.TechnicalJson;
                await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
                    ListingPublishLogSteps.Failed, "error",
                    $"O marketplace recusou ou falhou a publicação (HTTP {call.StatusCode}). Veja o retorno técnico.",
                    TryParseJson(call.TechnicalJson), ct);
            }
            return;
        }

        ApplyDemoSnapshot(listing, ad);
        var demo = DemoCallback(listing, ad, "publish",
            "Marketplace não conectado. Resposta simulada — o anúncio não foi enviado ao canal.");
        await WriteCallbackAsync(ad, listing, runId, action, demo, ct);
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Applied, "warning",
            "Publicação demo local. O marketplace não recebeu o anúncio. Conecte o canal em Marketplaces e publique de novo.",
            TryParseJson(demo.TechnicalJson), ct);
    }

    public async Task CancelAsync(Advertisement ad, Listing listing, Guid runId, CancellationToken ct)
    {
        const string action = "cancel";
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Received, "info", "Pedido de cancelamento neste marketplace.",
            new { listing.Id, listing.RemoteId, listing.Status }, ct);

        var marketplace = await db.Marketplaces.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == listing.MarketplaceCode, ct);
        var cfg = await db.CompanyMarketplaceConfigs
            .FirstOrDefaultAsync(c => c.CompanyId == ad.CompanyId && c.MarketplaceCode == listing.MarketplaceCode, ct);
        var token = cfg is null ? null : await ReadTokenAsync(cfg.Id, ct);
        var linked = cfg is { IsEnabled: true, LinkStatus: LinkStatuses.Linked } && !string.IsNullOrWhiteSpace(token);

        MarketplaceCall call;
        if (linked && !string.IsNullOrWhiteSpace(listing.RemoteId) && !listing.RemoteId.StartsWith("demo-", StringComparison.OrdinalIgnoreCase))
            call = await CallMarketplaceAsync(ad, listing, marketplace, token!, "cancel", runId, ct);
        else
            call = DemoCallback(listing, ad, "cancel",
                linked
                    ? "Id remoto de demo — cancelamento só no Vilmo."
                    : "Marketplace não conectado. Cancelamento só no Vilmo.");

        listing.Status = ListingStatuses.Cancelled;
        listing.RemoteStatus = "paused";
        listing.LastSyncedAt = DateTimeOffset.UtcNow;
        listing.LastSyncJson = call.TechnicalJson;
        await WriteCallbackAsync(ad, listing, runId, action, call, ct);
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Applied, "info",
            "Anúncio cancelado neste canal (status paused).",
            new { listing.RemoteId, listing.RemoteStatus, httpStatus = call.StatusCode }, ct);
    }

    public async Task RefreshAsync(Advertisement ad, Listing listing, Guid runId, CancellationToken ct)
    {
        const string action = "refresh";
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Received, "info", "Atualizando dados online deste marketplace.",
            new { listing.Id, listing.RemoteId, listing.Status }, ct);

        var marketplace = await db.Marketplaces.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == listing.MarketplaceCode, ct);
        var cfg = await db.CompanyMarketplaceConfigs
            .FirstOrDefaultAsync(c => c.CompanyId == ad.CompanyId && c.MarketplaceCode == listing.MarketplaceCode, ct);
        var token = cfg is null ? null : await ReadTokenAsync(cfg.Id, ct);
        var linked = cfg is { IsEnabled: true, LinkStatus: LinkStatuses.Linked } && !string.IsNullOrWhiteSpace(token);

        MarketplaceCall call;
        if (linked && !string.IsNullOrWhiteSpace(listing.RemoteId) && !listing.RemoteId.StartsWith("demo-", StringComparison.OrdinalIgnoreCase))
            call = await CallMarketplaceAsync(ad, listing, marketplace, token!, "refresh", runId, ct);
        else
            call = DemoCallback(listing, ad, "refresh",
                listing.Status is ListingStatuses.Cancelled
                    ? "Canal cancelado. Snapshot local paused."
                    : linked
                        ? "Id remoto de demo — snapshot local."
                        : "Marketplace não conectado. Snapshot local.");

        await WriteCallbackAsync(ad, listing, runId, action, call, ct);
        listing.LastSyncedAt = DateTimeOffset.UtcNow;
        listing.LastSyncJson = call.TechnicalJson;
        if (listing.Status is ListingStatuses.Cancelled)
            listing.RemoteStatus = "paused";
        else if (call.Ok && TryReadRemoteId(call.Body) is { } remoteId)
        {
            listing.Status = ListingStatuses.Published;
            listing.RemoteId = remoteId;
            listing.RemoteTitle = ad.Title;
            listing.RemotePrice = ad.Price;
            listing.RemoteQuantity = ad.AvailableQuantity;
            listing.RemoteStatus = "active";
        }
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Applied, call.Ok ? "info" : "warning",
            call.Ok ? "Dados online atualizados a partir do marketplace." : "Atualização local (sem callback real do canal).",
            TryParseJson(call.TechnicalJson), ct);
    }

    async Task WriteCallbackAsync(Advertisement ad, Listing listing, Guid runId, string action, MarketplaceCall call, CancellationToken ct)
    {
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Callback, call.Ok ? "info" : "warning",
            call.Ok
                ? $"Callback do marketplace HTTP {call.StatusCode}."
                : $"Callback do marketplace HTTP {call.StatusCode} (não publicado no canal).",
            TryParseJson(call.TechnicalJson), ct);
    }

    async Task<MarketplaceCall> CallMarketplaceAsync(
        Advertisement ad, Listing listing, Marketplace? marketplace, string token, string action, Guid runId, CancellationToken ct)
    {
        var protocol = marketplace?.AuthProtocolCode ?? "";
        var baseUrl = (marketplace?.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || !protocol.Equals("OAuth2", StringComparison.OrdinalIgnoreCase))
        {
            await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
                ListingPublishLogSteps.Calling, "warning",
                $"Protocolo {protocol} ainda não executa HTTP de anúncio.",
                new { protocol, baseUrl }, ct);
            return new MarketplaceCall(false, 0, $"{baseUrl}", "", JsonSerializer.Serialize(new
            {
                source = "vilmo",
                skipped = true,
                reason = "ProtocolNotImplemented",
                protocol,
                callbackResponse = (object?)null
            }, JsonOpts));
        }

        var (method, url, payload) = BuildRequest(ad, listing, baseUrl, action);
        await logs.WriteAsync(ad.CompanyId, runId, ad.Id, listing.Id, listing.MarketplaceCode, action,
            ListingPublishLogSteps.Calling, "info",
            $"Chamando {method} {url}",
            new { method, url, request = payload }, ct);

        try
        {
            var client = httpFactory.CreateClient("marketplace");
            using var req = new HttpRequestMessage(new HttpMethod(method), url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (payload is not null)
                req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (body.Length > 16000) body = body[..16000] + "…";
            var tech = JsonSerializer.Serialize(new
            {
                source = "marketplace",
                method,
                url,
                httpStatus = (int)resp.StatusCode,
                request = payload,
                callbackResponse = TryParseJson(body)
            }, JsonOpts);
            return new MarketplaceCall(resp.IsSuccessStatusCode, (int)resp.StatusCode, url, body, tech);
        }
        catch (Exception ex)
        {
            var tech = JsonSerializer.Serialize(new
            {
                source = "marketplace",
                method,
                url,
                httpStatus = 0,
                request = payload,
                callbackResponse = (object?)null,
                error = ex.Message
            }, JsonOpts);
            return new MarketplaceCall(false, 0, url, ex.Message, tech);
        }
    }

    static (string Method, string Url, object? Payload) BuildRequest(Advertisement ad, Listing listing, string baseUrl, string action)
    {
        var attrs = ad.Attributes
            .Where(a => a.MarketplaceCode == listing.MarketplaceCode)
            .ToDictionary(a => a.FieldName, a => a.FieldValue, StringComparer.OrdinalIgnoreCase);
        if (action == "cancel" && !string.IsNullOrWhiteSpace(listing.RemoteId))
            return ("PUT", $"{baseUrl}/items/{listing.RemoteId}", new { status = "paused" });
        if (action == "refresh" && !string.IsNullOrWhiteSpace(listing.RemoteId))
            return ("GET", $"{baseUrl}/items/{listing.RemoteId}", null);
        return ("POST", $"{baseUrl}/items", new Dictionary<string, object?>
        {
            ["title"] = ad.Title,
            ["category_id"] = Attr(attrs, "categoryId"),
            ["price"] = ad.Price,
            ["currency_id"] = string.IsNullOrWhiteSpace(ad.Currency) ? "BRL" : ad.Currency,
            ["available_quantity"] = ad.AvailableQuantity,
            ["buying_mode"] = Attr(attrs, "buyingMode") ?? "buy_it_now",
            ["listing_type_id"] = Attr(attrs, "listingTypeId") ?? "gold_special",
            ["condition"] = ad.Condition,
            ["site_id"] = "MLB",
            ["seller_custom_field"] = ad.Sku
        });
    }

    static string? Attr(IReadOnlyDictionary<string, string> attrs, string key) =>
        attrs.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    async Task<string?> ReadTokenAsync(Guid configId, CancellationToken ct)
    {
        var row = await db.CompanyMarketplaceParameters.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConfigId == configId && p.ParameterKey == "AccessToken", ct);
        if (row is null || string.IsNullOrWhiteSpace(row.ParameterValue)) return null;
        if (!row.IsSecret) return row.ParameterValue;
        try { return protector.Unprotect(row.ParameterValue); }
        catch { return row.ParameterValue; }
    }

    static bool IsDemoToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Contains("demo", StringComparison.OrdinalIgnoreCase);

    static void ApplyDemoSnapshot(Listing listing, Advertisement ad)
    {
        listing.Status = ListingStatuses.Published;
        listing.RemoteId ??= $"demo-{listing.Id:N}"[..12];
        listing.RemoteTitle = ad.Title;
        listing.RemotePrice = ad.Price;
        listing.RemoteQuantity = ad.AvailableQuantity;
        listing.RemoteStatus = "active";
        listing.RemotePermalink = $"https://demo.vilmomkt.com/{listing.MarketplaceCode}/{listing.RemoteId}";
        listing.LastSyncedAt = DateTimeOffset.UtcNow;
    }

    static void ApplyFromCallback(Listing listing, Advertisement ad, MarketplaceCall call)
    {
        listing.Status = ListingStatuses.Published;
        listing.RemoteId = TryReadRemoteId(call.Body) ?? listing.RemoteId ?? $"mp-{listing.Id:N}"[..12];
        listing.RemoteTitle = ad.Title;
        listing.RemotePrice = ad.Price;
        listing.RemoteQuantity = ad.AvailableQuantity;
        listing.RemoteStatus = TryReadString(call.Body, "status") ?? "active";
        listing.RemotePermalink = TryReadString(call.Body, "permalink")
            ?? $"https://demo.vilmomkt.com/{listing.MarketplaceCode}/{listing.RemoteId}";
        listing.LastSyncedAt = DateTimeOffset.UtcNow;
        listing.LastSyncJson = call.TechnicalJson;
    }

    static MarketplaceCall DemoCallback(Listing listing, Advertisement ad, string action, string note)
    {
        var remoteId = listing.RemoteId ?? $"demo-{listing.Id:N}"[..12];
        var callback = new
        {
            id = remoteId,
            title = ad.Title,
            price = ad.Price,
            available_quantity = ad.AvailableQuantity,
            status = action == "cancel" ? "paused" : (listing.Status == ListingStatuses.Cancelled ? "paused" : "active"),
            permalink = $"https://demo.vilmomkt.com/{listing.MarketplaceCode}/{remoteId}"
        };
        var tech = JsonSerializer.Serialize(new
        {
            source = "demo",
            method = action == "refresh" ? "GET" : "POST",
            url = $"demo://{listing.MarketplaceCode}/items",
            httpStatus = 201,
            warning = note,
            request = new { ad.Sku, ad.Title, ad.Price },
            callbackResponse = callback
        }, JsonOpts);
        return new MarketplaceCall(true, 201, $"demo://{listing.MarketplaceCode}/items", JsonSerializer.Serialize(callback, JsonOpts), tech);
    }

    static object TryParseJson(string raw)
    {
        try { return JsonSerializer.Deserialize<JsonElement>(raw); }
        catch { return raw; }
    }

    static string? TryReadRemoteId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("id", out var id))
                return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        }
        catch { /* not json */ }
        return null;
    }

    static string? TryReadString(string body, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        catch { /* not json */ }
        return null;
    }

    sealed record MarketplaceCall(bool Ok, int StatusCode, string Url, string Body, string TechnicalJson);
}
