using Vilmo.Domain;
using Vilmo.Hosting;
using Vilmo.Workers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddVilmo(builder.Configuration);
builder.Services.AddHostedService(sp => new PollingWorker(
    sp.GetRequiredService<WorkProcessor>(),
    sp.GetRequiredService<ILogger<PollingWorker>>())
{
    Kinds = [WorkKinds.NfeIngest, WorkKinds.NfeEmit]
});
var app = builder.Build();
app.MapGet("/health", () => Results.Text("vilmo-nfe\n", "text/plain"));
app.Run();
