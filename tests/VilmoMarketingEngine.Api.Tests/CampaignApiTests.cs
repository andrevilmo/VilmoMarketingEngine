using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using VilmoMarketingEngine.Api.Models;

namespace VilmoMarketingEngine.Api.Tests;

public class CampaignApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CampaignApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VilmoMarketingEngine", body);
    }

    [Fact]
    public async Task CreateThenListCampaign_RoundTrips()
    {
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync(
            "/campaigns",
            new CreateCampaignRequest("Summer Promo", "social", 5000m));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<Campaign>();
        Assert.NotNull(created);
        Assert.Equal("Summer Promo", created!.Name);

        var list = await client.GetFromJsonAsync<List<Campaign>>("/campaigns");
        Assert.NotNull(list);
        Assert.Contains(list!, c => c.Id == created.Id);
    }

    [Fact]
    public async Task CreateCampaign_WithInvalidBudget_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/campaigns",
            new CreateCampaignRequest("Bad Budget", "email", -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
