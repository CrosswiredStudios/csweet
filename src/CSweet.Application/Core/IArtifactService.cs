using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IArtifactService
{
    Task<IReadOnlyList<ArtifactResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<ArtifactResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateArtifactRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> UpdateAsync(Guid id, UpdateArtifactRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
