using System.Net;
using System.Text.Json;
using CSweet.AI.Providers;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Llm;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public class LlmProviderTests
{
    [Fact]
    public void LmStudioPreset_ReturnsExpectedDefaults()
    {
        var preset = LlmProviderPresets.LmStudioLocalhost();

        Assert.Equal("Local LM Studio", preset.Name);
        Assert.Equal(LlmProviderType.LmStudio, preset.ProviderType);
        Assert.Equal("http://localhost:1234/v1", preset.BaseUrl);
        Assert.Equal("lm-studio", preset.ApiKeyPlaceholder);
        Assert.True(preset.SupportsStreaming);
        Assert.False(preset.SupportsToolCalling);
        Assert.False(preset.SupportsStructuredOutput);
        Assert.False(preset.SupportsVision);
    }

    [Fact]
    public async Task CreateProviderProfile_InvalidBaseUrl_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var tester = CreateTester(dbContext, CreateReadyHandler());
        var service = new LlmProviderProfileService(dbContext, new InMemoryLlmProviderSecretStore(), tester);

        var result = await service.CreateAsync(new CreateLlmProviderProfileRequest(
            "Local LM Studio",
            LlmProviderType.LmStudio,
            "not-a-url",
            null,
            "local-model",
            null,
            null,
            null,
            SupportsStreaming: true,
            SupportsToolCalling: false,
            SupportsStructuredOutput: false,
            SupportsVision: false));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_base_url", result.ErrorCode);
    }

    [Fact]
    public async Task ChatTest_MarksSuccess_WhenResponseIsReady()
    {
        await using var dbContext = CreateDbContext();
        var profile = await AddProfileAsync(dbContext);
        var tester = CreateTester(dbContext, CreateReadyHandler());

        var result = await tester.TestAsync(profile.Id);

        Assert.True(result.ConnectionSucceeded);
        Assert.True(result.ChatSucceeded);
        Assert.True(result.StreamingSucceeded);
        Assert.Null(result.FailureMessage);
        Assert.NotNull((await dbContext.LlmProviderProfiles.SingleAsync()).LastSuccessfulConnectionAt);
    }

    [Fact]
    public async Task ChatTest_MarksFailure_OnTimeout()
    {
        await using var dbContext = CreateDbContext();
        var profile = await AddProfileAsync(dbContext);
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return JsonResponse(new { data = Array.Empty<object>() });
        });

        var tester = CreateTester(dbContext, handler, TimeSpan.FromMilliseconds(20));

        var result = await tester.TestAsync(profile.Id);

        Assert.False(result.ConnectionSucceeded);
        Assert.False(result.ChatSucceeded);
        Assert.Equal("Timeout.", result.FailureMessage);
    }

    [Fact]
    public async Task ChatTest_UsesReasoningFriendlyCompletionBudget()
    {
        await using var dbContext = CreateDbContext();
        var profile = await AddProfileAsync(dbContext, supportsStreaming: false);
        int? requestedMaxTokens = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse(new { data = new[] { new { id = "local-model" } } });
            }

            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(body);
            requestedMaxTokens = document.RootElement.GetProperty("max_tokens").GetInt32();

            return ChatResponse("READY");
        });
        var tester = CreateTester(dbContext, handler);

        var result = await tester.TestAsync(profile.Id);

        Assert.True(result.ChatSucceeded);
        Assert.True(requestedMaxTokens >= 128);
    }

    [Fact]
    public async Task StructuredOutputTest_FailsGracefully_WhenResponseIsNotJson()
    {
        await using var dbContext = CreateDbContext();
        var profile = await AddProfileAsync(dbContext, supportsStructuredOutput: true);
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync();

            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse(new { data = new[] { new { id = "local-model" } } });
            }

            if (body.Contains("exact shape", StringComparison.OrdinalIgnoreCase))
            {
                return ChatResponse("not-json");
            }

            return ChatResponse("READY");
        });

        var tester = CreateTester(dbContext, handler);

        var result = await tester.TestAsync(profile.Id);

        Assert.True(result.ChatSucceeded);
        Assert.False(result.StructuredOutputSucceeded);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public async Task OptionalStreamingTimeout_DoesNotFailRequiredChatTest()
    {
        await using var dbContext = CreateDbContext();
        var profile = await AddProfileAsync(dbContext);
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse(new { data = new[] { new { id = "local-model" } } });
            }

            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (body.Contains("\"stream\":true", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            return ChatResponse("READY");
        });
        var tester = CreateTester(dbContext, handler, TimeSpan.FromMilliseconds(20));

        var result = await tester.TestAsync(profile.Id);

        Assert.True(result.ConnectionSucceeded);
        Assert.True(result.ChatSucceeded);
        Assert.False(result.StreamingSucceeded);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public async Task ToolCallingUnsupported_DoesNotFailWholeProviderSetup()
    {
        await using var dbContext = CreateDbContext();
        var profile = await AddProfileAsync(dbContext, supportsToolCalling: true);
        var tester = CreateTester(dbContext, CreateReadyHandler());

        var result = await tester.TestAsync(profile.Id);

        Assert.True(result.ConnectionSucceeded);
        Assert.True(result.ChatSucceeded);
        Assert.False(result.ToolCallingSucceeded);
        Assert.Null(result.FailureMessage);
    }

    private static LlmConnectionTester CreateTester(
        CSweetDbContext dbContext,
        HttpMessageHandler handler,
        TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };

        return new LlmConnectionTester(
            dbContext,
            new InMemoryLlmProviderSecretStore(),
            new OpenAiCompatibleProviderClient(httpClient));
    }

    private static StubHttpMessageHandler CreateReadyHandler()
    {
        return new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(JsonResponse(new { data = new[] { new { id = "local-model", owned_by = "local" } } }));
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(ChatResponse("READY"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
    }

    private static HttpResponseMessage ChatResponse(string content)
    {
        return JsonResponse(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content } }
            }
        });
    }

    private static HttpResponseMessage JsonResponse(object body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static async Task<LlmProviderProfile> AddProfileAsync(
        CSweetDbContext dbContext,
        bool supportsStreaming = true,
        bool supportsStructuredOutput = false,
        bool supportsToolCalling = false)
    {
        var now = DateTimeOffset.UtcNow;
        var profile = new LlmProviderProfile
        {
            Id = Guid.NewGuid(),
            Name = "Local LM Studio",
            ProviderType = LlmProviderType.LmStudio,
            BaseUrl = "http://fake-provider/v1",
            DefaultChatModel = "local-model",
            SupportsStreaming = supportsStreaming,
            SupportsStructuredOutput = supportsStructuredOutput,
            SupportsToolCalling = supportsToolCalling,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.LlmProviderProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return profile;
    }

    private static CSweetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CSweetDbContext(options);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
