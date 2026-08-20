using Vilmo.Domain;
using Vilmo.Hosting;
using Vilmo.Workers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddVilmo(builder.Configuration);
builder.Services.AddHostedService(sp => new PollingWorker(
    sp.GetRequiredService<WorkProcessor>(),
    sp.GetRequiredService<ILogger<PollingWorker>>())
{
    Kinds = [WorkKinds.SaleImport, WorkKinds.PublishListing, WorkKinds.StockPublish, WorkKinds.UploadInvoice]
});
var app = builder.Build();
app.MapGet("/health", () => Results.Text("vilmo-worker\n", "text/plain"));
app.Run();
