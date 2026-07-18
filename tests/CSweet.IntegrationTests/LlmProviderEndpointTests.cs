using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Llm;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CSweet.IntegrationTests;

public class LlmProviderEndpointTests
{
    [Fact]
    public async Task ProviderProfile_CanBeCreatedListedAndFetched()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var created = await CreateProfileAsync(client);
        var list = await client.GetFromJsonAsync<IReadOnlyList<LlmProviderProfileResponse>>("/api/llm-provider-profiles");
        var fetched = await client.GetFromJsonAsync<LlmProviderProfileResponse>($"/api/llm-provider-profiles/{created.Id}");

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.DoesNotContain("secret-test-key", JsonSerializer.Serialize(created), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(list);
        Assert.Contains(list, x => x.Id == created.Id);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task ProviderProfile_CanBeUpdatedAndDeleted()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var created = await CreateProfileAsync(client);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/llm-provider-profiles/{created.Id}",
            new UpdateLlmProviderProfileRequest(
                "Updated LM Studio",
                LlmProviderType.OpenAiCompatible,
                "http://fake-provider/v2",
                "updated-key",
                ReplaceApiKey: true,
                "updated-model",
                null,
                8192,
                1024,
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false,
                IsEnabled: true));
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<LlmProviderProfileActionResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updateResult?.Profile);
        Assert.Equal("Updated LM Studio", updateResult.Profile.Name);
        Assert.Equal("updated-model", updateResult.Profile.DefaultChatModel);

        var deleteResponse = await client.DeleteAsync($"/api/llm-provider-profiles/{created.Id}");
        var fetchedAfterDelete = await client.GetAsync($"/api/llm-provider-profiles/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fetchedAfterDelete.StatusCode);
    }

    [Theory]
    [InlineData(LlmProviderType.LmStudio)]
    [InlineData(LlmProviderType.UnslothStudio)]
    [InlineData(LlmProviderType.Ollama)]
    [InlineData(LlmProviderType.Vllm)]
    [InlineData(LlmProviderType.OpenAi)]
    [InlineData(LlmProviderType.GoogleGemini)]
    [InlineData(LlmProviderType.OpenRouter)]
    [InlineData(LlmProviderType.Groq)]
    [InlineData(LlmProviderType.TogetherAi)]
    [InlineData(LlmProviderType.Custom)]
    public async Task PreviewModelCatalog_ReturnsAvailableModelsForSupportedSetupProviders(LlmProviderType providerType)
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/llm-provider-profiles/model-catalog/preview",
            new PreviewModelCatalogRequest(
                providerType,
                "http://fake-provider/v1",
                "secret-test-key"));
        var result = await response.Content.ReadFromJsonAsync<PreviewModelCatalogResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Contains(result.Models, model => model.Id == "local-model");
    }

    [Fact]
    public async Task TestProvider_PersistsCapabilityTestAndUpdatesLastSuccessfulConnection()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var created = await CreateProfileAsync(client);

        var response = await client.PostAsync($"/api/llm-provider-profiles/{created.Id}/test", content: null);
        var result = await response.Content.ReadFromJsonAsync<ModelCapabilityTestResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.ConnectionSucceeded);
        Assert.True(result.ChatSucceeded);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        var persistedTest = await dbContext.ModelCapabilityTests.SingleAsync(x => x.ProviderProfileId == created.Id);
        var profile = await dbContext.LlmProviderProfiles.SingleAsync(x => x.Id == created.Id);

        Assert.True(persistedTest.ChatSucceeded);
        Assert.NotNull(profile.LastSuccessfulConnectionAt);
    }

    [Fact]
    public async Task SetDefaultChatProvider_SucceedsOnlyAfterSuccessfulChatTest()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var created = await CreateProfileAsync(client);

        var beforeTestResponse = await client.PostAsJsonAsync(
            "/api/setup/default-chat-provider",
            new SetDefaultChatProviderRequest(created.Id));

        Assert.Equal(HttpStatusCode.BadRequest, beforeTestResponse.StatusCode);

        await client.PostAsync($"/api/llm-provider-profiles/{created.Id}/test", content: null);

        var afterTestResponse = await client.PostAsJsonAsync(
            "/api/setup/default-chat-provider",
            new SetDefaultChatProviderRequest(created.Id));

        Assert.Equal(HttpStatusCode.OK, afterTestResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        var configuration = await dbContext.SystemConfigurations.SingleAsync();

        Assert.Equal(created.Id, configuration.DefaultChatProviderId);
    }

    private static async Task<LlmProviderProfileResponse> CreateProfileAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/llm-provider-profiles",
            new CreateLlmProviderProfileRequest(
                "Local LM Studio",
                LlmProviderType.LmStudio,
                "http://fake-provider/v1",
                "secret-test-key",
                "local-model",
                null,
                null,
                null,
                SupportsStreaming: true,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                SupportsVision: false));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LlmProviderProfileResponse>())!;
    }

    private static WebApplicationFactory<Program> CreateFactory()
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
                    services.AddDbContext<CSweetDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));

                    services.RemoveAll<OpenAiCompatibleProviderClient>();
                    services.AddScoped(_ => new OpenAiCompatibleProviderClient(new HttpClient(new FakeProviderHandler())
                    {
                        Timeout = TimeSpan.FromSeconds(5)
                    }));
                });
            });
    }

    private sealed class FakeProviderHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(JsonResponse(new
                {
                    data = new[] { new { id = "local-model", owned_by = "fake-provider" } }
                }));
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(JsonResponse(new
                {
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = "READY" } }
                    }
                }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse(object body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
