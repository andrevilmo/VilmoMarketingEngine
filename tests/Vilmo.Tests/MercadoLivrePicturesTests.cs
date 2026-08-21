using Vilmo.Services;

namespace Vilmo.Tests;

public class MercadoLivrePicturesTests
{
    [Fact]
    public void Parse_newlines_json_and_payload()
    {
        var urls = MercadoLivrePictures.Parse("""
            https://cdn.example.com/a.jpg
            https://cdn.example.com/b.jpg
            not-a-url
            ftp://x/y
            """);
        Assert.Equal(["https://cdn.example.com/a.jpg", "https://cdn.example.com/b.jpg"], urls);

        var fromJson = MercadoLivrePictures.Parse("""[{"source":"https://img.example.com/1.png"},{"url":"https://img.example.com/2.png"}]""");
        Assert.Equal(["https://img.example.com/1.png", "https://img.example.com/2.png"], fromJson);

        var payload = MercadoLivrePictures.ToPayload(urls);
        Assert.Equal("https://cdn.example.com/a.jpg", payload[0]["source"]);
        Assert.Equal("https://cdn.example.com/b.jpg", payload[1]["source"]);
    }

    [Fact]
    public void Parse_dedupes_and_caps_at_12()
    {
        var lines = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"https://img.example.com/{i}.jpg"));
        var urls = MercadoLivrePictures.Parse(lines + "\nhttps://img.example.com/1.jpg");
        Assert.Equal(12, urls.Count);
        Assert.Equal("https://img.example.com/1.jpg", urls[0]);
        Assert.Equal("https://img.example.com/12.jpg", urls[11]);
    }

    [Fact]
    public void Empty_or_invalid_is_empty()
    {
        Assert.Empty(MercadoLivrePictures.Parse(null));
        Assert.Empty(MercadoLivrePictures.Parse(""));
        Assert.Empty(MercadoLivrePictures.Parse("foto local"));
        Assert.Empty(MercadoLivrePictures.Parse("mailto:a@b.c"));
    }

    [Fact]
    public void Detects_png_jpeg_and_rejects_text()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        Assert.True(AdvertisementPictureStore.TryDetect(png, out var ext, out var mime));
        Assert.Equal(".png", ext);
        Assert.Equal("image/png", mime);

        Assert.True(AdvertisementPictureStore.TryDetect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, out ext, out mime));
        Assert.Equal(".jpg", ext);
        Assert.Equal("image/jpeg", mime);

        Assert.False(AdvertisementPictureStore.TryDetect("hello"u8, out _, out _));
        Assert.True(AdvertisementPictureStore.TryNormalizeName($"{Guid.NewGuid():N}.jpg", out var name, out var type));
        Assert.EndsWith(".jpg", name);
        Assert.Equal("image/jpeg", type);
        Assert.False(AdvertisementPictureStore.TryNormalizeName("../secret.jpg", out _, out _));
    }
}
