namespace VilmoMarketingEngine.Api.Models;

public record Campaign(Guid Id, string Name, string Channel, decimal Budget, DateTimeOffset CreatedAt);

public record CreateCampaignRequest(string Name, string Channel, decimal Budget);
