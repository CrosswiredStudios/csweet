using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IWorkTaskService
{
    Task<IReadOnlyList<WorkTaskResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<WorkTaskResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateWorkTaskRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> UpdateAsync(Guid id, UpdateWorkTaskRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
