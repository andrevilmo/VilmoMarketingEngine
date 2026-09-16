using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Vilmo.Domain;
using Vilmo.Services;

namespace Vilmo.Tests;

public sealed class FakeCartImageDownloader : ICartImageDownloader
{
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public Task<CartImageBytes?> DownloadAsync(string url, CancellationToken ct)
    {
        if (!CartCsvParser.IsUsableImageUrl(url)) return Task.FromResult<CartImageBytes?>(null);
        if (url.Contains("fail-download", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<CartImageBytes?>(null);
        return Task.FromResult<CartImageBytes?>(new CartImageBytes(Png, "image/png", ".png"));
    }
}

public class CartCsvParserTests
{
    [Fact]
    public void Parses_quoted_description_and_pipe_images()
    {
        var csv = """
            id,sku,name,quantity,unit_price,line_total,url,cart_image,product_images,description
            111,SKU-1,"Cabo USB",2,10,20.00,https://shop.example/p1,https://cdn.example/cart.jpg,"https://cdn.example/a.jpg | https://cdn.awsli.com.br/1200x1200/--PRODUTO_IMAGEM-- | https://cdn.example/b.webp","Marca: X, Modelo: Y"
            """;
        var rows = CartCsvParser.Parse(csv);
        Assert.Single(rows);
        Assert.Equal("111", rows[0].SourceId);
        Assert.Equal("SKU-1", rows[0].Sku);
        Assert.Equal(2, rows[0].Quantity);
        Assert.Equal("Marca: X, Modelo: Y", rows[0].Description);
        var urls = CartCsvParser.CollectImageUrls(rows[0]);
        Assert.DoesNotContain(urls, u => u.Contains("--PRODUTO_IMAGEM--"));
        Assert.Contains(urls, u => u.Contains("cart.jpg"));
        Assert.Equal(3, urls.Count);
    }

    [Fact]
    public void Skips_placeholder_gif()
    {
        Assert.False(CartCsvParser.IsUsableImageUrl("https://cdn.awsli.com.br/production/static/img/produto-sem-imagem.gif"));
        Assert.True(CartCsvParser.IsUsableImageUrl("https://cdn.example/ok.png"));
    }
}

public class CartImportApiTests : IClassFixture<ApiFactory>
{
    readonly ApiFactory _factory;
    readonly HttpClient _client;

    public CartImportApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    async Task<(string Token, Guid CompanyId)> LoginAdminAsync()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new { email = "admin@vilmomkt.com", password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;
        var companyId = doc.RootElement.GetProperty("user").GetProperty("memberships")[0].GetProperty("companyId").GetGuid();
        return (token, companyId);
    }

    HttpRequestMessage Authed(HttpMethod method, string url, string token, Guid companyId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("X-Company-Id", companyId.ToString());
        return req;
    }

    static string SampleCsv() =>
        """
        id,sku,name,quantity,unit_price,line_total,url,cart_image,product_images,description
        403530441,Z9WY3HQML,CABO USB IPHONE,2,16,32.00,https://shop.example/cabo,https://cdn.example/cart.png,"https://cdn.example/a.png | https://cdn.awsli.com.br/1200x1200/--PRODUTO_IMAGEM-- | https://cdn.example/fail-download.png","Cabo USB para iPhone 12W"
        """;

    [Fact]
    public async Task Admin_imports_csv_downloads_images_and_links_to_nfe_product()
    {
        var (token, companyId) = await LoginAdminAsync();
        var csv = SampleCsv();
        var importReq = Authed(HttpMethod.Post, "/cart-imports", token, companyId);
        importReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        var form = new MultipartFormDataContent();
        form.Add(file, "file", "cart.csv");
        importReq.Content = form;
        var importRes = await _client.SendAsync(importReq);
        Assert.Equal(HttpStatusCode.Accepted, importRes.StatusCode);
        using var importDoc = JsonDocument.Parse(await importRes.Content.ReadAsStringAsync());
        var batchId = importDoc.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(1, importDoc.RootElement.GetProperty("rowCount").GetInt32());

        await _factory.DrainCartAsync();

        var batchReq = Authed(HttpMethod.Get, $"/cart-imports/{batchId}", token, companyId);
        var batchRes = await _client.SendAsync(batchReq);
        batchRes.EnsureSuccessStatusCode();
        using var batchDoc = JsonDocument.Parse(await batchRes.Content.ReadAsStringAsync());
        Assert.Equal("Done", batchDoc.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, batchDoc.RootElement.GetProperty("imageOk").GetInt32());

        var listReq = Authed(HttpMethod.Get, "/cart-products", token, companyId);
        var listRes = await _client.SendAsync(listReq);
        listRes.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await listRes.Content.ReadAsStringAsync());
        Assert.Equal(1, listDoc.RootElement.GetArrayLength());
        var cart = listDoc.RootElement[0];
        var cartId = cart.GetProperty("id").GetGuid();
        Assert.Equal("Z9WY3HQML", cart.GetProperty("sku").GetString());
        Assert.Equal(2, cart.GetProperty("imageCount").GetInt32());
        var imageId = cart.GetProperty("images").EnumerateArray().First(i => i.GetProperty("status").GetString() == "Saved").GetProperty("id").GetGuid();

        var imgReq = Authed(HttpMethod.Get, $"/cart-products/{cartId}/images/{imageId}", token, companyId);
        var imgRes = await _client.SendAsync(imgReq);
        imgRes.EnsureSuccessStatusCode();
        Assert.Equal("image/png", imgRes.Content.Headers.ContentType?.MediaType);
        Assert.True((await imgRes.Content.ReadAsByteArrayAsync()).Length > 10);

        var first43 = "4226086843137100016155500100000000312345678";
        var chave = first43 + ChaveAcesso.Dv(first43);
        var xml = XmlParseTests.SampleXml(chave, "11222333000181", "68431371000161");
        var xmlReq = Authed(HttpMethod.Post, "/nfe/xml", token, companyId);
        xmlReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        xmlReq.Content = new StringContent(xml, Encoding.UTF8, "application/xml");
        var xmlRes = await _client.SendAsync(xmlReq);
        xmlRes.EnsureSuccessStatusCode();

        var linkReq = Authed(HttpMethod.Post, $"/cart-products/{cartId}/link", token, companyId);
        linkReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        linkReq.Content = JsonContent.Create(new { nfeChave = chave, nItem = 1 });
        var linkRes = await _client.SendAsync(linkReq);
        linkRes.EnsureSuccessStatusCode();
        using var linkDoc = JsonDocument.Parse(await linkRes.Content.ReadAsStringAsync());
        Assert.Equal("CAMISETA-XML", linkDoc.RootElement.GetProperty("productSku").GetString());
        Assert.Equal(2, linkDoc.RootElement.GetProperty("imageCount").GetInt32());

        var prodReq = Authed(HttpMethod.Get, "/products/CAMISETA-XML", token, companyId);
        var prodRes = await _client.SendAsync(prodReq);
        prodRes.EnsureSuccessStatusCode();
        using var prodDoc = JsonDocument.Parse(await prodRes.Content.ReadAsStringAsync());
        Assert.Equal("Cabo USB para iPhone 12W", prodDoc.RootElement.GetProperty("description").GetString());
        Assert.Equal("CABO USB IPHONE", prodDoc.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, prodDoc.RootElement.GetProperty("images").GetArrayLength());
        Assert.Equal(cartId, prodDoc.RootElement.GetProperty("linkedCartProductId").GetGuid());

        var nfeReq = Authed(HttpMethod.Get, $"/nfe/chaves/{chave}", token, companyId);
        var nfeRes = await _client.SendAsync(nfeReq);
        nfeRes.EnsureSuccessStatusCode();
        using var nfeDoc = JsonDocument.Parse(await nfeRes.Content.ReadAsStringAsync());
        Assert.Equal(1, nfeDoc.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(1, nfeDoc.RootElement.GetProperty("cartLinks").GetArrayLength());
    }

    [Fact]
    public async Task Vendor_cannot_import_cart()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new { email = "vendedor@vilmomkt.com", password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;
        var req = new HttpRequestMessage(HttpMethod.Post, "/cart-imports");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new MultipartFormDataContent
        {
            { new StringContent(SampleCsv(), Encoding.UTF8, "text/csv"), "file", "cart.csv" }
        };
        var denied = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
}
