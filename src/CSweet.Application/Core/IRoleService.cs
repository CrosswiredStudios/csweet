using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<RoleResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task EnsureDefaultsAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
