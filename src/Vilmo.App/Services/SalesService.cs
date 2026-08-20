using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Security;

namespace Vilmo.Services;

public sealed class SalesService(AppDbContext db, InventoryService inventory)
{
    public IQueryable<Sale> Visible(CompanyContext ctx)
    {
        var q = db.Sales.Include(s => s.Items).Include(s => s.Attributes).AsQueryable();
        if (ctx.IsAdmin && ctx.CompanyId is { } adminCompany)
            q = q.Where(s => s.CompanyId == adminCompany);
        else if (ctx.IsVendor)
            q = q.Where(s => s.CompanyId == ctx.CompanyId && s.VendorUserId == ctx.UserId);
        else
            q = q.Where(s => s.CompanyId == ctx.CompanyId);
        return q;
    }

    public async Task<Sale?> GetAsync(CompanyContext ctx, Guid id, CancellationToken ct) =>
        await Visible(ctx).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<object> ListAsync(CompanyContext ctx, string? status, string? marketplace, CancellationToken ct)
    {
        var q = Visible(ctx).AsNoTracking();
        if (!string.IsNullOrEmpty(status)) q = q.Where(s => s.Status == status);
        if (!string.IsNullOrEmpty(marketplace)) q = q.Where(s => s.MarketplaceCode == marketplace);
        var rows = await q.OrderByDescending(s => s.Id).Take(200).ToListAsync(ct);
        return rows.Select(Map);
    }

    public object Map(Sale s) => new
    {
        s.Id,
        s.CompanyId,
        s.VendorUserId,
        s.MarketplaceCode,
        s.RemoteOrderId,
        s.Status,
        statusPt = SaleStatuses.Pt.GetValueOrDefault(s.Status, s.Status),
        s.Total,
        s.BuyerName,
        s.RecipientName,
        address = new { s.Street, s.Number, s.Complement, s.Neighborhood, s.City, s.Uf, s.Cep },
        s.Tracking,
        s.StockShort,
        items = s.Items.Select(i => new { i.Sku, i.Name, i.Quantity, i.UnitPrice, i.Ncm, i.Cfop }),
        attributes = s.Attributes.Select(a => new { a.FieldName, a.FieldValue }),
        s.CreatedAt,
        s.UpdatedAt
    };

    public async Task<Sale> UpsertImportedAsync(
        Guid companyId, Guid? vendorUserId, string marketplaceCode, string remoteOrderId,
        string status, decimal total, string buyer, Recipient dest, IReadOnlyList<SaleItem> items,
        IReadOnlyDictionary<string, string> attrs, CancellationToken ct)
    {
        var sale = await db.Sales.Include(s => s.Items).Include(s => s.Attributes)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.MarketplaceCode == marketplaceCode && s.RemoteOrderId == remoteOrderId, ct);
        var isNew = sale is null;
        sale ??= new Sale { Id = Guid.NewGuid(), CompanyId = companyId, MarketplaceCode = marketplaceCode, RemoteOrderId = remoteOrderId };
        sale.VendorUserId = vendorUserId;
        sale.Total = total;
        sale.BuyerName = buyer;
        sale.RecipientName = dest.Name;
        sale.Street = dest.Street;
        sale.Number = dest.Number;
        sale.Complement = dest.Complement;
        sale.Neighborhood = dest.Neighborhood;
        sale.City = dest.City;
        sale.Uf = dest.Uf;
        sale.Cep = Cep.Digits(dest.Cep);
        sale.UpdatedAt = DateTimeOffset.UtcNow;
        if (isNew)
        {
            sale.Status = SaleStatuses.PendingPayment;
            db.Sales.Add(sale);
            foreach (var i in items)
            {
                i.Id = Guid.NewGuid();
                i.SaleId = sale.Id;
                db.SaleItems.Add(i);
            }
        }
        foreach (var (k, v) in attrs)
        {
            var row = sale.Attributes.FirstOrDefault(a => a.FieldName == k);
            if (row is null) db.SaleMarketplaceAttributes.Add(new SaleMarketplaceAttribute { Id = Guid.NewGuid(), SaleId = sale.Id, FieldName = k, FieldValue = v });
            else row.FieldValue = v;
        }
        await db.SaveChangesAsync(ct);
        sale = await db.Sales.Include(s => s.Items).Include(s => s.Attributes).FirstAsync(s => s.Id == sale.Id, ct);
        if (status == SaleStatuses.Paid && sale.Status != SaleStatuses.Paid)
            await inventory.ApplySalePaidAsync(sale, ct);
        else if (status is SaleStatuses.Cancelled or SaleStatuses.Returned)
        {
            await inventory.ReverseSalePaidAsync(sale, ct);
            sale.Status = status;
            await db.SaveChangesAsync(ct);
        }
        else if (sale.Status == SaleStatuses.PendingPayment && status != SaleStatuses.Paid)
        {
            sale.Status = status;
            await db.SaveChangesAsync(ct);
        }
        return sale;
    }

    public async Task<object> EmitNfeAsync(CompanyContext ctx, Guid saleId, CancellationToken ct)
    {
        var sale = await GetAsync(ctx, saleId, ct) ?? throw new KeyNotFoundException("SaleNotFound");
        if (sale.Status is not (SaleStatuses.Paid or SaleStatuses.InvoiceRejected))
            return new { error = "InvalidStatus", message = "NF-e só pode ser emitida em Pago ou NF-e rejeitada." };
        var company = await db.Companies.FirstAsync(c => c.Id == sale.CompanyId, ct);
        if (!company.ReadyToInvoice && !await db.CompanyCertificates.AnyAsync(c => c.CompanyId == company.Id && c.IsActive, ct))
            return new { error = "NotReadyToInvoice", message = "Empresa sem A1 / IE." };
        if (!Cep.IsValid(sale.Cep))
            return new { error = "InvalidCep", message = "CEP do destinatário deve ter 8 dígitos." };

        sale.Status = SaleStatuses.Invoicing;
        var chave = BuildHomologChave(company, sale);
        var doc = await db.NfeDocuments.FirstOrDefaultAsync(d => d.CompanyId == sale.CompanyId && d.SaleId == sale.Id && d.Kind == NfeDocumentKinds.Outbound, ct);
        if (doc is null)
        {
            doc = new NfeDocument
            {
                Id = Guid.NewGuid(),
                CompanyId = sale.CompanyId,
                SaleId = sale.Id,
                Kind = NfeDocumentKinds.Outbound,
                ChaveAcesso = chave,
                Status = "Queued"
            };
            db.NfeDocuments.Add(doc);
        }
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = sale.CompanyId,
            Kind = WorkKinds.NfeEmit,
            PayloadJson = $"{{\"saleId\":\"{sale.Id}\",\"nfeDocumentId\":\"{doc.Id}\"}}"
        });
        await db.SaveChangesAsync(ct);
        return new { saleId = sale.Id, status = sale.Status, nfeId = doc.Id, chave };
    }

    public async Task CompleteEmitAsync(Guid saleId, CancellationToken ct)
    {
        var sale = await db.Sales.Include(s => s.Items).FirstAsync(s => s.Id == saleId, ct);
        var company = await db.Companies.FirstAsync(c => c.Id == sale.CompanyId, ct);
        var doc = await db.NfeDocuments.FirstAsync(d => d.SaleId == saleId && d.Kind == NfeDocumentKinds.Outbound, ct);
        var nnf = company.NextNnf++;
        var xml = $"<nfeProc><NFe><infNFe Id=\"NFe{doc.ChaveAcesso}\"><ide><nNF>{nnf}</nNF></ide></infNFe></NFe></nfeProc>";
        doc.Xml = xml;
        doc.Status = "Authorized";
        doc.Protocol = "100";
        sale.Status = SaleStatuses.PreparingForDispatch;
        sale.UpdatedAt = DateTimeOffset.UtcNow;
        db.WorkItems.Add(new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = sale.CompanyId,
            Kind = WorkKinds.UploadInvoice,
            PayloadJson = $"{{\"saleId\":\"{sale.Id}\"}}"
        });
        await db.SaveChangesAsync(ct);
    }

    static string BuildHomologChave(Company company, Sale sale)
    {
        var uf = company.Uf == "SC" ? "42" : "42";
        var aamm = DateTime.UtcNow.ToString("yyMM");
        var cnpj = company.Cnpj;
        var mod = "55";
        var serie = (company.NfeSerie ?? "1").PadLeft(3, '0');
        var nnf = company.NextNnf.ToString().PadLeft(9, '0');
        var tpEmis = "1";
        var cnf = Math.Abs(sale.Id.GetHashCode()).ToString().PadLeft(8, '0')[..8];
        var first43 = uf + aamm + cnpj + mod + serie + nnf + tpEmis + cnf;
        if (first43.Length > 43) first43 = first43[..43];
        if (first43.Length < 43) first43 = first43.PadRight(43, '0');
        return first43 + ChaveAcesso.Dv(first43);
    }
}

public sealed record Recipient(string Name, string Street, string Number, string? Complement, string Neighborhood, string City, string Uf, string Cep);
