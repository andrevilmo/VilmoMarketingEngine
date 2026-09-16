using System.Globalization;

namespace Vilmo.Services;

public sealed record CartCsvRow(
    string SourceId,
    string Sku,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? SourceUrl,
    string? CartImageUrl,
    IReadOnlyList<string> ProductImageUrls,
    string? Description);

public static class CartCsvParser
{
    public static IReadOnlyList<CartCsvRow> Parse(string csv)
    {
        var records = ReadRecords(csv);
        if (records.Count < 2) return [];
        var header = records[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names)
        {
            foreach (var n in names)
            {
                var i = header.IndexOf(n);
                if (i >= 0) return i;
            }
            return -1;
        }
        var iId = Col("id", "sourceid", "source_id");
        var iSku = Col("sku");
        var iName = Col("name", "nome");
        var iQty = Col("quantity", "qty", "quantidade");
        var iUnit = Col("unit_price", "unitprice", "preco", "price");
        var iTotal = Col("line_total", "linetotal", "total");
        var iUrl = Col("url", "source_url");
        var iCartImg = Col("cart_image", "cartimage");
        var iImgs = Col("product_images", "productimages", "images");
        var iDesc = Col("description", "descricao");
        if (iSku < 0 || iName < 0)
            throw new ArgumentException("CsvMissingSkuOrName");

        var rows = new List<CartCsvRow>();
        for (var r = 1; r < records.Count; r++)
        {
            var cells = records[r];
            string Get(int i) => i >= 0 && i < cells.Count ? cells[i].Trim() : "";
            var sku = Get(iSku);
            var name = Get(iName);
            if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(name)) continue;
            var sourceId = Get(iId);
            if (string.IsNullOrWhiteSpace(sourceId)) sourceId = sku;
            rows.Add(new CartCsvRow(
                sourceId,
                sku,
                name,
                ParseDec(Get(iQty)),
                ParseDec(Get(iUnit)),
                ParseDec(Get(iTotal)),
                BlankToNull(Get(iUrl)),
                BlankToNull(Get(iCartImg)),
                SplitImages(Get(iImgs)),
                BlankToNull(Get(iDesc))));
        }
        return rows;
    }

    public static IReadOnlyList<string> CollectImageUrls(CartCsvRow row)
    {
        var list = new List<string>();
        void Add(string? url, bool first)
        {
            if (!IsUsableImageUrl(url)) return;
            if (list.Any(x => string.Equals(x, url, StringComparison.OrdinalIgnoreCase))) return;
            if (first) list.Insert(0, url!);
            else list.Add(url!);
        }
        foreach (var u in row.ProductImageUrls) Add(u, false);
        Add(row.CartImageUrl, true);
        return list;
    }

    public static bool IsUsableImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not "http" and not "https") return false;
        var s = uri.ToString();
        if (s.Contains("--PRODUTO_IMAGEM--", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("produto-sem-imagem", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    static decimal ParseDec(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (decimal.TryParse(raw, NumberStyles.Any, new CultureInfo("pt-BR"), out v)) return v;
        return 0;
    }

    static string? BlankToNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    static IReadOnlyList<string> SplitImages(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public static List<List<string>> ReadRecords(string csv)
    {
        var rows = new List<List<string>>();
        var cur = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            if (c == '"') { inQuotes = true; continue; }
            if (c == ',') { cur.Add(field.ToString()); field.Clear(); continue; }
            if (c == '\n')
            {
                if (field.Length > 0 && field[^1] == '\r') field.Length--;
                cur.Add(field.ToString());
                field.Clear();
                if (cur.Exists(x => x.Length > 0)) rows.Add(cur);
                cur = [];
                continue;
            }
            field.Append(c);
        }
        if (field.Length > 0 || cur.Count > 0)
        {
            cur.Add(field.ToString());
            if (cur.Exists(x => x.Length > 0)) rows.Add(cur);
        }
        return rows;
    }
}
