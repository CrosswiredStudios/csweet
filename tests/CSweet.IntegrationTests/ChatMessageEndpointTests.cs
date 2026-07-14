using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Api.Chat;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CSweet.IntegrationTests;

public sealed class ChatMessageEndpointTests
{
    [Fact]
    public async Task StreamMessage_ReturnsSseAndPersistsBothTurns()
    {
        await using var factory = CreateFactory();
        var seeded = await SeedConversationAsync(factory, includeProvider: true);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{seeded.ConversationId}/messages/stream",
            new SendChatMessageRequest("Say hello."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"sequence\":0", body);
        Assert.Contains("\"delta\":\"Hello \"", body);
        Assert.Contains("\"sequence\":1", body);
        Assert.Contains("\"delta\":\"there\"", body);
        Assert.Contains("\"isFinal\":true", body);

        var messages = await client.GetFromJsonAsync<IReadOnlyList<ConversationMessageResponse>>(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{seeded.ConversationId}/messages");

        Assert.NotNull(messages);
        Assert.Equal(2, messages.Count);
        Assert.Equal((int)ConversationRole.User, messages[0].Role);
        Assert.Equal("Say hello.", messages[0].Content);
        Assert.Equal((int)ConversationRole.Assistant, messages[1].Role);
        Assert.Equal("Hello there", messages[1].Content);
    }

    [Fact]
    public async Task StreamMessage_EmptyMessageReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        var seeded = await SeedConversationAsync(factory, includeProvider: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{seeded.ConversationId}/messages/stream",
            new SendChatMessageRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StreamMessage_UnknownConversationReturnsNotFound()
    {
        await using var factory = CreateFactory();
        var seeded = await SeedConversationAsync(factory, includeProvider: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{Guid.NewGuid()}/messages/stream",
            new SendChatMessageRequest("Hello."));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StreamMessage_NoEnabledProviderReturnsConflict()
    {
        await using var factory = CreateFactory();
        var seeded = await SeedConversationAsync(factory, includeProvider: false);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{seeded.ConversationId}/messages/stream",
            new SendChatMessageRequest("Hello."));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No enabled LLM provider", body);
    }

    [Fact]
    public async Task StreamMessage_RuntimeStillStartingReturnsConflictWithoutPersistingMessage()
    {
        await using var factory = CreateFactory(runtimeReady: false);
        var seeded = await SeedConversationAsync(factory, includeProvider: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{seeded.ConversationId}/messages/stream",
            new SendChatMessageRequest("Hello."));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("still starting", await response.Content.ReadAsStringAsync());
        var messages = await client.GetFromJsonAsync<IReadOnlyList<ConversationMessageResponse>>(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{seeded.ConversationId}/messages");
        Assert.Empty(messages!);
    }

    [Fact]
    public async Task StreamMessage_PersistedConfigurationSelectsProviderAndHydratesRuntime()
    {
        await using var factory = CreateFactory();
        var seeded = await SeedConversationAsync(
            factory,
            includeProvider: true,
            includeConfiguredProvider: true);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/core/organizations/{seeded.OrganizationId}/conversations/{seeded.ConversationId}/messages/stream",
            new SendChatMessageRequest("Use my configured provider."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var broker = Assert.IsType<FakeBrokerClient>(
            factory.Services.GetRequiredService<IAgentBrokerClient>());
        Assert.Equal(seeded.ConfiguredProviderId, broker.PublishedProviderId);
        Assert.Equal(1, broker.HydrationRequests);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool runtimeReady = true)
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<CSweetDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<CSweetDbContext>>();
                    services.RemoveAll<IHostedService>();
                    services.RemoveAll<IAgentBrokerClient>();
                    services.RemoveAll<IAgentInteractiveRuntimeService>();
                    services.AddDbContext<CSweetDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                    services.AddSingleton<IAgentBrokerClient, FakeBrokerClient>();
                    services.AddSingleton<IAgentInteractiveRuntimeService>(
                        new FakeInteractiveRuntimeService(runtimeReady));
                });
            });
    }

    private static async Task<SeededConversation> SeedConversationAsync(
        WebApplicationFactory<Program> factory,
        bool includeProvider,
        bool includeConfiguredProvider = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        var now = DateTimeOffset.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatedAt = now,
            UpdatedAt = now
        };
        var self = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = now
        };
        var installationId = Guid.NewGuid();
        var agent = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            DisplayName = "Personal Assistant",
            EmployeeType = EmployeeType.Agent,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            AgentInstallationId = installationId,
            CreatedAt = now.AddSeconds(1)
        };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            AgentOrganizationUserId = agent.Id,
            InitiatedByOrganizationUserId = self.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.CoreOrganizations.Add(organization);
        dbContext.CoreOrganizationUsers.AddRange(self, agent);
        dbContext.CoreConversations.Add(conversation);

        Guid? configuredProviderId = null;
        if (includeProvider)
        {
            dbContext.LlmProviderProfiles.Add(new LlmProviderProfile
            {
                Id = Guid.NewGuid(),
                Name = "Local Provider",
                ProviderType = LlmProviderType.LmStudio,
                BaseUrl = "http://localhost:1234",
                DefaultChatModel = "local-model",
                SupportsStreaming = true,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            });

            if (includeConfiguredProvider)
            {
                configuredProviderId = Guid.NewGuid();
                dbContext.LlmProviderProfiles.Add(new LlmProviderProfile
                {
                    Id = configuredProviderId.Value,
                    Name = "Configured Provider",
                    ProviderType = LlmProviderType.LmStudio,
                    BaseUrl = "http://configured:1234",
                    DefaultChatModel = "configured-model",
                    SupportsStreaming = true,
                    IsEnabled = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                dbContext.AgentInstallationConfigurations.Add(new AgentInstallationConfiguration
                {
                    Id = Guid.NewGuid(),
                    AgentInstallationId = installationId,
                    SchemaVersion = "1.0",
                    SettingsJson = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["llmProviderId"] = configuredProviderId.Value.ToString(),
                        ["llmModel"] = "configured-model"
                    }),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        await dbContext.SaveChangesAsync();
        return new SeededConversation(organization.Id, conversation.Id, configuredProviderId);
    }

    private sealed record SeededConversation(
        Guid OrganizationId,
        Guid ConversationId,
        Guid? ConfiguredProviderId);

    private sealed class FakeInteractiveRuntimeService(bool isReady) : IAgentInteractiveRuntimeService
    {
        public Task<AgentRuntimeReadinessResponse> EnsureReadyAsync(
            Guid installationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Response(installationId));

        public Task<AgentRuntimeReadinessResponse> GetStatusAsync(
            Guid installationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Response(installationId));

        private AgentRuntimeReadinessResponse Response(Guid installationId) =>
            new(
                installationId,
                Guid.NewGuid(),
                isReady ? AgentRuntimeReadinessStages.Ready : AgentRuntimeReadinessStages.WaitingForBroker,
                isReady ? AgentRuntimeStatus.Running.ToString() : AgentRuntimeStatus.WaitingForBrokerRegistration.ToString(),
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                isReady ? DateTimeOffset.UtcNow : null,
                IsReady: isReady,
                IsTerminal: false);
    }

    private sealed class FakeBrokerClient : IAgentBrokerClient
    {
        private readonly IChatStreamRouter _router;

        public FakeBrokerClient(IChatStreamRouter router)
        {
            _router = router;
        }

        public Guid? PublishedProviderId { get; private set; }
        public int HydrationRequests { get; private set; }

        public Task StartAsync(RegisterAgent registration, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async IAsyncEnumerable<BrokerToAgentMessage> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task PublishEventAsync(
            PublishEvent message,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("com.csweet.user.message.received.v1", message.EventType);
            using var payload = JsonDocument.Parse(message.Payload.ToByteArray());
            PublishedProviderId = payload.RootElement.GetProperty("providerProfileId").GetGuid();
            var conversationMarker = message.Subject.LastIndexOf("/conversation/", StringComparison.Ordinal);
            Assert.True(conversationMarker >= 0);
            var conversationId = Guid.Parse(message.Subject[(conversationMarker + "/conversation/".Length)..]);

            _router.Publish(conversationId, new ChatStreamChunk(0, "Hello ", IsFinal: false));
            _router.Publish(conversationId, new ChatStreamChunk(1, "there", IsFinal: false));
            _router.Publish(conversationId, new ChatStreamChunk(2, string.Empty, IsFinal: true));

            return Task.CompletedTask;
        }

        public Task<CapabilityResult> InvokeCapabilityAsync(
            RequestCapability request,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(CSweet.Contracts.Agents.AgentConfigurationCapabilities.Update, request.Capability);
            HydrationRequests++;
            var response = new CSweet.Contracts.Agents.AgentConfigurationUpdateResponse(
                true,
                "Configuration applied.",
                JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(
                    request.Payload.ToByteArray()) ?? new Dictionary<string, JsonElement>());
            return Task.FromResult(new CapabilityResult
            {
                Succeeded = true,
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(response))
            });
        }

        public Task SendCapabilityResultAsync(
            CapabilityResult result,
            string? correlationId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
