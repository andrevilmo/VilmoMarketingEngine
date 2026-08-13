using System.Collections.Concurrent;
using VilmoMarketingEngine.Api.Models;

namespace VilmoMarketingEngine.Api.Services;

/// <summary>
/// Simple thread-safe in-memory store for campaigns. This is a starter
/// implementation; swap for a real persistence layer as the engine grows.
/// </summary>
public class CampaignStore
{
    private readonly ConcurrentDictionary<Guid, Campaign> _campaigns = new();

    public IReadOnlyCollection<Campaign> GetAll() =>
        _campaigns.Values.OrderBy(c => c.CreatedAt).ToArray();

    public Campaign? Get(Guid id) => _campaigns.TryGetValue(id, out var campaign) ? campaign : null;

    public Campaign Add(CreateCampaignRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Campaign name is required.", nameof(request));
        }

        if (request.Budget < 0)
        {
            throw new ArgumentException("Campaign budget cannot be negative.", nameof(request));
        }

        var campaign = new Campaign(
            Guid.NewGuid(),
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Channel) ? "unspecified" : request.Channel.Trim(),
            request.Budget,
            DateTimeOffset.UtcNow);

        _campaigns[campaign.Id] = campaign;
        return campaign;
    }
}
