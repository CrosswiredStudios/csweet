using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IArtifactApprovalService
{
    Task<IReadOnlyList<ApprovalResponse>> ListByArtifactAsync(Guid artifactId, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> ApproveAsync(Guid artifactId, string? comment = null, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> RejectAsync(Guid artifactId, string? comment = null, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> RequestRevisionAsync(Guid artifactId, string? comment = null, CancellationToken cancellationToken = default);
}
