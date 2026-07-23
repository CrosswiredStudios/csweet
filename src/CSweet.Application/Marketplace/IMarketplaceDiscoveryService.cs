using CSweet.Contracts.Marketplace;

namespace CSweet.Application.Marketplace;

public interface IMarketplaceDiscoveryService
{
    Task<MarketplaceDiscoveryResponse> SearchAsync(
        MarketplaceDiscoveryQuery query,
        CancellationToken cancellationToken = default);
}
