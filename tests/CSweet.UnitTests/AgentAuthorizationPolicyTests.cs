using CSweet.Agent.Contracts.Grpc;
using CSweet.AgentHost.Broker;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        registration.DeclaredCapabilities.AddRange(new[]
        {
            "allowed.capability.v1",
            "unapproved.capability.v1"
        });
        registration.RequestedSubscriptions.AddRange(new[]
        {
            "allowed.event.v1",
            "secret.event.v1"
        });
        registration.RequestedPublications.AddRange(new[]
        {
            "allowed.result.v1",
            "admin.changed.v1"
        });

        var accepted = policy.TryAuthorize(
            registration,
            out var grant,
            out var rejectionReason);

        Assert.True(accepted, rejectionReason);
        Assert.NotNull(grant);
        Assert.Single(grant.Capabilities);
        Assert.Contains("allowed.capability.v1", grant.Capabilities);
        Assert.Single(grant.Subscriptions);
        Assert.Contains("allowed.event.v1", grant.Subscriptions);
        Assert.Single(grant.Publications);
        Assert.Contains("allowed.result.v1", grant.Publications);
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

    [Fact]
    public async Task AuthorizeAsync_LoadsPersistedInstallationGrant()
    {
        await using var dbContext = CreateDbContext();
        var installation = SeedPersistedInstallation(dbContext);
        await dbContext.SaveChangesAsync();
        var configured = new ConfiguredAgentAuthorizationPolicy(
            Options.Create(new AgentBrokerPolicyOptions()));
        var policy = new PersistedAgentAuthorizationPolicy(configured, dbContext);
        var registration = new RegisterAgent
        {
            AgentId = "com.example.agent",
            AgentVersion = "1.0.0",
            InstallationId = installation.Id.ToString(),
            BusinessId = "business-1",
            RuntimeInstanceId = Guid.NewGuid().ToString(),
            TickId = Guid.NewGuid().ToString(),
            WorkloadToken = "bounded-token"
        };
        registration.DeclaredCapabilities.AddRange(["allowed.capability.v1", "unapproved.v1"]);
        registration.RequestedSubscriptions.Add("allowed.event.v1");
        registration.RequestedPublications.Add("allowed.result.v1");

        var result = await policy.AuthorizeAsync(registration, CancellationToken.None);

        Assert.True(result.Accepted, result.RejectionReason);
        Assert.NotNull(result.Grant);
        Assert.Equal(["allowed.capability.v1"], result.Grant.Capabilities);
        Assert.Equal(["allowed.event.v1"], result.Grant.Subscriptions);
        Assert.Equal(["allowed.result.v1"], result.Grant.Publications);
    }

    private static AgentInstallation SeedPersistedInstallation(CSweetDbContext dbContext)
    {
        var package = new AgentPackageVersion
        {
            Id = Guid.NewGuid(),
            PackageSourceId = Guid.NewGuid(),
            CommitSha = new string('a', 40),
            ManifestDigest = new string('b', 64),
            ManifestJson = "{}",
            AgentId = "com.example.agent",
            AgentName = "Example Agent",
            Version = "1.0.0",
            PublisherId = "com.example",
            PublisherName = "Example",
            RuntimeType = "dotnet-project",
            Status = AgentPackageVersionStatus.Approved,
            ImportedAt = DateTimeOffset.UtcNow
        };
        var installation = new AgentInstallation
        {
            Id = Guid.NewGuid(),
            PackageVersionId = package.Id,
            PackageVersion = package,
            BusinessId = "business-1",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        installation.Grant = new AgentInstallationGrant
        {
            Id = Guid.NewGuid(),
            AgentInstallationId = installation.Id,
            CapabilitiesJson = "[\"allowed.capability.v1\"]",
            SubscriptionsJson = "[\"allowed.event.v1\"]",
            PublicationsJson = "[\"allowed.result.v1\"]",
            ApprovedAt = DateTimeOffset.UtcNow
        };
        dbContext.AgentInstallations.Add(installation);
        return installation;
    }

    private static CSweetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CSweetDbContext(options);
    }
}
