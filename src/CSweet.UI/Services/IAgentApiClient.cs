using CSweet.Contracts.Agents;

namespace CSweet.UI.Services;

public interface IAgentApiClient
{
    Task<IReadOnlyList<AgentCatalogItemResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<AgentImportPreviewResponse> PreviewImportAsync(
        PreviewAgentImportRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentInstallationResponse> InstallAsync(
        Guid importId,
        InstallAgentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentInstallationResponse>> ListInstallationsAsync(
        CancellationToken cancellationToken = default);

    Task<AgentInstallationResponse> UpdateScheduleAsync(
        Guid installationId,
        UpdateAgentScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentInstallationResponse> RunNowAsync(
        Guid installationId,
        CancellationToken cancellationToken = default);

    Task<AgentInstallationResponse> DisableAsync(
        Guid installationId,
        CancellationToken cancellationToken = default);

    Task<AgentConfigurationSchemaResponse> GetConfigurationAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    Task<AgentConfigurationUpdateResponse> UpdateConfigurationAsync(
        string agentId,
        UpdateAgentConfigurationRequest request,
        CancellationToken cancellationToken = default);
}
