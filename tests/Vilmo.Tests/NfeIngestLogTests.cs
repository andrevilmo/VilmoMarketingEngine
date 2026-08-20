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
}
