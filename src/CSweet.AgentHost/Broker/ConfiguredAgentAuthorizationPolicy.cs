using CSweet.Agent.Contracts.Grpc;
using Microsoft.Extensions.Options;

namespace CSweet.AgentHost.Broker;

public interface IAgentAuthorizationPolicy
{
    Task<AgentAuthorizationResult> AuthorizeAsync(
        RegisterAgent registration,
        CancellationToken cancellationToken);
}

public sealed record AgentAuthorizationResult(
    bool Accepted,
    AuthorizedAgentGrant? Grant,
    string RejectionReason);

public sealed class ConfiguredAgentAuthorizationPolicy
{
    private readonly AgentBrokerPolicyOptions _options;

    public ConfiguredAgentAuthorizationPolicy(IOptions<AgentBrokerPolicyOptions> options)
    {
        _options = options.Value;
    }

    public bool TryAuthorize(
        RegisterAgent registration,
        out AuthorizedAgentGrant? grant,
        out string rejectionReason)
    {
        grant = null;

        if (!_options.Agents.TryGetValue(registration.AgentId, out var configured))
        {
            rejectionReason = "The agent is not present in the broker grant configuration.";
            return false;
        }

        if (!configured.Enabled)
        {
            rejectionReason = "The agent is disabled by broker policy.";
            return false;
        }

        if (configured.AllowedBusinessIds.Count > 0 &&
            !configured.AllowedBusinessIds.Contains(
                registration.BusinessId,
                StringComparer.Ordinal))
        {
            rejectionReason = "The agent is not authorized for this business.";
            return false;
        }

        grant = new AuthorizedAgentGrant(
            Intersect(registration.DeclaredCapabilities, configured.Capabilities),
            Intersect(registration.RequestedSubscriptions, configured.Subscriptions),
            Intersect(registration.RequestedPublications, configured.Publications),
            Intersect(registration.RequestedPermissions, configured.Permissions));
        rejectionReason = string.Empty;
        return true;
    }

    public bool HasConfiguration(string agentId) => _options.Agents.ContainsKey(agentId);

    private static IReadOnlySet<string> Intersect(
        IEnumerable<string> requested,
        IEnumerable<string> configured)
    {
        var allowed = configured.ToHashSet(StringComparer.Ordinal);
        return requested
            .Where(allowed.Contains)
            .ToHashSet(StringComparer.Ordinal);
    }
}
