namespace Vilmo.Domain;

public enum CfopMovement
{
    Ignore = 0,
    InboundPurchaseOrReturn = 1,
    OutboundSaleAlreadyStockedAtPaid = 2
}

public static class CfopPolicy
{
    public static CfopMovement Classify(string cfop, string emitCnpj, string destCnpj, string companyCnpj)
    {
        var code = new string(cfop.Where(char.IsDigit).ToArray());
        if (code.Length < 4) return CfopMovement.Ignore;
        var group = code[0];
        var company = Cnpj.Digits(companyCnpj);
        var emit = Cnpj.Digits(emitCnpj);
        var dest = Cnpj.Digits(destCnpj);

        if (dest == company && group is '1' or '2')
            return CfopMovement.InboundPurchaseOrReturn;
        if (emit == company && group is '5' or '6')
            return CfopMovement.OutboundSaleAlreadyStockedAtPaid;
        return CfopMovement.Ignore;
    }
}
