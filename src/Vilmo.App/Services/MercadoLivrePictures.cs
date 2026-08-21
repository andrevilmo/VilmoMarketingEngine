using System.Text.Json;

namespace Vilmo.Services;

/// <summary>
/// Mercado Livre POST /items <c>pictures</c> from saved advertisement URLs.
/// Clássica (<c>gold_special</c>) rejects listings without photos
/// (<c>item.listing_type_id.requiresPictures</c>).
/// </summary>
public static class MercadoLivrePictures
{
    public const string FieldName = "pictures";
    public const int MaxCount = 12;

    public static List<string> Parse(string? raw)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in Candidates(raw))
        {
            if (!TryNormalizeUrl(candidate, out var url)) continue;
            if (!seen.Add(url)) continue;
            result.Add(url);
            if (result.Count >= MaxCount) break;
        }
        return result;
    }

    public static string Serialize(IEnumerable<string> urls) =>
        string.Join('\n', urls);

    public static List<Dictionary<string, object?>> ToPayload(IReadOnlyList<string> urls) =>
        urls.Select(u => new Dictionary<string, object?> { ["source"] = u }).ToList();

    public static bool TryNormalizeUrl(string? raw, out string url)
    {
        url = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        if (string.IsNullOrWhiteSpace(uri.Host)) return false;
        url = uri.ToString();
        return true;
    }

    static IEnumerable<string> Candidates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        var t = raw.Trim();
        if (t.StartsWith('['))
        {
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(t);
            }
            catch (JsonException)
            {
                doc = null;
            }
            if (doc is not null)
            {
                using (doc)
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var fromJson = FromJson(el);
                            if (!string.IsNullOrWhiteSpace(fromJson))
                                yield return fromJson;
                        }
                        yield break;
                    }
                }
            }
        }
        foreach (var part in t.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return part;
    }

    static string FromJson(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? "";
        if (el.ValueKind != JsonValueKind.Object)
            return "";
        if (el.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String)
            return source.GetString() ?? "";
        if (el.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            return url.GetString() ?? "";
        return "";
    }
}
