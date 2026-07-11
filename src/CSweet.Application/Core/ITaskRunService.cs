using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface ITaskRunService
{
    Task<IReadOnlyList<TaskRunResponse>> ListByTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TaskRunResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> CreateAsync(Guid taskId, CreateTaskRunRequest request, CancellationToken cancellationToken = default);
}
