using VilmoMarketingEngine.Api.Models;
using VilmoMarketingEngine.Api.Services;

namespace VilmoMarketingEngine.Api.Tests;

public class CampaignStoreTests
{
    [Fact]
    public void Add_StoresCampaignAndAssignsId()
    {
        var store = new CampaignStore();

        var campaign = store.Add(new CreateCampaignRequest("Spring Launch", "email", 2500m));

        Assert.NotEqual(Guid.Empty, campaign.Id);
        Assert.Equal("Spring Launch", campaign.Name);
        Assert.Equal("email", campaign.Channel);
        Assert.Single(store.GetAll());
        Assert.Equal(campaign, store.Get(campaign.Id));
    }

    [Fact]
    public void Add_DefaultsChannelWhenMissing()
    {
        var store = new CampaignStore();

        var campaign = store.Add(new CreateCampaignRequest("No Channel", "  ", 10m));

        Assert.Equal("unspecified", campaign.Channel);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("Valid", -5)]
    public void Add_RejectsInvalidInput(string name, decimal budget)
    {
        var store = new CampaignStore();

        Assert.Throws<ArgumentException>(() => store.Add(new CreateCampaignRequest(name, "email", budget)));
    }
}
