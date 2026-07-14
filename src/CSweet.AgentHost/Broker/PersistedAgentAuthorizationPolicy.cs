using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.AgentHost.Broker;

public sealed class PersistedAgentAuthorizationPolicy : IAgentAuthorizationPolicy
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConfiguredAgentAuthorizationPolicy _configuredPolicy;
    private readonly CSweetDbContext _dbContext;

    public PersistedAgentAuthorizationPolicy(
        ConfiguredAgentAuthorizationPolicy configuredPolicy,
        CSweetDbContext dbContext)
    {
        _configuredPolicy = configuredPolicy;
        _dbContext = dbContext;
    }

    public async Task<AgentAuthorizationResult> AuthorizeAsync(
        RegisterAgent registration,
        CancellationToken cancellationToken)
    {
        if (_configuredPolicy.HasConfiguration(registration.AgentId))
        {
            var accepted = _configuredPolicy.TryAuthorize(
                registration,
                out var configuredGrant,
                out var configuredReason);
            return new AgentAuthorizationResult(accepted, configuredGrant, configuredReason);
        }

        if (!Guid.TryParse(registration.InstallationId, out var installationId))
        {
            return Reject("The installation ID is not a valid persisted installation identifier.");
        }

        var installation = await _dbContext.AgentInstallations
            .AsNoTracking()
            .Include(x => x.PackageVersion)
            .Include(x => x.Grant)
            .SingleOrDefaultAsync(x => x.Id == installationId, cancellationToken);
        if (installation?.PackageVersion is null || installation.Grant is null)
        {
            return Reject("The agent installation was not found.");
        }

        if (!installation.IsEnabled)
        {
            return Reject("The agent installation is disabled.");
        }

        if (installation.PackageVersion.Status is not (
                AgentPackageVersionStatus.Approved or AgentPackageVersionStatus.Built))
        {
            return Reject("The agent package version is not approved.");
        }

        if (!string.Equals(installation.PackageVersion.AgentId, registration.AgentId, StringComparison.Ordinal) ||
            !string.Equals(installation.PackageVersion.Version, registration.AgentVersion, StringComparison.Ordinal) ||
            !string.Equals(installation.BusinessId, registration.BusinessId, StringComparison.Ordinal))
        {
            return Reject("The registration identity does not match the persisted installation.");
        }

        try
        {
            var grant = new AuthorizedAgentGrant(
                Intersect(registration.DeclaredCapabilities, Deserialize(installation.Grant.CapabilitiesJson)),
                Intersect(registration.RequestedSubscriptions, Deserialize(installation.Grant.SubscriptionsJson)),
                Intersect(registration.RequestedPublications, Deserialize(installation.Grant.PublicationsJson)));
            return new AgentAuthorizationResult(true, grant, string.Empty);
        }
        catch (JsonException)
        {
            return Reject("The persisted installation grant is invalid.");
        }
    }

    private static AgentAuthorizationResult Reject(string reason) => new(false, null, reason);

    private static IReadOnlyList<string> Deserialize(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, SerializerOptions)
        ?? throw new JsonException("Grant was null.");

    private static IReadOnlySet<string> Intersect(
        IEnumerable<string> requested,
        IEnumerable<string> approved)
    {
        var approvedSet = approved.ToHashSet(StringComparer.Ordinal);
        return requested.Where(approvedSet.Contains).ToHashSet(StringComparer.Ordinal);
    }
}