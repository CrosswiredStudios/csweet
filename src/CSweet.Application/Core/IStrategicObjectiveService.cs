using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IStrategicObjectiveService
{
    Task<IReadOnlyList<StrategicObjectiveResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<StrategicObjectiveResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateStrategicObjectiveRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> UpdateAsync(Guid id, UpdateStrategicObjectiveRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
