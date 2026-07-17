using CSweet.Application.Communications;
using CSweet.Application.Setup;
using CSweet.Communications.Abstractions;
using CSweet.Contracts.Communications;
using CSweet.Domain.Communications;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Communications;

public sealed class CommunicationIngressHandler(
    CSweetDbContext db,
    IPluginAuthorizationPolicy authorization,
    ICommunicationRouter router) : ICommunicationIngressHandler
{
    public async Task<CommunicationActionResponse> IngestAsync(
        Guid pluginInstallationId,
        Guid organizationId,
        NormalizedCommunicationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.CommunicationIngressReceipts.AsNoTracking().SingleOrDefaultAsync(
            x => x.PluginInstallationId == pluginInstallationId && x.IdempotencyKey == envelope.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
            return new(existing.Succeeded, existing.ErrorCode, existing.ResultMessage);

        CommunicationActionResponse result;
        if (!await authorization.CanAccessOrganizationAsync(pluginInstallationId, organizationId, cancellationToken))
            result = new(false, "organization_denied", "The plugin is not authorized for this organization.");
        else
        {
            var connection = await db.CommunicationConnections.AsNoTracking().AnyAsync(x =>
                x.PluginInstallationId == pluginInstallationId && x.OrganizationId == organizationId &&
                x.ProviderKey == envelope.Provider.ToLowerInvariant() && x.WorkspaceExternalId == envelope.WorkspaceExternalId,
                cancellationToken);
            result = connection
                ? await router.RouteInboundAsync(envelope, cancellationToken)
                : new(false, "workspace_not_connected", "The provider workspace is not mapped to this organization.");
        }

        db.CommunicationIngressReceipts.Add(new CommunicationIngressReceipt
        {
            Id = Guid.NewGuid(), PluginInstallationId = pluginInstallationId, OrganizationId = organizationId,
            ProviderKey = envelope.Provider.ToLowerInvariant(), IdempotencyKey = envelope.IdempotencyKey,
            Succeeded = result.Succeeded, ErrorCode = result.ErrorCode,
            ResultMessage = result.Message, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
