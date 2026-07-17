using CSweet.Application.Setup;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Setup;

public sealed class PluginOrganizationGrantService(CSweetDbContext db) : IPluginOrganizationGrantService
{
    public async Task GrantAsync(Guid installationId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var valid = await db.AgentInstallations.AnyAsync(x => x.Id == installationId && x.IsEnabled &&
            x.Scope == PluginInstallationScope.System && x.PackageVersion!.PluginKind == PluginKind.Service,
            cancellationToken);
        if (!valid) throw new InvalidOperationException("An enabled system communication plugin is required.");
        if (!await db.CoreOrganizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
            throw new InvalidOperationException("The organization was not found.");
        if (await db.PluginOrganizationGrants.AnyAsync(x => x.PluginInstallationId == installationId &&
            x.OrganizationId == organizationId, cancellationToken)) return;
        db.PluginOrganizationGrants.Add(new PluginOrganizationGrant
        {
            Id = Guid.NewGuid(), PluginInstallationId = installationId,
            OrganizationId = organizationId, GrantedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(Guid installationId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var grant = await db.PluginOrganizationGrants.SingleOrDefaultAsync(x => x.PluginInstallationId == installationId &&
            x.OrganizationId == organizationId, cancellationToken);
        if (grant is null) return;
        db.PluginOrganizationGrants.Remove(grant);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListAsync(Guid installationId, CancellationToken cancellationToken = default) =>
        await db.PluginOrganizationGrants.AsNoTracking().Where(x => x.PluginInstallationId == installationId)
            .OrderBy(x => x.OrganizationId).Select(x => x.OrganizationId).ToListAsync(cancellationToken);
}
