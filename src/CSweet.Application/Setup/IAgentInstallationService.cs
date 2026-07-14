using CSweet.Contracts.Agents;

namespace CSweet.Application.Setup;

public interface IAgentInstallationService
{
    Task<AgentInstallationResponse> InstallAsync(
        Guid importId,
        InstallAgentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentInstallationResponse>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<AgentInstallationResponse?> GetAsync(
        Guid installationId,
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
}