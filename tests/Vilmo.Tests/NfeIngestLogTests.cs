using Microsoft.EntityFrameworkCore;
using Vilmo.Data;
using Vilmo.Domain;
using Vilmo.Services;

namespace Vilmo.Tests;

public class NfeIngestLogTests
{
    static AppDbContext Db()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"nfe-log-{Guid.NewGuid():N}.db")}")
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task ProcessQueued_without_xml_logs_waiting_distdfe_when_cert_present()
    {
        await using var db = Db();
        var companyId = Guid.NewGuid();
        db.Companies.Add(new Company
        {
            Id = companyId,
            LegalName = "T",
            Cnpj = "68431371000161",
            Street = "a",
            Number = "1",
            Neighborhood = "b",
            City = "c",
            Uf = "SC",
            Cep = "88010000"
        });
        db.CompanyCertificates.Add(new CompanyCertificate
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Cnpj = "68431371000161",
            ContainerPath = "/tmp/x.pfx",
            PasswordCipher = "cipher",
            IsActive = true
        });
        var first43 = "4226086843137100016155500100000000112345678";
        var chave = first43 + ChaveAcesso.Dv(first43);
        var doc = new NfeDocument
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ChaveAcesso = chave,
            Kind = NfeDocumentKinds.Inbound,
            Status = "Queued"
        };
        db.NfeDocuments.Add(doc);
        var runId = Guid.NewGuid();
        var work = new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = WorkKinds.NfeIngest,
            PayloadJson = $"{{\"chave\":\"{chave}\",\"nfeDocumentId\":\"{doc.Id}\",\"runId\":\"{runId}\"}}",
            Status = "Processing"
        };
        db.WorkItems.Add(work);
        await db.SaveChangesAsync();

        var nfe = new NfeIngestService(db, new InventoryService(db), new NfeIngestLogService(db));
        await nfe.ProcessQueuedAsync(work, default);

        var logs = (await db.NfeIngestLogs.ToListAsync())
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .ToList();
        Assert.Equal(NfeIngestLogSteps.WaitingDistDFe, logs[0].StepCode);
        Assert.Equal("warning", logs[0].Level);
        Assert.Contains("Receita", logs[0].UserMessage);
        Assert.Contains("DistDFe", logs[0].TechnicalJson);
        Assert.DoesNotContain("cipher", logs[0].TechnicalJson);
        Assert.Equal(runId, logs[0].RunId);
        Assert.Equal(NfeIngestLogSteps.WorkerStarted, logs[1].StepCode);
    }

    [Fact]
    public async Task ProcessQueued_with_third_party_xml_applies_stock()
    {
        await using var db = Db();
        var companyId = Guid.NewGuid();
        db.Companies.Add(new Company
        {
            Id = companyId,
            LegalName = "T",
            Cnpj = "68431371000161",
            Street = "a",
            Number = "1",
            Neighborhood = "b",
            City = "c",
            Uf = "SC",
            Cep = "88010000"
        });
        var first43 = "4226081122233300018155500100000000512345678";
        var chave = first43 + ChaveAcesso.Dv(first43);
        var xml = XmlParseTests.SampleXml(chave, "11222333000181", "00000000000191", "5102", "CAMISETA-ONLINE");
        var doc = new NfeDocument
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ChaveAcesso = chave,
            Kind = NfeDocumentKinds.Inbound,
            Status = "WaitingDistDFe",
            Xml = xml
        };
        db.NfeDocuments.Add(doc);
        var runId = Guid.NewGuid();
        var work = new WorkItem
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Kind = WorkKinds.NfeIngest,
            PayloadJson = $"{{\"chave\":\"{chave}\",\"nfeDocumentId\":\"{doc.Id}\",\"runId\":\"{runId}\"}}",
            Status = "Processing"
        };
        db.WorkItems.Add(work);
        await db.SaveChangesAsync();

        var nfe = new NfeIngestService(db, new InventoryService(db), new NfeIngestLogService(db));
        await nfe.ProcessQueuedAsync(work, default);

        var applied = await db.NfeDocuments.FirstAsync(d => d.Id == doc.Id);
        Assert.Equal("Applied", applied.Status);
        Assert.Equal(xml, applied.Xml);
        var product = await db.Products.SingleAsync(p => p.CompanyId == companyId && p.Sku == "CAMISETA-ONLINE");
        Assert.Equal("Camiseta XML", product.Name);
        var bal = await db.InventoryBalances.SingleAsync(b => b.CompanyId == companyId && b.Sku == "CAMISETA-ONLINE");
        Assert.Equal(2m, bal.OnHand);
        var move = await db.InventoryMovements.SingleAsync(m =>
            m.CompanyId == companyId && m.ChaveAcesso == chave && m.NItem == 1);
        Assert.Equal("CAMISETA-ONLINE", move.Sku);
        Assert.Equal(InventoryMovementKinds.NfeInbound, move.Kind);
        var logs = await db.NfeIngestLogs.Where(l => l.RunId == runId).ToListAsync();
        Assert.Contains(logs, l => l.StepCode == NfeIngestLogSteps.ThirdPartyXml);
        Assert.Contains(logs, l => l.StepCode == NfeIngestLogSteps.StockApplied);
        Assert.DoesNotContain(logs, l => l.StepCode == NfeIngestLogSteps.CnpjMismatch);
        Assert.Equal("Done", work.Status);
    }
}
