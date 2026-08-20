using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

public sealed class NfeIngestService(AppDbContext db, InventoryService inventory)
{
    static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public async Task<object> EnqueueChaveAsync(Guid companyId, string cnpj, string chaveRaw, CancellationToken ct)
    {
        var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);
        if (Cnpj.Digits(cnpj) != company.Cnpj)
            return new { error = "CnpjMismatch", message = "CNPJ não é desta empresa." };
        if (!ChaveAcesso.TryNormalize(chaveRaw, out var chave))
            return new { error = "InvalidChave", message = "Chave de acesso inválida (44 dígitos + DV)." };

        var existing = await db.NfeDocuments.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.ChaveAcesso == chave, ct);
        if (existing is not null)
            return new { existing.Id, chave, existing.Status, replayed = true };

        var doc = new NfeDocument
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = NfeDocumentKinds.Inbound,
            ChaveAcesso = chave,
            Status = "Queued"
        };
        db.NfeDocuments.Add(doc);
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = WorkKinds.NfeIngest,
            PayloadJson = $"{{\"chave\":\"{chave}\",\"nfeDocumentId\":\"{doc.Id}\"}}"
        });
        await db.SaveChangesAsync(ct);
        return new { doc.Id, chave, status = doc.Status };
    }

    public async Task<object> IngestXmlAsync(Guid companyId, string xml, CancellationToken ct)
    {
        var parsed = Parse(xml);
        if (parsed is null) return new { error = "InvalidXml", message = "XML NF-e não reconhecido." };
        var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);
        if (parsed.EmitCnpj != company.Cnpj && parsed.DestCnpj != company.Cnpj)
            return new { error = "CnpjMismatch", message = "XML não pertence ao CNPJ da empresa." };

        var doc = await db.NfeDocuments.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.ChaveAcesso == parsed.Chave, ct);
        if (doc is null)
        {
            doc = new NfeDocument
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Kind = NfeDocumentKinds.Inbound,
                ChaveAcesso = parsed.Chave,
                Status = "Parsed"
            };
            db.NfeDocuments.Add(doc);
        }
        doc.Xml = xml;
        doc.Status = "Parsed";
        await db.SaveChangesAsync(ct);
        await ApplyItemsAsync(company, parsed, ct);
        doc.Status = "Applied";
        await db.SaveChangesAsync(ct);
        return new { doc.Id, chave = parsed.Chave, status = doc.Status, items = parsed.Items.Count };
    }

    public async Task ProcessQueuedAsync(WorkItem work, CancellationToken ct)
    {
        var doc = await db.NfeDocuments.FirstOrDefaultAsync(d => d.Id.ToString() == Extract(work.PayloadJson, "nfeDocumentId"), ct);
        if (doc is null) { work.Status = "Failed"; work.Error = "NfeNotFound"; return; }
        if (!string.IsNullOrEmpty(doc.Xml))
        {
            await IngestXmlAsync(work.CompanyId, doc.Xml, ct);
            work.Status = "Done";
            return;
        }
        var cert = await db.CompanyCertificates.AnyAsync(c => c.CompanyId == work.CompanyId && c.IsActive, ct);
        doc.Status = cert ? "WaitingDistDFe" : "CertificateNotConfigured";
        doc.Error = cert
            ? "DistDFe consChNFe queued; send XML if SEFAZ summary-only."
            : "A1 not configured; upload XML.";
        work.Status = "Done";
        work.Error = doc.Status;
        await db.SaveChangesAsync(ct);
    }

    async Task ApplyItemsAsync(Company company, ParsedNfe parsed, CancellationToken ct)
    {
        foreach (var item in parsed.Items)
        {
            var move = CfopPolicy.Classify(item.Cfop, parsed.EmitCnpj, parsed.DestCnpj, company.Cnpj);
            var sku = await MatchOrCreateProductAsync(company.Id, parsed.EmitCnpj, item, ct);
            if (move == CfopMovement.InboundPurchaseOrReturn)
                await inventory.ApplyInboundAsync(company.Id, sku, item.Qty, parsed.Chave, item.NItem, ct);
        }
    }

    async Task<string> MatchOrCreateProductAsync(Guid companyId, string emitCnpj, ParsedItem item, CancellationToken ct)
    {
        Product? p = null;
        if (!string.IsNullOrWhiteSpace(item.Ean) && item.Ean != "SEM GTIN")
            p = await db.Products.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Ean == item.Ean, ct);
        p ??= await db.Products.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Sku == item.CProd, ct);
        if (p is not null) return p.Sku;
        var sku = string.IsNullOrWhiteSpace(item.CProd) ? $"SKU-{item.NItem}" : item.CProd;
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Sku = sku,
            Name = item.XProd,
            Ean = item.Ean,
            Ncm = item.Ncm,
            Cfop = item.Cfop,
            SalePrice = item.VUn
        });
        await db.SaveChangesAsync(ct);
        return sku;
    }

    public static ParsedNfe? Parse(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var inf = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "infNFe");
            if (inf is null) return null;
            var id = inf.Attribute("Id")?.Value ?? "";
            var chave = ChaveAcesso.Digits(id);
            if (chave.Length != 44) return null;
            var emit = Cnpj.Digits(inf.Descendants().FirstOrDefault(x => x.Name.LocalName == "emit")?
                .Descendants().FirstOrDefault(x => x.Name.LocalName == "CNPJ")?.Value ?? "");
            var dest = Cnpj.Digits(inf.Descendants().FirstOrDefault(x => x.Name.LocalName == "dest")?
                .Descendants().FirstOrDefault(x => x.Name.LocalName is "CNPJ" or "CPF")?.Value ?? "");
            var items = new List<ParsedItem>();
            foreach (var det in inf.Descendants().Where(x => x.Name.LocalName == "det"))
            {
                var prod = det.Descendants().First(x => x.Name.LocalName == "prod");
                items.Add(new ParsedItem(
                    int.TryParse(det.Attribute("nItem")?.Value, out var n) ? n : items.Count + 1,
                    Val(prod, "cProd"),
                    Val(prod, "cEAN"),
                    Val(prod, "xProd"),
                    Val(prod, "NCM"),
                    Val(prod, "CFOP"),
                    decimal.TryParse(Val(prod, "qCom"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0,
                    decimal.TryParse(Val(prod, "vUnCom"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0
                ));
            }
            return new ParsedNfe(chave, emit, dest, items);
        }
        catch
        {
            return null;
        }
    }

    static string Val(XElement prod, string name) =>
        prod.Descendants().FirstOrDefault(x => x.Name.LocalName == name)?.Value ?? "";

    static string Extract(string json, string key)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
        }
        catch { return ""; }
    }
}

public sealed record ParsedNfe(string Chave, string EmitCnpj, string DestCnpj, List<ParsedItem> Items);
public sealed record ParsedItem(int NItem, string CProd, string Ean, string XProd, string Ncm, string Cfop, decimal Qty, decimal VUn);
