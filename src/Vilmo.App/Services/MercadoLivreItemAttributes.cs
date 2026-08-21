using System.Text.Json;

namespace Vilmo.Services;

/// <summary>
/// Builds Mercado Livre POST /items <c>attributes</c> from saved advertisement fields.
/// Required category attributes cannot be N/A (value_id -1).
/// </summary>
public static class MercadoLivreItemAttributes
{
    public const string FieldPrefix = "ml:";
    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "categoryId", "listingTypeId", "buyingMode", "shippingMode", "familyName"
    };

    public static bool IsNotApplicable(string? valueId, string? valueName)
    {
        if (valueId is "-1" or "–1") return true;
        var n = (valueName ?? "").Trim();
        if (n.Length == 0) return false;
        return n.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || n.Equals("NA", StringComparison.OrdinalIgnoreCase)
            || n.Equals("N.A.", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Não aplicável", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Nao aplicavel", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Não se aplica", StringComparison.OrdinalIgnoreCase);
    }

    public static string FieldName(string attributeId) => FieldPrefix + attributeId.Trim();

    public static bool TryAttributeId(string fieldName, out string id)
    {
        id = "";
        if (string.IsNullOrWhiteSpace(fieldName)) return false;
        var t = fieldName.Trim();
        if (t.StartsWith(FieldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            id = t[FieldPrefix.Length..].Trim().ToUpperInvariant();
            return id.Length > 0 && !Reserved.Contains(id);
        }
        return false;
    }

    public static List<Dictionary<string, object?>> Build(
        IReadOnlyDictionary<string, string> attrs, string? brand, string? gtin)
    {
        var result = new List<Dictionary<string, object?>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in attrs)
        {
            if (!TryAttributeId(kv.Key, out var id)) continue;
            if (!TryEntry(id, kv.Value, out var row)) continue;
            result.Add(row);
            seen.Add(id);
        }
        if (seen.Add("BRAND") && !string.IsNullOrWhiteSpace(brand) && !IsNotApplicable(null, brand))
            result.Add(Entry("BRAND", null, brand.Trim()));
        if (seen.Add("GTIN") && !string.IsNullOrWhiteSpace(gtin) && !IsNotApplicable(null, gtin))
            result.Add(Entry("GTIN", null, gtin.Trim()));
        return result;
    }

    public static bool TryEntry(string id, string? raw, out Dictionary<string, object?> row)
    {
        row = [];
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        string? valueId = null;
        string? valueName = t;
        if (t.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(t);
                var el = doc.RootElement;
                valueId = Str(el, "value_id") ?? Str(el, "valueId");
                valueName = Str(el, "value_name") ?? Str(el, "valueName") ?? valueName;
            }
            catch (JsonException)
            {
                valueName = t;
            }
        }
        else if (t.Contains('|', StringComparison.Ordinal))
        {
            var parts = t.Split('|', 2);
            valueId = parts[0].Trim();
            valueName = parts.Length > 1 ? parts[1].Trim() : valueId;
        }
        if (IsNotApplicable(valueId, valueName)) return false;
        if (string.IsNullOrWhiteSpace(valueId) && string.IsNullOrWhiteSpace(valueName)) return false;
        row = Entry(id, valueId, valueName);
        return true;
    }

    static Dictionary<string, object?> Entry(string id, string? valueId, string? valueName)
    {
        var row = new Dictionary<string, object?> { ["id"] = id };
        if (!string.IsNullOrWhiteSpace(valueId)) row["value_id"] = valueId.Trim();
        if (!string.IsNullOrWhiteSpace(valueName)) row["value_name"] = valueName.Trim();
        return row;
    }

    static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
