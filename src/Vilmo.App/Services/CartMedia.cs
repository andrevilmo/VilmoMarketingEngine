namespace Vilmo.Services;

public sealed record CartImageBytes(byte[] Bytes, string ContentType, string Extension);

public interface ICartImageDownloader
{
    Task<CartImageBytes?> DownloadAsync(string url, CancellationToken ct);
}

public interface ICartMediaStore
{
    string Root { get; }
    Task<string> SaveAsync(Guid companyId, Guid cartProductId, Guid imageId, string extension, byte[] bytes, CancellationToken ct);
    string AbsolutePath(string relativePath);
}

public sealed class FileCartMediaStore(IConfiguration config) : ICartMediaStore
{
    public string Root { get; } = config["CART_MEDIA_ROOT"]
        ?? Path.Combine(Path.GetTempPath(), "vilmo-cart-media");

    public async Task<string> SaveAsync(Guid companyId, Guid cartProductId, Guid imageId, string extension, byte[] bytes, CancellationToken ct)
    {
        var ext = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.StartsWith('.') ? extension : "." + extension;
        var rel = Path.Combine(companyId.ToString("N"), cartProductId.ToString("N"), imageId.ToString("N") + ext)
            .Replace('\\', '/');
        var abs = AbsolutePath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        await File.WriteAllBytesAsync(abs, bytes, ct);
        return rel;
    }

    public string AbsolutePath(string relativePath)
    {
        var safe = relativePath.Replace('\\', '/').TrimStart('/');
        if (safe.Contains("..", StringComparison.Ordinal)) throw new InvalidOperationException("BadPath");
        return Path.Combine(Root, safe.Replace('/', Path.DirectorySeparatorChar));
    }
}

public sealed class HttpCartImageDownloader(IHttpClientFactory http) : ICartImageDownloader
{
    const int MaxBytes = 8 * 1024 * 1024;

    public async Task<CartImageBytes?> DownloadAsync(string url, CancellationToken ct)
    {
        if (!CartCsvParser.IsUsableImageUrl(url)) return null;
        var client = http.CreateClient("cart-images");
        using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode) return null;
        var type = res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return null;
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buf = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buf, ct)) > 0)
        {
            if (ms.Length + read > MaxBytes) return null;
            ms.Write(buf, 0, read);
        }
        if (ms.Length < 32) return null;
        var ext = Ext(type, url);
        return new CartImageBytes(ms.ToArray(), type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? type : GuessType(ext), ext);
    }

    static string Ext(string contentType, string url)
    {
        if (contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase)) return ".jpg";
        if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase)) return ".png";
        if (contentType.Contains("webp", StringComparison.OrdinalIgnoreCase)) return ".webp";
        if (contentType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return ".gif";
        var path = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.AbsolutePath : url;
        var e = Path.GetExtension(path);
        return e is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" ? e : ".jpg";
    }

    static string GuessType(string ext) => ext switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg"
    };
}
