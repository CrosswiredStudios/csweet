using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IExecutiveBriefingService
{
    Task<ExecutiveBriefingSettingsResponse?> GetSettingsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<ExecutiveBriefingActionResponse> UpdateSettingsAsync(Guid organizationId, UpdateExecutiveBriefingSettingsRequest request, CancellationToken cancellationToken = default);
    Task<ExecutiveBriefingActionResponse> QueueManualAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<ExecutiveBriefingActionResponse> QueueActivationAsync(Guid organizationId, Guid chiefOrganizationUserId, CancellationToken cancellationToken = default);
    Task<ExecutiveBriefingActionResponse> QueueRuntimeStartupAsync(Guid installationId, Guid runtimeInstanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutiveBriefingHistoryItem>> ListHistoryAsync(Guid organizationId, int take = 20, CancellationToken cancellationToken = default);
}
