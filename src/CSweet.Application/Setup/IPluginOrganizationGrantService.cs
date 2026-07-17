namespace CSweet.Application.Setup;

public interface IPluginOrganizationGrantService
{
    Task GrantAsync(Guid installationId, Guid organizationId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid installationId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListAsync(Guid installationId, CancellationToken cancellationToken = default);
}
