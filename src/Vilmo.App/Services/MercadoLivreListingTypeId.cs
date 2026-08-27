using System.Text.RegularExpressions;

namespace Vilmo.Services;

/// <summary>
/// Mercado Livre <c>listing_type_id</c> must be a URL-friendly code
/// (e.g. gold_special), never a product name or Portuguese label.
/// </summary>
public static partial class MercadoLivreListingTypeId
{
    public const string Default = "gold_special";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        "gold_special",
        "gold_pro",
        "gold",
        "gold_premium",
        "silver",
        "bronze",
        "free"
    };

    [GeneratedRegex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactCode();

    [GeneratedRegex(@"\(([a-z][a-z0-9]*(?:_[a-z0-9]+)*)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodeInParens();

    public static bool TryNormalize(string? raw, out string id)
    {
        id = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        var paren = CodeInParens().Match(t);
        if (paren.Success)
            t = paren.Groups[1].Value;
        t = t.ToLowerInvariant();
        if (!ExactCode().IsMatch(t)) return false;
        if (!Known.Contains(t) && !t.Contains('_')) return false;
        id = t;
        return true;
    }
}
