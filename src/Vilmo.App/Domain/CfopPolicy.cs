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
        var company = Cnpj.Digits(companyCnpj);
        var emit = Cnpj.Digits(emitCnpj);
        var dest = Cnpj.Digits(destCnpj);
        var code = new string(cfop.Where(char.IsDigit).ToArray());
        var group = code.Length >= 4 ? code[0] : '\0';

        if (dest == company && group is '1' or '2')
            return CfopMovement.InboundPurchaseOrReturn;
        if (emit == company && group is '5' or '6')
            return CfopMovement.OutboundSaleAlreadyStockedAtPaid;
        // XML / DistDFe from any other emit+dest: load products into this company's stock.
        if (emit != company && dest != company)
            return CfopMovement.InboundPurchaseOrReturn;
        return CfopMovement.Ignore;
    }
}
