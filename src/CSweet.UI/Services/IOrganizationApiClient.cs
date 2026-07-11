using CSweet.Contracts.Planning;

namespace CSweet.UI.Services;

public interface IOrganizationApiClient
{
    Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<OrganizationResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<OrganizationResponse> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
