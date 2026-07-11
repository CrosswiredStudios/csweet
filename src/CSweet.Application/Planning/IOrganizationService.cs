using CSweet.Contracts.Planning;

namespace CSweet.Application.Planning;

public interface IOrganizationService
{
    Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
