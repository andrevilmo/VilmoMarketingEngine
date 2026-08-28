namespace Vilmo.Marketplaces;

public sealed record RemoteMarketplaceAd(
    string RemoteId,
    string Title,
    decimal? Price,
    decimal? Quantity,
    string? Status,
    string? Permalink,
    string? Thumbnail,
    string? SellerCustomField,
    string? Gtin,
    string? CategoryId,
    string? ListingTypeId,
    IReadOnlyList<string> PictureUrls,
    string? BuyingMode,
    string? ShippingMode,
    string SnapshotJson);

public sealed record MarketplaceCatalogContext(
    Guid CompanyId,
    string AccessToken,
    string BaseUrl,
    string? SellerId);

public interface IMarketplaceListingCatalogAdapter
{
    string MarketplaceCode { get; }
    Task<IReadOnlyList<RemoteMarketplaceAd>> ListAdsAsync(MarketplaceCatalogContext ctx, CancellationToken ct);
}
