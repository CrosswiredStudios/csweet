using CSweet.Application.Setup;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Setup;

public sealed class PersistedPluginAuthorizationPolicy(CSweetDbContext db) : IPluginAuthorizationPolicy
{
    public async Task<bool> CanAccessOrganizationAsync(
        Guid installationId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var installation = await db.AgentInstallations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == installationId && x.IsEnabled, cancellationToken);
        if (installation is null) return false;
        if (installation.Scope == PluginInstallationScope.Organization)
            return string.Equals(installation.BusinessId, organizationId.ToString("D"), StringComparison.OrdinalIgnoreCase);
        return await db.PluginOrganizationGrants.AsNoTracking().AnyAsync(
            x => x.PluginInstallationId == installationId && x.OrganizationId == organizationId,
            cancellationToken);
    }
}
