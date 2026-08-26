using Vilmo.Data;

namespace Vilmo.Marketplaces;

public sealed class MarketplaceStockGateway(
    IEnumerable<IMarketplaceStockAdapter> adapters,
    IMarketplaceHttpClient http)
{
    readonly Dictionary<string, IMarketplaceStockAdapter> _adapters =
        adapters.ToDictionary(a => a.MarketplaceCode, StringComparer.OrdinalIgnoreCase);

    public async Task ApplyAsync(Listing listing, decimal quantity, CancellationToken ct)
    {
        listing.AvailableQuantity = quantity < 0 ? 0 : quantity;
        listing.LastStockPublishedAt = DateTimeOffset.UtcNow;

        if (!_adapters.TryGetValue(listing.MarketplaceCode, out var adapter))
        {
            listing.LastStockPublishError = "OperationNotBound";
            listing.LastStockPayloadJson = null;
            return;
        }

        var request = adapter.Map(listing, listing.AvailableQuantity);
        listing.LastStockPayloadJson = request.BodyJson;

        if (string.IsNullOrWhiteSpace(listing.RemoteId))
        {
            listing.LastStockPublishError = "NoRemoteId";
            return;
        }

        var result = await http.SendAsync(request, ct);
        listing.LastStockPublishError = result.Ok ? null : (result.Error ?? $"Http{result.StatusCode}");
    }
}
