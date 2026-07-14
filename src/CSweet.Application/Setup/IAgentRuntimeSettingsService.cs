using CSweet.Contracts.Setup;

namespace CSweet.Application.Setup;

public interface IAgentRuntimeSettingsService
{
    Task<AgentRuntimeSettingsResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<AgentRuntimeSettingsActionResponse> UpdateAsync(
        UpdateAgentRuntimeSettingsRequest request,
        CancellationToken cancellationToken = default);
}
