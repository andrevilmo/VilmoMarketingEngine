using System.Text.RegularExpressions;

namespace Vilmo.Domain;

public static class Cnpj
{
    public static string Digits(string raw) => new(raw.Where(char.IsDigit).ToArray());

    public static string Complete(string first12)
    {
        var d = Digits(first12);
        if (d.Length != 12) throw new ArgumentException("Need 12 digits");
        var dv1 = Check(d, 12);
        var dv2 = Check(d + dv1, 13);
        return d + dv1 + dv2;
    }

    public static bool IsValid(string raw)
    {
        var d = Digits(raw);
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;
        return Check(d, 12) == d[12] - '0' && Check(d, 13) == d[13] - '0';
    }

    public static string Format(string raw)
    {
        var d = Digits(raw);
        if (d.Length != 14) return raw;
        return $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}";
    }

    static int Check(string d, int len)
    {
        int[] w = len == 12
            ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
            : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum = 0;
        for (var i = 0; i < w.Length; i++) sum += (d[i] - '0') * w[i];
        var r = sum % 11;
        return r < 2 ? 0 : 11 - r;
    }
}

public static class ChaveAcesso
{
    static readonly Regex DigitsOnly = new("^[0-9]{44}$", RegexOptions.Compiled);

    public static string Digits(string raw) => new(raw.Where(char.IsDigit).ToArray());

    public static bool TryNormalize(string raw, out string chave)
    {
        chave = Digits(raw);
        if (!DigitsOnly.IsMatch(chave)) return false;
        return Dv(chave[..43]) == chave[43] - '0';
    }

    /// <summary>
    /// Pull a valid 44-digit NF-e chave from a DANFE barcode/QR payload (raw digits, chNFe=, NFC-e p=).
    /// </summary>
    public static bool TryExtractFromPayload(string? raw, out string chave)
    {
        chave = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var chNfe = Regex.Match(raw, @"chNFe=(\d{44})", RegexOptions.IgnoreCase);
        if (chNfe.Success && TryNormalize(chNfe.Groups[1].Value, out chave)) return true;

        var pQuery = Regex.Match(raw, @"[?&]p=([^&]+)", RegexOptions.IgnoreCase);
        if (pQuery.Success)
        {
            var p = Uri.UnescapeDataString(pQuery.Groups[1].Value.Replace("+", "%20"));
            var head = p.Split('|')[0];
            if (TryNormalize(head, out chave)) return true;
        }

        var digits = Digits(raw);
        for (var i = 0; i + 44 <= digits.Length; i++)
        {
            if (TryNormalize(digits.Substring(i, 44), out chave)) return true;
        }
        return false;
    }

    public static int Dv(string first43)
    {
        var weight = 2;
        var sum = 0;
        for (var i = first43.Length - 1; i >= 0; i--)
        {
            sum += (first43[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }
        var r = 11 - sum % 11;
        return r is 0 or 1 or 10 or 11 ? 0 : r;
    }
}

public static class Cep
{
    public static string Digits(string raw) => new(raw.Where(char.IsDigit).ToArray());
    public static bool IsValid(string raw) => Digits(raw).Length == 8;
}
