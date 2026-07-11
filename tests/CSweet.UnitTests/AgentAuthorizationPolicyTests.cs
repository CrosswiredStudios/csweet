using CSweet.Agent.Contracts.Grpc;
using CSweet.AgentHost.Broker;
using Microsoft.Extensions.Options;

namespace CSweet.UnitTests;

public sealed class AgentAuthorizationPolicyTests
{
    [Fact]
    public void TryAuthorize_GrantsOnlyConfiguredRequestedValues()
    {
        var options = new AgentBrokerPolicyOptions
        {
            Agents = new Dictionary<string, AgentGrantOptions>(StringComparer.Ordinal)
            {
                ["com.example.agent"] = new()
                {
                    Enabled = true,
                    AllowedBusinessIds = ["business-1"],
                    Capabilities = ["allowed.capability.v1"],
                    Subscriptions = ["allowed.event.v1"],
                    Publications = ["allowed.result.v1"]
                }
            }
        };
        var policy = new ConfiguredAgentAuthorizationPolicy(Options.Create(options));
        var registration = new RegisterAgent
        {
            AgentId = "com.example.agent",
            AgentVersion = "1.0.0",
            InstallationId = "install-1",
            BusinessId = "business-1"
        };
        registration.DeclaredCapabilities.AddRange([
            "allowed.capability.v1",
            "unapproved.capability.v1"
        ]);
        registration.RequestedSubscriptions.AddRange([
            "allowed.event.v1",
            "secret.event.v1"
        ]);
        registration.RequestedPublications.AddRange([
            "allowed.result.v1",
            "admin.changed.v1"
        ]);

        var accepted = policy.TryAuthorize(
            registration,
            out var grant,
            out var rejectionReason);

        Assert.True(accepted, rejectionReason);
        Assert.NotNull(grant);
        Assert.Equal(["allowed.capability.v1"], grant.Capabilities);
        Assert.Equal(["allowed.event.v1"], grant.Subscriptions);
        Assert.Equal(["allowed.result.v1"], grant.Publications);
    }

    [Fact]
    public void TryAuthorize_RejectsBusinessOutsideGrant()
    {
        var options = new AgentBrokerPolicyOptions
        {
            Agents = new Dictionary<string, AgentGrantOptions>(StringComparer.Ordinal)
            {
                ["com.example.agent"] = new()
                {
                    Enabled = true,
                    AllowedBusinessIds = ["business-1"]
                }
            }
        };
        var policy = new ConfiguredAgentAuthorizationPolicy(Options.Create(options));

        var accepted = policy.TryAuthorize(
            new RegisterAgent
            {
                AgentId = "com.example.agent",
                AgentVersion = "1.0.0",
                InstallationId = "install-1",
                BusinessId = "business-2"
            },
            out var grant,
            out var rejectionReason);

        Assert.False(accepted);
        Assert.Null(grant);
        Assert.Contains("not authorized", rejectionReason, StringComparison.OrdinalIgnoreCase);
    }
}
