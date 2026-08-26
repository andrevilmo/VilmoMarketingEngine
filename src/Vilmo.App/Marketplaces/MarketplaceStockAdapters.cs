using System.Text.Json;
using Vilmo.Data;

namespace Vilmo.Marketplaces;

static class StockQty
{
    public static int Units(decimal quantity) => (int)decimal.Truncate(quantity < 0 ? 0 : quantity);
}

public sealed class MercadoLivreStockAdapter : IMarketplaceStockAdapter
{
    public string MarketplaceCode => "MercadoLivre";

    public MarketplaceHttpRequest Map(Listing listing, decimal quantity)
    {
        var id = listing.RemoteId ?? listing.Sku;
        var body = JsonSerializer.Serialize(new { available_quantity = StockQty.Units(quantity) });
        return new MarketplaceHttpRequest(MarketplaceCode, "PUT", $"/items/{id}", body, listing.Id);
    }
}

public sealed class ShopeeStockAdapter : IMarketplaceStockAdapter
{
    public string MarketplaceCode => "Shopee";

    public MarketplaceHttpRequest Map(Listing listing, decimal quantity)
    {
        var body = JsonSerializer.Serialize(new
        {
            item_id = listing.RemoteId ?? listing.Sku,
            stock_list = new[]
            {
                new { seller_stock = new[] { new { stock = StockQty.Units(quantity) } } }
            }
        });
        return new MarketplaceHttpRequest(MarketplaceCode, "POST", "/api/v2/product/update_stock", body, listing.Id);
    }
}

public sealed class SheinStockAdapter : IMarketplaceStockAdapter
{
    public string MarketplaceCode => "Shein";

    public MarketplaceHttpRequest Map(Listing listing, decimal quantity)
    {
        var body = JsonSerializer.Serialize(new
        {
            skuCode = listing.Sku,
            productCode = listing.RemoteId ?? listing.Sku,
            quantity = StockQty.Units(quantity)
        });
        return new MarketplaceHttpRequest(MarketplaceCode, "POST", "/open-api/goods/stock", body, listing.Id);
    }
}

public sealed class MagaluStockAdapter : IMarketplaceStockAdapter
{
    public string MarketplaceCode => "Magalu";

    public MarketplaceHttpRequest Map(Listing listing, decimal quantity)
    {
        var body = JsonSerializer.Serialize(new
        {
            sku = listing.Sku,
            id = listing.RemoteId ?? listing.Sku,
            quantity = StockQty.Units(quantity)
        });
        return new MarketplaceHttpRequest(MarketplaceCode, "PUT", "/seller/v1/portfolios/stocks", body, listing.Id);
    }
}
