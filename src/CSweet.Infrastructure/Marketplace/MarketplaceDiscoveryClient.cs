using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Json;
using CSweet.Agent.SDK;
using CSweet.Application.Marketplace;
using CSweet.Contracts.Marketplace;
using Microsoft.Extensions.Options;

namespace CSweet.Infrastructure.Marketplace;

public sealed class MarketplaceOptions
{
    public const string SectionName = "CSweet:Marketplace";

    public bool Enabled { get; set; }

    [Required, Url]
    public string BaseUrl { get; set; } = "https://marketplace.csweet.com/";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class MarketplaceDiscoveryClient(
    HttpClient http,
    IOptions<MarketplaceOptions> options)
    : IMarketplaceDiscoveryService, IWorkforceCatalogProvider
{
    public string ProviderKey => "csweet-marketplace";
    public WorkforceCatalogKind CatalogKind => WorkforceCatalogKind.DigitalMarketplace;

    public async Task<MarketplaceDiscoveryResponse> SearchAsync(
        MarketplaceDiscoveryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return Offline("The online C-Sweet Marketplace is disabled for this installation.");

        try
        {
            using var response = await http.GetAsync(BuildPath(query), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Offline($"Marketplace discovery returned HTTP {(int)response.StatusCode}.");
            var remote = await response.Content.ReadFromJsonAsync<RemoteDiscoveryResponse>(
                cancellationToken: cancellationToken);
            if (remote is null)
                return Offline("Marketplace discovery returned an empty response.");
            return new MarketplaceDiscoveryResponse(
                remote.Items.Select(Map).ToArray(),
                remote.Total,
                remote.Categories,
                remote.PricingModels,
                true,
                null);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            return Offline("The online C-Sweet Marketplace is currently unavailable.");
        }
    }

    public async Task<WorkforceSearchResponse> SearchAsync(
        WorkforceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var discovery = await SearchAsync(new MarketplaceDiscoveryQuery(
            Search: null,
            Capability: request.RequiredCapabilities.FirstOrDefault(),
            MaximumPrice: request.MaximumBudget,
            Sort: "rating",
            Take: Math.Clamp(request.MaximumResults * 3, 1, 100)), cancellationToken);
        if (!discovery.IsOnline)
            return new WorkforceSearchResponse([], [], false, discovery.UnavailableReason);

        var accepted = new List<WorkforceCandidate>();
        var rejected = new List<RejectedWorkforceCandidate>();
        foreach (var agent in discovery.Items)
        {
            var missing = request.RequiredCapabilities
                .Except(agent.Capabilities, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var reasons = new List<string>();
            reasons.AddRange(missing.Select(x => $"Missing capability {x}."));
            if (request.RequiredCredentials is { Count: > 0 })
                reasons.Add("The marketplace listing does not provide verified credential evidence.");
            if (!string.IsNullOrWhiteSpace(request.Currency) &&
                !string.Equals(request.Currency, agent.Currency, StringComparison.OrdinalIgnoreCase))
                reasons.Add($"Price is denominated in {agent.Currency}, not {request.Currency}.");
            var price = agent.PriceInCents / 100m;
            if (request.MaximumBudget is { } maximum && price is { } amount && amount > maximum)
                reasons.Add("Listed price exceeds the requested budget.");
            if (reasons.Count > 0)
            {
                rejected.Add(new RejectedWorkforceCandidate(
                    agent.Id.ToString("D"), agent.Name, "CSweetMarketplace", reasons));
                continue;
            }

            var ratingScore = agent.Rating is { } rating
                ? Math.Clamp(rating / 10m, 0m, 1m)
                : 0.5m;
            var score = Math.Min(0.99m,
                0.55m + ratingScore * 0.35m + (agent.IsFeatured ? 0.05m : 0m));
            accepted.Add(new WorkforceCandidate(
                agent.Id.ToString("D"),
                "CSweetMarketplace",
                "Agent",
                agent.Name,
                agent.Capabilities,
                [],
                price,
                agent.Currency,
                score,
                agent.Rating is { } scoreRating
                    ? $"Marketplace listing matched the requested capabilities. Current six-month rating: {scoreRating:0.0}/10 from {agent.RatingCount} review(s). Review and acquire it at {agent.ListingUrl}"
                    : $"Marketplace listing matched the requested capabilities. Review and acquire it at {agent.ListingUrl}",
                true));
        }

        return new WorkforceSearchResponse(
            accepted.OrderByDescending(x => x.Score)
                .Take(Math.Clamp(request.MaximumResults, 1, 25)).ToArray(),
            rejected,
            true,
            null);
    }

    private string BuildPath(MarketplaceDiscoveryQuery query)
    {
        var parameters = new List<string>();
        Add("q", query.Search);
        Add("category", query.Category);
        Add("capability", query.Capability);
        Add("pricing", query.PricingModel);
        Add("maxPrice", query.MaximumPrice?.ToString(CultureInfo.InvariantCulture));
        Add("sort", query.Sort);
        Add("take", Math.Clamp(query.Take, 1, 100).ToString(CultureInfo.InvariantCulture));
        return "api/v1/discovery/agents" +
            (parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}");

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parameters.Add($"{key}={Uri.EscapeDataString(value)}");
        }
    }

    private MarketplaceAgentResponse Map(RemoteAgent item) =>
        new(item.Id, item.PublisherSlug, item.ListingSlug, item.Name, item.PublisherName,
            item.Summary, item.Category, item.Capabilities, item.PricingModel,
            item.PriceInCents, item.BillingUnitQuantity, item.Currency, item.Rating,
            item.RatingCount, item.IsFeatured, item.RepositoryUrl, item.DocumentationUrl,
            new Uri(http.BaseAddress!, item.ListingPath).ToString());

    private static MarketplaceDiscoveryResponse Offline(string reason) =>
        new([], 0, [], [], false, reason);

    private sealed record RemoteDiscoveryResponse(
        IReadOnlyList<RemoteAgent> Items,
        int Total,
        IReadOnlyList<string> Categories,
        IReadOnlyList<string> PricingModels);

    private sealed record RemoteAgent(
        Guid Id,
        string PublisherSlug,
        string ListingSlug,
        string Name,
        string PublisherName,
        string Summary,
        string Category,
        IReadOnlyList<string> Capabilities,
        string PricingModel,
        int? PriceInCents,
        int BillingUnitQuantity,
        string Currency,
        decimal? Rating,
        int RatingCount,
        bool IsFeatured,
        string RepositoryUrl,
        string DocumentationUrl,
        string ListingPath);
}
