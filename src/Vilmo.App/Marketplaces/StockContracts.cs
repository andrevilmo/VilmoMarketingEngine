using Vilmo.Data;

namespace Vilmo.Marketplaces;

/// <summary>Canonical stock push. Core inventory never calls a marketplace HTTP API.</summary>
public sealed record StockUpdateDto(string Sku, decimal Quantity, string? RemoteListingId);

public sealed record MarketplaceHttpRequest(
    string MarketplaceCode,
    string Method,
    string Path,
    string BodyJson,
    Guid ListingId);

public sealed record MarketplaceHttpResult(bool Ok, int StatusCode, string? Error);

public interface IMarketplaceStockAdapter
{
    string MarketplaceCode { get; }
    MarketplaceHttpRequest Map(Listing listing, decimal quantity);
}

public interface IMarketplaceHttpClient
{
    Task<MarketplaceHttpResult> SendAsync(MarketplaceHttpRequest request, CancellationToken ct);
}

/// <summary>
/// Records mapped stock updates. Live HTTP is opt-in later via a real client;
/// demo/OAuth stubs must not hit production marketplace APIs from ERP.
/// </summary>
public sealed class RecordingMarketplaceHttpClient : IMarketplaceHttpClient
{
    readonly List<MarketplaceHttpRequest> _sent = [];
    readonly object _gate = new();

    public IReadOnlyList<MarketplaceHttpRequest> Sent
    {
        get { lock (_gate) return [.. _sent]; }
    }

    public Task<MarketplaceHttpResult> SendAsync(MarketplaceHttpRequest request, CancellationToken ct)
    {
        lock (_gate) _sent.Add(request);
        return Task.FromResult(new MarketplaceHttpResult(true, 200, null));
    }
}
