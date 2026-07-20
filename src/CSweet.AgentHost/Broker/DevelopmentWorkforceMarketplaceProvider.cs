using CSweet.Agent.SDK;

namespace CSweet.AgentHost.Broker;

/// <summary>Opt-in test fixture. Program registration makes this impossible to enable outside Development.</summary>
public sealed class DevelopmentWorkforceMarketplaceProvider : IWorkforceCatalogProvider
{
    public string ProviderKey => "development-marketplace-stub";
    public WorkforceCatalogKind CatalogKind => WorkforceCatalogKind.DigitalMarketplace;

    public Task<WorkforceSearchResponse> SearchAsync(WorkforceSearchRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = new WorkforceCandidate("stub-chief-ops", "DevelopmentMarketplaceStub", "RemoteAgent",
            "Development Operations Agent", request.RequiredCapabilities, request.RequiredCredentials ?? [],
            99m, request.Currency ?? "USD", 0.65m,
            "Synthetic candidate for development and automated approval-flow testing only.", true);
        return Task.FromResult(new WorkforceSearchResponse([candidate], [], true,
            "Development marketplace stub is enabled. These candidates are synthetic and cannot be used in production."));
    }
}
