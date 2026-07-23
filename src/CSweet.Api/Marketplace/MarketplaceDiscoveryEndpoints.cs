using CSweet.Application.Marketplace;
using CSweet.Contracts.Marketplace;

namespace CSweet.Api.Marketplace;

public static class MarketplaceDiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapMarketplaceDiscoveryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/marketplace/agents", (
            string? q,
            string? category,
            string? capability,
            string? pricing,
            decimal? maxPrice,
            string? sort,
            int? take,
            IMarketplaceDiscoveryService marketplace,
            CancellationToken cancellationToken) =>
            marketplace.SearchAsync(new MarketplaceDiscoveryQuery(
                q, category, capability, pricing, maxPrice, sort, take ?? 24),
                cancellationToken));
        return endpoints;
    }
}
