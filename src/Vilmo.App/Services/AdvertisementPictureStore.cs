using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Vilmo.Services;

/// <summary>
/// Stores Mercado Livre listing photos on disk and builds the public URL
/// Mercado Livre fetches as <c>pictures.source</c>.
/// </summary>
public sealed class AdvertisementPictureStore(IConfiguration config)
{
    public const long MaxBytes = 8 * 1024 * 1024;

    static readonly Dictionary<string, string> ExtToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    public string DirectoryPath
    {
        get
        {
            var dir = config["Pictures:Directory"] ?? config["PICTURES_DIRECTORY"];
            if (!string.IsNullOrWhiteSpace(dir)) return dir;
            return Directory.Exists("/data/pictures")
                ? "/data/pictures"
                : Path.Combine(Path.GetTempPath(), "vilmo-pictures");
        }
    }

    public async Task<(string FileName, string ContentType)> SaveAsync(Stream input, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await input.CopyToAsync(ms, ct);
        if (ms.Length == 0)
            throw new ArgumentException("PictureEmpty");
        if (ms.Length > MaxBytes)
            throw new ArgumentException("PictureTooLarge");
        var bytes = ms.ToArray();
        if (!TryDetect(bytes, out var ext, out var mime))
            throw new ArgumentException("InvalidPictureFile");
        var name = $"{Guid.NewGuid():N}{ext}";
        Directory.CreateDirectory(DirectoryPath);
        await File.WriteAllBytesAsync(Path.Combine(DirectoryPath, name), bytes, ct);
        return (name, mime);
    }

    public bool TryResolve(string? fileName, out string path, out string contentType)
    {
        path = "";
        contentType = "";
        if (!TryNormalizeName(fileName, out var name, out contentType))
            return false;
        var full = Path.GetFullPath(Path.Combine(DirectoryPath, name));
        var root = Path.GetFullPath(DirectoryPath);
        if (!full.StartsWith(root, StringComparison.Ordinal))
            return false;
        if (!File.Exists(full))
            return false;
        path = full;
        return true;
    }

    public static string PublicFileUrl(HttpContext http, IConfiguration config, string fileName)
    {
        var pub = (config["PUBLIC_BASE_URL"] ?? "").Trim().TrimEnd('/');
        if (pub.Length > 0)
            return $"{pub}/api/media/pictures/{fileName}";
        var prefix = http.Request.Headers["X-Forwarded-Prefix"].ToString().Trim().TrimEnd('/');
        var origin = $"{http.Request.Scheme}://{http.Request.Host}";
        return string.IsNullOrEmpty(prefix)
            ? $"{origin}/media/pictures/{fileName}"
            : $"{origin}{prefix}/media/pictures/{fileName}";
    }

    public static bool TryDetect(ReadOnlySpan<byte> bytes, out string ext, out string mime)
    {
        ext = "";
        mime = "";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            ext = ".jpg";
            mime = "image/jpeg";
            return true;
        }
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            ext = ".png";
            mime = "image/png";
            return true;
        }
        if (bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            ext = ".webp";
            mime = "image/webp";
            return true;
        }
        return false;
    }

    public static bool TryNormalizeName(string? fileName, out string name, out string contentType)
    {
        name = "";
        contentType = "";
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var n = Path.GetFileName(fileName.Trim()) ?? "";
        if (n.Length == 0) return false;
        var ext = Path.GetExtension(n);
        if (!ExtToMime.TryGetValue(ext, out var mime) || string.IsNullOrEmpty(mime))
            return false;
        contentType = mime;
        var stem = Path.GetFileNameWithoutExtension(n);
        if (stem.Length != 32) return false;
        foreach (var c in stem)
        {
            var ok = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if (!ok) return false;
        }
        name = stem.ToLowerInvariant() + ext.ToLowerInvariant();
        return true;
    }
}
