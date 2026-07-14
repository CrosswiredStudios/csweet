using CSweet.Contracts.Setup;

namespace CSweet.UI.Services;

public interface IAgentRuntimeSettingsApiClient
{
    Task<AgentRuntimeSettingsResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<AgentRuntimeSettingsActionResponse> UpdateAsync(
        UpdateAgentRuntimeSettingsRequest request,
        CancellationToken cancellationToken = default);
}
