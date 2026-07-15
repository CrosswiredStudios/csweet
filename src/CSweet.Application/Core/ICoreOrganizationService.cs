using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface ICoreOrganizationService
{
    Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default, Guid? applicationUserId = null);
    Task<CoreActionResponse> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
