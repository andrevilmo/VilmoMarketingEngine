using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

public sealed class NfeIngestService(AppDbContext db, InventoryService inventory, NfeIngestLogService logs)
{
    static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public async Task<object> EnqueueChaveAsync(Guid companyId, string cnpj, string chaveRaw, CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);
        await logs.WriteAsync(companyId, runId, NfeIngestLogSteps.Received, "info",
            "Recebemos o pedido de ingestão da nota.",
            new { source = "chave", cnpjSubmitted = Cnpj.Digits(cnpj), chaveRawLength = chaveRaw?.Length ?? 0 },
            null, ChaveAcesso.Digits(chaveRaw ?? ""), ct);

        if (Cnpj.Digits(cnpj) != company.Cnpj)
        {
            await logs.WriteAsync(companyId, runId, NfeIngestLogSteps.CnpjMismatch, "error",
                "O CNPJ informado não é desta empresa. Nada foi enviado à Receita.",
                new { error = "CnpjMismatch", companyCnpj = company.Cnpj, submittedCnpj = Cnpj.Digits(cnpj) },
                null, ChaveAcesso.Digits(chaveRaw ?? ""), ct);
            return new { error = "CnpjMismatch", message = "CNPJ não é desta empresa.", runId };
        }
        if (!ChaveAcesso.TryNormalize(chaveRaw ?? "", out var chave))
        {
            await logs.WriteAsync(companyId, runId, NfeIngestLogSteps.InvalidChave, "error",
                "A chave de acesso não é válida. Confira os 44 dígitos (incluindo o dígito verificador).",
                new { error = "InvalidChave", chaveDigits = ChaveAcesso.Digits(chaveRaw ?? "") },
                null, ChaveAcesso.Digits(chaveRaw ?? ""), ct);
            return new { error = "InvalidChave", message = "Chave de acesso inválida (44 dígitos + DV).", runId };
        }

        await logs.WriteAsync(companyId, runId, NfeIngestLogSteps.Validated, "info",
            "A chave de acesso está correta. Seguimos com a consulta.",
            new { chave, cnpj = company.Cnpj },
            null, chave, ct);

        var existing = await db.NfeDocuments.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.ChaveAcesso == chave, ct);
        if (existing is not null)
        {
            await logs.WriteAsync(companyId, runId, NfeIngestLogSteps.Replayed, "warning",
                $"Esta chave já foi enviada. Situação atual: {NfeIngestLogService.StatusPt(existing.Status)}. O estoque não será alterado de novo.",
                new
                {
                    error = (string?)null,
                    replayed = true,
                    nfeDocumentId = existing.Id,
                    status = existing.Status,
                    documentError = existing.Error
                },
                existing.Id, chave, ct);
            return new { existing.Id, chave, existing.Status, replayed = true, runId };
        }

        var doc = new NfeDocument
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = NfeDocumentKinds.Inbound,
            ChaveAcesso = chave,
            Status = "Queued"
        };
        var workId = Guid.NewGuid();
        db.NfeDocuments.Add(doc);
        db.WorkItems.Add(new WorkItem
        {
            Id = workId,
            CompanyId = companyId,
            Kind = WorkKinds.NfeIngest,
            PayloadJson = $"{{\"chave\":\"{chave}\",\"nfeDocumentId\":\"{doc.Id}\",\"runId\":\"{runId}\"}}"
        });
        await db.SaveChangesAsync(ct);
        await logs.WriteAsync(companyId, runId, NfeIngestLogSteps.Queued, "info",
            "A nota entrou na fila para consulta à Receita (SEFAZ / DistDFe).",
            new { nfeDocumentId = doc.Id, workItemId = workId, status = doc.Status, kind = WorkKinds.NfeIngest },
            doc.Id, chave, ct);
        return new { doc.Id, chave, status = doc.Status, runId };
    }

    public async Task<object> IngestXmlAsync(Guid companyId, string xml, CancellationToken ct, Guid? runId = null)
    {
        var id = runId ?? Guid.NewGuid();
        var parsed = Parse(xml);
        if (runId is null)
        {
            await logs.WriteAsync(companyId, id, NfeIngestLogSteps.XmlReceived, "info",
                "Recebemos o arquivo XML da nota.",
                new { source = "xml", xmlLength = xml?.Length ?? 0, chave = parsed?.Chave },
                null, parsed?.Chave, ct);
        }

        if (parsed is null)
        {
            await logs.WriteAsync(companyId, id, NfeIngestLogSteps.InvalidXml, "error",
                "Não reconhecemos este XML como uma NF-e. Envie o arquivo original da nota.",
                new { error = "InvalidXml", xmlLength = xml?.Length ?? 0 },
                null, null, ct);
            return new { error = "InvalidXml", message = "XML NF-e não reconhecido.", runId = id };
        }
        var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);
        if (parsed.EmitCnpj != company.Cnpj && parsed.DestCnpj != company.Cnpj)
        {
            await logs.WriteAsync(companyId, id, NfeIngestLogSteps.CnpjMismatch, "error",
                "Este XML não pertence ao CNPJ da empresa selecionada.",
                new { error = "CnpjMismatch", emitCnpj = parsed.EmitCnpj, destCnpj = parsed.DestCnpj, companyCnpj = company.Cnpj, chave = parsed.Chave },
                null, parsed.Chave, ct);
            return new { error = "CnpjMismatch", message = "XML não pertence ao CNPJ da empresa.", runId = id };
        }

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
        await logs.WriteAsync(companyId, id, NfeIngestLogSteps.XmlParsed, "info",
            $"Lemos a nota. Encontramos {parsed.Items.Count} item(ns).",
            new
            {
                nfeDocumentId = doc.Id,
                chave = parsed.Chave,
                emitCnpj = parsed.EmitCnpj,
                destCnpj = parsed.DestCnpj,
                itemCount = parsed.Items.Count,
                items = parsed.Items.Select(i => new { i.NItem, i.CProd, i.Cfop, i.Qty }).ToList()
            },
            doc.Id, parsed.Chave, ct);

        await ApplyItemsAsync(company, parsed, ct);
        doc.Status = "Applied";
        await db.SaveChangesAsync(ct);
        await logs.WriteAsync(companyId, id, NfeIngestLogSteps.StockApplied, "info",
            "Estoque atualizado com os itens de entrada desta nota.",
            new { nfeDocumentId = doc.Id, chave = parsed.Chave, status = doc.Status, itemCount = parsed.Items.Count },
            doc.Id, parsed.Chave, ct);
        return new { doc.Id, chave = parsed.Chave, status = doc.Status, items = parsed.Items.Count, runId = id };
    }

    public async Task ProcessQueuedAsync(WorkItem work, CancellationToken ct)
    {
        var runId = Guid.TryParse(Extract(work.PayloadJson, "runId"), out var parsedRun) ? parsedRun : work.Id;
        var chaveHint = Extract(work.PayloadJson, "chave");
        var docIdRaw = Extract(work.PayloadJson, "nfeDocumentId");
        Guid.TryParse(docIdRaw, out var docId);
        var doc = docId == Guid.Empty
            ? null
            : await db.NfeDocuments.FirstOrDefaultAsync(d => d.Id == docId, ct);
        if (doc is null)
        {
            work.Status = "Failed";
            work.Error = "NfeNotFound";
            await logs.WriteAsync(work.CompanyId, runId, NfeIngestLogSteps.Failed, "error",
                "Não encontramos o registro da nota na fila. Tente enviar de novo.",
                new { error = "NfeNotFound", workItemId = work.Id, payload = work.PayloadJson },
                null, chaveHint, ct);
            return;
        }

        await logs.WriteAsync(work.CompanyId, runId, NfeIngestLogSteps.WorkerStarted, "info",
            "O serviço da nota começou a processar este pedido.",
            new { workItemId = work.Id, nfeDocumentId = doc.Id, chave = doc.ChaveAcesso, hasXml = !string.IsNullOrEmpty(doc.Xml) },
            doc.Id, doc.ChaveAcesso, ct);

        if (!string.IsNullOrEmpty(doc.Xml))
        {
            await IngestXmlAsync(work.CompanyId, doc.Xml, ct, runId);
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
        if (cert)
        {
            await logs.WriteAsync(work.CompanyId, runId, NfeIngestLogSteps.WaitingDistDFe, "warning",
                "Aguardando o XML da Receita. Se a nota não aparecer no estoque, envie o arquivo XML.",
                new
                {
                    nfeDocumentId = doc.Id,
                    chave = doc.ChaveAcesso,
                    status = doc.Status,
                    sefaz = "DistDFe consChNFe not implemented; parking WaitingDistDFe",
                    workItemId = work.Id
                },
                doc.Id, doc.ChaveAcesso, ct);
        }
        else
        {
            await logs.WriteAsync(work.CompanyId, runId, NfeIngestLogSteps.CertificateMissing, "warning",
                "Falta o certificado digital A1 desta empresa. Envie o XML da nota para atualizar o estoque.",
                new { nfeDocumentId = doc.Id, chave = doc.ChaveAcesso, status = doc.Status, workItemId = work.Id },
                doc.Id, doc.ChaveAcesso, ct);
        }
    }

    public async Task LogWorkFailureAsync(WorkItem work, Exception ex, CancellationToken ct)
    {
        var runId = Guid.TryParse(Extract(work.PayloadJson, "runId"), out var parsedRun) ? parsedRun : work.Id;
        Guid? docId = Guid.TryParse(Extract(work.PayloadJson, "nfeDocumentId"), out var d) ? d : null;
        await logs.WriteAsync(work.CompanyId, runId, NfeIngestLogSteps.Failed, "error",
            "Não foi possível concluir a ingestão. Tente de novo ou envie o XML.",
            new { error = ex.GetType().Name, message = ex.Message, workItemId = work.Id },
            docId, Extract(work.PayloadJson, "chave"), ct);
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
