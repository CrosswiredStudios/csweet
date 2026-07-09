using CSweet.Contracts.Llm;

namespace CSweet.AI.Providers;

public interface IModelCatalogClient
{
    Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);
}
