using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Vilmo.Data;
using Vilmo.Domain;

namespace Vilmo.Services;

public sealed class LabelService(AppDbContext db)
{
    static LabelService() => QuestPDF.Settings.License = LicenseType.Community;

    public async Task<(byte[] Pdf, string Format)> CreateAsync(Guid companyId, Guid saleId, string format, CancellationToken ct)
    {
        var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId && s.CompanyId == companyId, ct)
                   ?? throw new KeyNotFoundException("SaleNotFound");
        if (sale.Status is not (SaleStatuses.PreparingForDispatch or SaleStatuses.LabelPrinted))
            throw new InvalidOperationException("LabelNotAllowed");
        var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);
        var nfe = await db.NfeDocuments.FirstOrDefaultAsync(d => d.SaleId == sale.Id && d.Kind == NfeDocumentKinds.Outbound, ct);

        format = format is "Mm138x106" ? "Mm138x106" : "Mm100x150";
        var existing = await db.ShipmentLabels.FirstOrDefaultAsync(l => l.SaleId == sale.Id && l.Format == format, ct);
        if (existing is not null) return (existing.Pdf, format);

        var pdf = BuildPdf(company, sale, nfe?.ChaveAcesso, format);
        db.ShipmentLabels.Add(new ShipmentLabel
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SaleId = sale.Id,
            Format = format,
            Pdf = pdf
        });
        if (sale.Status == SaleStatuses.PreparingForDispatch)
            sale.Status = SaleStatuses.LabelPrinted;
        await db.SaveChangesAsync(ct);
        return (pdf, format);
    }

    static byte[] BuildPdf(Company company, Sale sale, string? chave, string format)
    {
        var w = format == "Mm138x106" ? 138f : 100f;
        var h = format == "Mm138x106" ? 106f : 150f;
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(w, h, Unit.Millimetre);
                page.Margin(6, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.Content().Column(col =>
                {
                    col.Item().Text("ETIQUETA PARA ENVIO").Bold().FontSize(12);
                    col.Item().PaddingTop(4).Text("REMETENTE").Bold();
                    col.Item().Text(company.TradeName ?? company.LegalName);
                    col.Item().Text(company.LegalName).FontSize(8);
                    col.Item().Text($"CNPJ {Cnpj.Format(company.Cnpj)}");
                    col.Item().Text($"{company.Street}, {company.Number} {company.Complement}".Trim());
                    col.Item().Text($"{company.Neighborhood} — {company.City}/{company.Uf}");
                    col.Item().Text($"CEP {company.Cep}");
                    col.Item().PaddingTop(8).Text("DESTINATÁRIO").Bold();
                    col.Item().Text(sale.RecipientName);
                    col.Item().Text($"{sale.Street}, {sale.Number} {sale.Complement}".Trim());
                    col.Item().Text($"{sale.Neighborhood} — {sale.City}/{sale.Uf}");
                    col.Item().Text($"CEP {sale.Cep}");
                    if (!string.IsNullOrEmpty(sale.Tracking))
                        col.Item().PaddingTop(8).Text($"Rastreio {sale.Tracking}").Bold();
                    if (!string.IsNullOrEmpty(chave))
                        col.Item().PaddingTop(4).Text($"NF-e {chave}").FontSize(7);
                    col.Item().PaddingTop(10).Text("Colar no maior lado. Não cobrir código de barras. Não dobrar sobre arestas.").FontSize(7).Italic();
                });
            });
        }).GeneratePdf();
    }
}
