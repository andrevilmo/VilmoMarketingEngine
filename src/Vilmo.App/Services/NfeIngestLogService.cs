using System.Text.Json;
using Vilmo.Data;

namespace Vilmo.Services;

public static class NfeIngestLogSteps
{
    public const string Received = "received";
    public const string Validated = "validated";
    public const string CnpjMismatch = "cnpj_mismatch";
    public const string InvalidChave = "invalid_chave";
    public const string Queued = "queued";
    public const string Replayed = "replayed";
    public const string WorkerStarted = "worker_started";
    public const string XmlReceived = "xml_received";
    public const string InvalidXml = "invalid_xml";
    public const string ThirdPartyXml = "third_party_xml";
    public const string XmlParsed = "xml_parsed";
    public const string StockApplied = "stock_applied";
    public const string WaitingDistDFe = "waiting_distdfe";
    public const string CertificateMissing = "certificate_missing";
    public const string Failed = "failed";
}

public sealed class NfeIngestLogService(AppDbContext db)
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(
        Guid companyId,
        Guid runId,
        string stepCode,
        string level,
        string userMessage,
        object? technical,
        Guid? nfeDocumentId,
        string? chave,
        CancellationToken ct)
    {
        db.NfeIngestLogs.Add(new NfeIngestLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            RunId = runId,
            NfeDocumentId = nfeDocumentId,
            ChaveAcesso = string.IsNullOrWhiteSpace(chave) ? null : chave,
            StepCode = stepCode,
            Level = level,
            UserMessage = userMessage,
            TechnicalJson = JsonSerializer.Serialize(technical ?? new { }, JsonOpts),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public static string StatusPt(string? status) => status switch
    {
        "Queued" => "na fila",
        "WaitingDistDFe" => "aguardando o XML da Receita",
        "CertificateNotConfigured" => "falta o certificado A1",
        "Parsed" => "XML lido",
        "Applied" => "estoque atualizado",
        "Failed" => "falhou",
        _ => string.IsNullOrWhiteSpace(status) ? "desconhecida" : status
    };
}
