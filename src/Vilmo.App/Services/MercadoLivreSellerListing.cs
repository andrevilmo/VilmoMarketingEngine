using System.Text.Json;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

/// <summary>
/// Mercado Livre blocks POST /items with 403 seller.unable_to_list when the
/// seller account is missing address (cause address_pending). The item body is
/// not the problem — the ML user profile is.
/// </summary>
public static class MercadoLivreSellerListing
{
    public const string AddressesUrl = "https://myaccount.mercadolivre.com.br/addresses";

    public static IReadOnlyList<string> ListCodes(JsonElement user)
    {
        var codes = new List<string>();
        CollectCodes(user, "list", codes);
        CollectCodes(user, "sell", codes);
        return codes;
    }

    public static bool IsSellerProfile(JsonElement user)
    {
        if (user.ValueKind != JsonValueKind.Object) return false;
        if (!user.TryGetProperty("id", out _)) return false;
        var code = Str(user, "code") ?? Str(user, "error");
        if (string.Equals(code, "unauthorized", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(Str(user, "message"), "invalid access token", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public static bool CanList(JsonElement user)
    {
        if (!IsSellerProfile(user)) return false;
        var codes = ListCodes(user);
        if (NeedsAddress(codes) || NeedsPhone(codes) || NeedsIdentification(codes))
            return false;
        if (TryStatusAllow(user, "list", out var allow))
            return allow;
        return codes.Count == 0;
    }

    public static bool NeedsAddress(IEnumerable<string> codes) =>
        codes.Any(c => c.Contains("address", StringComparison.OrdinalIgnoreCase));

    public static bool NeedsPhone(IEnumerable<string> codes) =>
        codes.Any(c => c.Contains("phone", StringComparison.OrdinalIgnoreCase));

    public static bool NeedsIdentification(IEnumerable<string> codes) =>
        codes.Any(c => c.Contains("identification", StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> CausesFromHttpBody(string? body)
    {
        var codes = new List<string>();
        if (string.IsNullOrWhiteSpace(body)) return codes;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var el = doc.RootElement;
            if (el.TryGetProperty("cause", out var cause))
            {
                if (cause.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in cause.EnumerateArray())
                    {
                        if (c.ValueKind == JsonValueKind.String)
                        {
                            var s = c.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) codes.Add(s);
                        }
                        else
                        {
                            var m = Str(c, "code") ?? Str(c, "message");
                            if (!string.IsNullOrWhiteSpace(m)) codes.Add(m);
                        }
                    }
                }
                else if (cause.ValueKind == JsonValueKind.String)
                {
                    var s = cause.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) codes.Add(s);
                }
            }
        }
        catch (JsonException)
        {
            /* keep empty */
        }
        return codes;
    }

    public static string UserMessage(IEnumerable<string> codes)
    {
        var list = codes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (NeedsAddress(list))
            return "O Mercado Livre bloqueou o anúncio porque o endereço da conta vendedora está incompleto (address_pending). Abra Meu perfil → Endereços no Mercado Livre, preencha rua, número, cidade, UF e CEP, salve e marque o endereço para uso. Depois publique de novo. Vilmo envia o endereço da empresa automaticamente antes de POST /items.";
        if (NeedsPhone(list))
            return "O Mercado Livre bloqueou o anúncio porque falta o telefone da conta vendedora. Complete o telefone em Meu perfil no Mercado Livre e publique de novo.";
        if (NeedsIdentification(list))
            return "O Mercado Livre bloqueou o anúncio porque o CPF/CNPJ da conta vendedora está pendente. Complete a identificação em Meu perfil no Mercado Livre e publique de novo.";
        if (list.Count > 0)
            return $"O Mercado Livre não permite anunciar nesta conta ({string.Join(", ", list)}). Complete os dados em Minha conta e tente a primeira publicação manual no site.";
        return "O Mercado Livre recusou a publicação (seller.unable_to_list). Complete os dados da conta vendedora em Minha conta.";
    }

    public static string UserMessageFromHttp(int status, string? body)
    {
        if (status == 401 || (body ?? "").Contains("invalid access token", StringComparison.OrdinalIgnoreCase))
            return "Token do Mercado Livre inválido ou expirado. Reconecte o canal em Marketplaces e publique de novo.";
        var causes = CausesFromHttpBody(body);
        if (status == 403 || causes.Count > 0)
        {
            var msg = UserMessage(causes);
            if (causes.Count > 0 || (body ?? "").Contains("unable_to_list", StringComparison.OrdinalIgnoreCase))
                return msg;
        }
        return $"O marketplace recusou ou falhou a publicação (HTTP {status}). Veja o retorno técnico.";
    }

    public static Dictionary<string, object?> UserUpdateBody(Company company)
    {
        var uf = (company.Uf ?? "").Trim().ToUpperInvariant();
        var state = uf.Length == 2 ? $"BR-{uf}" : uf;
        var line = string.Join(" ", new[] { company.Street, company.Number, company.Complement }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var body = new Dictionary<string, object?>
        {
            ["address"] = line,
            ["state"] = state,
            ["city"] = company.City,
            ["zip_code"] = Cep.Digits(company.Cep)
        };
        if (TrySplitPhone(company.Phone, out var area, out var number))
            body["phone"] = new Dictionary<string, object?> { ["area_code"] = area, ["number"] = number };
        if (Cnpj.IsValid(company.Cnpj))
            body["identification"] = new Dictionary<string, object?>
            {
                ["type"] = "CNPJ",
                ["number"] = Cnpj.Digits(company.Cnpj)
            };
        if (!string.IsNullOrWhiteSpace(company.LegalName))
            body["company"] = new Dictionary<string, object?>
            {
                ["corporate_name"] = company.LegalName,
                ["brand_name"] = string.IsNullOrWhiteSpace(company.TradeName) ? company.LegalName : company.TradeName
            };
        return body;
    }

    public static Dictionary<string, object?> AddressCreateBody(Company company)
    {
        var uf = (company.Uf ?? "").Trim().ToUpperInvariant();
        var stateId = uf.Length == 2 ? $"BR-{uf}" : uf;
        var line = string.Join(" ", new[] { company.Street, company.Number }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        return new Dictionary<string, object?>
        {
            ["address_line"] = line,
            ["street_name"] = company.Street,
            ["street_number"] = company.Number,
            ["comment"] = company.Complement,
            ["zip_code"] = Cep.Digits(company.Cep),
            ["city"] = new Dictionary<string, object?> { ["name"] = company.City },
            ["state"] = new Dictionary<string, object?> { ["id"] = stateId },
            ["country"] = new Dictionary<string, object?> { ["id"] = "BR" },
            ["neighborhood"] = new Dictionary<string, object?> { ["name"] = company.Neighborhood }
        };
    }

    public static bool TrySplitPhone(string? raw, out string area, out string number)
    {
        area = "";
        number = "";
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 11) return false;
        area = digits[..2];
        number = digits[2..];
        return true;
    }

    public static object PublicStatus(JsonElement? me, bool connected)
    {
        if (!connected || me is null)
            return new
            {
                connected,
                canList = false,
                codes = Array.Empty<string>(),
                nickname = (string?)null,
                address = (object?)null,
                fixUrl = AddressesUrl,
                message = connected
                    ? "Não lemos o perfil do vendedor no Mercado Livre."
                    : "Conecte o Mercado Livre em Marketplaces para publicar."
            };
        var user = me.Value;
        var codes = ListCodes(user);
        var can = CanList(user);
        return new
        {
            connected = true,
            canList = can,
            codes,
            nickname = Str(user, "nickname"),
            address = AddressSummary(user),
            fixUrl = AddressesUrl,
            message = can ? "A conta Mercado Livre pode anunciar." : UserMessage(codes)
        };
    }

    static object? AddressSummary(JsonElement user)
    {
        if (!user.TryGetProperty("address", out var a) || a.ValueKind != JsonValueKind.Object)
            return null;
        return new
        {
            state = Str(a, "state"),
            city = Str(a, "city"),
            address = Str(a, "address"),
            zipCode = Str(a, "zip_code")
        };
    }

    static void CollectCodes(JsonElement user, string statusKey, List<string> codes)
    {
        if (!user.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Object)
            return;
        if (!status.TryGetProperty(statusKey, out var list) || list.ValueKind != JsonValueKind.Object)
            return;
        if (!list.TryGetProperty("codes", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;
        foreach (var c in arr.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.String) continue;
            var s = c.GetString();
            if (!string.IsNullOrWhiteSpace(s) && !codes.Contains(s, StringComparer.OrdinalIgnoreCase))
                codes.Add(s);
        }
    }

    static bool TryStatusAllow(JsonElement user, string statusKey, out bool allow)
    {
        allow = false;
        if (!user.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Object)
            return false;
        if (!status.TryGetProperty(statusKey, out var list) || list.ValueKind != JsonValueKind.Object)
            return false;
        if (!list.TryGetProperty("allow", out var a))
            return false;
        if (a.ValueKind == JsonValueKind.True) { allow = true; return true; }
        if (a.ValueKind == JsonValueKind.False) { allow = false; return true; }
        return false;
    }

    static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
