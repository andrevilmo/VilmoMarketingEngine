using VilmoMarketingEngine.Api.Models;
using VilmoMarketingEngine.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<CampaignStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "VilmoMarketingEngine" }))
    .WithName("Health");

var campaigns = app.MapGroup("/campaigns");

campaigns.MapGet("/", (CampaignStore store) => Results.Ok(store.GetAll()))
    .WithName("ListCampaigns");

campaigns.MapGet("/{id:guid}", (Guid id, CampaignStore store) =>
    store.Get(id) is { } campaign ? Results.Ok(campaign) : Results.NotFound())
    .WithName("GetCampaign");

campaigns.MapPost("/", (CreateCampaignRequest request, CampaignStore store) =>
{
    try
    {
        var campaign = store.Add(request);
        return Results.Created($"/campaigns/{campaign.Id}", campaign);
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [ex.Message]
        });
    }
})
.WithName("CreateCampaign");

app.Run();

// Exposed so integration tests can bootstrap the app via WebApplicationFactory.
public partial class Program;
