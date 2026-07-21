using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.AgentHost.Broker;
using CSweet.AI.Providers;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class PlatformLlmCapabilityHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ConfiguredInstallationModel_IsPassedToProviderFactory()
    {
        await using var db = CreateDb();
        var providerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        db.LlmProviderProfiles.Add(Profile(providerId));
        db.AgentInstallationConfigurations.Add(Configuration(installationId, providerId, "configured-model"));
        await db.SaveChangesAsync();
        var factory = new RecordingProviderFactory();
        var handler = new PlatformLlmCapabilityHandler(
            db,
            factory,
            new AgentEmployeeIdentityResolver(db),
            NullLogger<PlatformLlmCapabilityHandler>.Instance);

        var results = new List<CapabilityResult>();
        await foreach (var result in handler.StreamAsync(
            Session(installationId),
            Request(providerId, "configured-model"),
            CancellationToken.None))
        {
            results.Add(result);
        }

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.True(result.Succeeded, result.Error));
        Assert.Equal("configured-model", factory.Model);
    }

    [Fact]
    public async Task ModelOutsideInstallationConfiguration_IsRejected()
    {
        await using var db = CreateDb();
        var providerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        db.LlmProviderProfiles.Add(Profile(providerId));
        db.AgentInstallationConfigurations.Add(Configuration(installationId, providerId, "configured-model"));
        await db.SaveChangesAsync();
        var factory = new RecordingProviderFactory();
        var handler = new PlatformLlmCapabilityHandler(
            db,
            factory,
            new AgentEmployeeIdentityResolver(db),
            NullLogger<PlatformLlmCapabilityHandler>.Instance);

        var results = new List<CapabilityResult>();
        await foreach (var result in handler.StreamAsync(
            Session(installationId),
            Request(providerId, "unapproved-model"),
            CancellationToken.None))
        {
            results.Add(result);
        }

        var failure = Assert.Single(results);
        Assert.False(failure.Succeeded);
        Assert.Contains("not approved", failure.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(factory.Model);
    }

    [Fact]
    public async Task InstallationWithoutTypedLlmGrant_IsRejected()
    {
        await using var db = CreateDb();
        var providerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        db.LlmProviderProfiles.Add(Profile(providerId));
        await db.SaveChangesAsync();
        var factory = new RecordingProviderFactory();
        var handler = new PlatformLlmCapabilityHandler(
            db,
            factory,
            new AgentEmployeeIdentityResolver(db),
            NullLogger<PlatformLlmCapabilityHandler>.Instance);

        var results = new List<CapabilityResult>();
        await foreach (var result in handler.StreamAsync(
            Session(installationId, grantLlm: false),
            Request(providerId, "configured-model"),
            CancellationToken.None))
        {
            results.Add(result);
        }

        var failure = Assert.Single(results);
        Assert.False(failure.Succeeded);
        Assert.Contains(BrokerLlmCapabilities.ChatStream, failure.Error, StringComparison.Ordinal);
        Assert.Null(factory.Model);
    }

    [Fact]
    public async Task InstructionsToolsAndFunctionCalls_AreTransportedAcrossBroker()
    {
        await using var db = CreateDb();
        var providerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        db.LlmProviderProfiles.Add(Profile(providerId));
        db.AgentInstallationConfigurations.Add(Configuration(installationId, providerId, "configured-model"));
        await db.SaveChangesAsync();
        using var schemaDocument = JsonDocument.Parse("""
            {"type":"object","properties":{"question":{"type":"string"}},"required":["question"]}
            """);
        var factory = new RecordingProviderFactory();
        var handler = new PlatformLlmCapabilityHandler(
            db,
            factory,
            new AgentEmployeeIdentityResolver(db),
            NullLogger<PlatformLlmCapabilityHandler>.Instance);
        var payload = new BrokerLlmRequest(
            providerId,
            "configured-model",
            [new BrokerLlmMessage("user", Contents: [new BrokerLlmContent("text", Text: "Help me staff the company")])],
            "Recommend roles only.",
            [new BrokerLlmTool("ask_user", "Ask the owner to choose.", schemaDocument.RootElement.Clone())]);
        var request = new RequestCapability
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Capability = BrokerLlmCapabilities.ChatStream,
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))
        };

        var results = new List<CapabilityResult>();
        await foreach (var result in handler.StreamAsync(Session(installationId), request, CancellationToken.None))
        {
            results.Add(result);
        }

        Assert.Equal("Recommend roles only.", factory.Client.Options?.Instructions);
        var forwardedTool = Assert.IsAssignableFrom<AIFunctionDeclaration>(Assert.Single(factory.Client.Options!.Tools!));
        Assert.Equal("ask_user", forwardedTool.Name);
        var chunks = results
            .Where(result => result.Succeeded && !result.Payload.IsEmpty)
            .Select(result => JsonSerializer.Deserialize<BrokerLlmChunk>(result.Payload.Span, JsonOptions)!)
            .ToList();
        var call = Assert.Single(
            chunks.SelectMany(chunk => chunk.Contents ?? []),
            content => content.Kind == "function_call");
        Assert.Equal("ask_user", call.Name);
        Assert.Equal("call-1", call.CallId);
    }

    [Fact]
    public async Task EmployeeIdentity_IsAuthoritativeAndRefreshesForEveryModelCall()
    {
        await using var db = CreateDb();
        var providerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var role = new CSweet.Domain.Core.Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Operations Lead",
            Description = "Own company operations.",
            ResponsibilitiesJson = "[\"Coordinate delivery\"]",
            AuthorityLevel = CSweet.Domain.Core.AuthorityLevel.ExecutionWithApproval
        };
        db.LlmProviderProfiles.Add(Profile(providerId));
        db.AgentInstallationConfigurations.Add(Configuration(installationId, providerId, "configured-model"));
        db.CoreOrganizationUsers.AddRange(
            new CSweet.Domain.Core.OrganizationUser
            {
                Id = managerId,
                OrganizationId = organizationId,
                DisplayName = "Morgan",
                EmployeeType = CSweet.Domain.Core.EmployeeType.Human,
                IsActive = true
            },
            new CSweet.Domain.Core.OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                AgentInstallationId = installationId,
                DisplayName = "Avery",
                EmployeeType = CSweet.Domain.Core.EmployeeType.Agent,
                RoleId = role.Id,
                Role = role,
                ReportsToOrganizationUserId = managerId,
                IsActive = true
            });
        await db.SaveChangesAsync();
        var factory = new RecordingProviderFactory();
        var handler = new PlatformLlmCapabilityHandler(
            db,
            factory,
            new AgentEmployeeIdentityResolver(db),
            NullLogger<PlatformLlmCapabilityHandler>.Instance);
        var session = Session(installationId, organizationId: organizationId);

        await DrainAsync(handler.StreamAsync(
            session,
            Request(providerId, "configured-model", "Keep replies concise."),
            CancellationToken.None));

        var firstInstructions = factory.Client.Options?.Instructions;
        Assert.Contains("Avery", firstInstructions, StringComparison.Ordinal);
        Assert.Contains("Operations Lead", firstInstructions, StringComparison.Ordinal);
        Assert.Contains("Coordinate delivery", firstInstructions, StringComparison.Ordinal);
        Assert.Contains(managerId.ToString("D"), firstInstructions, StringComparison.Ordinal);
        Assert.Contains(installationId.ToString("D"), firstInstructions, StringComparison.Ordinal);
        Assert.Contains("Keep replies concise.", firstInstructions, StringComparison.Ordinal);
        Assert.Contains("that record refers to you", firstInstructions, StringComparison.Ordinal);

        var employee = await db.CoreOrganizationUsers.SingleAsync(x => x.AgentInstallationId == installationId);
        employee.DisplayName = "Riley";
        role.Name = "Program Director";
        await db.SaveChangesAsync();

        await DrainAsync(handler.StreamAsync(
            session,
            Request(providerId, "configured-model", "Keep replies concise."),
            CancellationToken.None));

        var refreshedInstructions = factory.Client.Options?.Instructions;
        Assert.Contains("Riley", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("Program Director", refreshedInstructions, StringComparison.Ordinal);
        Assert.DoesNotContain("Avery", refreshedInstructions, StringComparison.Ordinal);
    }

    private static CSweetDbContext CreateDb() => new(
        new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static LlmProviderProfile Profile(Guid providerId) => new()
    {
        Id = providerId,
        Name = "Local provider",
        ProviderType = LlmProviderType.LmStudio,
        BaseUrl = "http://localhost:1234/v1",
        DefaultChatModel = string.Empty,
        IsEnabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static AgentInstallationConfiguration Configuration(
        Guid installationId,
        Guid providerId,
        string model) => new()
    {
        Id = Guid.NewGuid(),
        AgentInstallationId = installationId,
        SchemaVersion = "1",
        SettingsJson = JsonSerializer.Serialize(new
        {
            llmProviderId = providerId,
            llmModel = model
        }, JsonOptions),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static AgentSession Session(
        Guid installationId,
        bool grantLlm = true,
        Guid? organizationId = null) => new(
        Guid.NewGuid().ToString("N"),
        "test-agent",
        installationId.ToString("D"),
        (organizationId ?? Guid.NewGuid()).ToString("D"),
        Guid.NewGuid().ToString("D"),
        Guid.NewGuid().ToString("D"),
        new AuthorizedAgentGrant(
            new HashSet<string>(),
            new HashSet<string>(),
            new HashSet<string>(),
            new HashSet<string>(),
            grantLlm
                ? new HashSet<string>([BrokerLlmCapabilities.ChatStream], StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal)));

    private static RequestCapability Request(Guid providerId, string model, string? instructions = null)
    {
        var payload = new BrokerLlmRequest(
            providerId,
            model,
            [new BrokerLlmMessage("user", "Hello")],
            instructions);
        return new RequestCapability
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Capability = BrokerLlmCapabilities.ChatStream,
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))
        };
    }

    private static async Task DrainAsync(IAsyncEnumerable<CapabilityResult> results)
    {
        await foreach (var _ in results)
        {
        }
    }

    private sealed class RecordingProviderFactory : ILlmProviderFactory
    {
        public string? Model { get; private set; }
        public RecordingChatClient Client { get; } = new();

        public Task<IChatClient> CreateChatClientAsync(
            Guid providerProfileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IChatClient>(Client);

        public Task<IChatClient> CreateChatClientAsync(
            Guid providerProfileId,
            string? model,
            CancellationToken cancellationToken = default)
        {
            Model = model;
            return Task.FromResult<IChatClient>(Client);
        }
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "response")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Options = options;
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant,
            [
                new FunctionCallContent(
                    "call-1",
                    "ask_user",
                    new Dictionary<string, object?>
                    {
                        ["question"] = "Which role should we hire first?"
                    })
            ]);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
