using System.Text.RegularExpressions;

namespace Vilmo.Services;

/// <summary>
/// Mercado Livre <c>category_id</c> must be a URL-friendly MLB code (e.g. MLB5672), never the category name.
/// </summary>
public static partial class MercadoLivreCategoryId
{
    [GeneratedRegex(@"MLB\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodeInText();

    [GeneratedRegex(@"^MLB\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactCode();

    public static bool TryNormalize(string? raw, out string id)
    {
        id = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        if (ExactCode().IsMatch(t))
        {
            id = t.ToUpperInvariant();
            return true;
        }
        var m = CodeInText().Match(t);
        if (!m.Success) return false;
        id = m.Value.ToUpperInvariant();
        return true;
    }
}
