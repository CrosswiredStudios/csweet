using CSweet.Contracts.Agents;

namespace CSweet.UI.Services;

public interface IAgentApiClient
{
    Task<IReadOnlyList<AgentCatalogItemResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<AgentImportPreviewResponse> PreviewImportAsync(
        PreviewAgentImportRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentConfigurationSchemaResponse> GetConfigurationAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    Task<AgentConfigurationUpdateResponse> UpdateConfigurationAsync(
        string agentId,
        UpdateAgentConfigurationRequest request,
        CancellationToken cancellationToken = default);
}
