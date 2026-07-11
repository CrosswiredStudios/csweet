using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IWorkerService
{
    Task<IReadOnlyList<WorkerResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<WorkerResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateWorkerRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> UpdateAsync(Guid id, UpdateWorkerRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
