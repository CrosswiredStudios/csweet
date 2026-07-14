using System.Net;
using System.Net.Http.Json;
using System.Text;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Contracts.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CSweet.IntegrationTests;

public class AgentImportPreviewEndpointTests
{
    [Fact]
    public async Task Post_PreviewsAndPersistsRootManifest()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        await MarkSetupCompleteAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/agents/imports/preview",
            new PreviewAgentImportRequest("https://github.com/example/research-agent"));
        var preview = await response.Content.ReadFromJsonAsync<AgentImportPreviewResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(preview);
        Assert.Equal("com.example.research-agent", preview.AgentId);
        Assert.Equal("Previewed", preview.Status);
        Assert.Equal("src/ResearchAgent/ResearchAgent.csproj", preview.ProjectPath);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        Assert.Single(await dbContext.AgentPackageSources.ToListAsync());
        Assert.Single(await dbContext.AgentPackageVersions.ToListAsync());
        Assert.Single(await dbContext.AuditEvents
            .Where(x => x.EventType == "agent-import.previewed")
            .ToListAsync());
    }

    [Fact]
    public async Task Post_RejectsUnsupportedRepositoryUrl()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        await MarkSetupCompleteAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/agents/imports/preview",
            new PreviewAgentImportRequest("https://gitlab.com/example/research-agent"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("GitHub", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Install_CreatesGrantAndScheduleAndSupportsManagementActions()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        await MarkSetupCompleteAsync(factory);
        var settingsResponse = await client.PutAsJsonAsync(
            "/api/agent-runtime/settings",
            new UpdateAgentRuntimeSettingsRequest(EnableImportedAgents: true));
        settingsResponse.EnsureSuccessStatusCode();
        var previewResponse = await client.PostAsJsonAsync(
            "/api/agents/imports/preview",
            new PreviewAgentImportRequest("https://github.com/example/research-agent"));
        var preview = await previewResponse.Content.ReadFromJsonAsync<AgentImportPreviewResponse>();
        Assert.NotNull(preview);

        var installResponse = await client.PostAsJsonAsync(
            $"/api/agents/imports/{preview.ImportId}/install",
            new InstallAgentRequest(
                "default",
                "Periodic",
                900,
                "Skip",
                ["research.execute.v1"],
                [],
                [],
                [],
                [],
                600,
                512,
                50));
        var installation = await installResponse.Content.ReadFromJsonAsync<AgentInstallationResponse>();

        Assert.Equal(HttpStatusCode.OK, installResponse.StatusCode);
        Assert.NotNull(installation);
        Assert.Equal("Periodic", installation.Schedule.ActivationMode);
        Assert.NotNull(installation.Schedule.NextTickAt);

        var listed = await client.GetFromJsonAsync<IReadOnlyList<AgentInstallationResponse>>(
            "/api/agents/installations");
        Assert.Single(listed!);

        var scheduleResponse = await client.PutAsJsonAsync(
            $"/api/agents/installations/{installation.Id}/schedule",
            new UpdateAgentScheduleRequest("Manual", 1200, "Queue", 300, true));
        var scheduled = await scheduleResponse.Content.ReadFromJsonAsync<AgentInstallationResponse>();
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
        Assert.Equal("Manual", scheduled!.Schedule.ActivationMode);
        Assert.Null(scheduled.Schedule.NextTickAt);

        var runResponse = await client.PostAsync(
            $"/api/agents/installations/{installation.Id}/run-now",
            null);
        var run = await runResponse.Content.ReadFromJsonAsync<AgentInstallationResponse>();
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
        Assert.NotNull(run?.Schedule.RunRequestedAt);

        var disableResponse = await client.PostAsync(
            $"/api/agents/installations/{installation.Id}/disable",
            null);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<AgentInstallationResponse>();
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        Assert.False(disabled!.IsEnabled);
        Assert.False(disabled.Schedule.IsEnabled);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        Assert.Single(await dbContext.AgentInstallations.ToListAsync());
        Assert.Single(await dbContext.AgentInstallationGrants.ToListAsync());
        Assert.Single(await dbContext.AgentSchedules.ToListAsync());
    }

    private static async Task MarkSetupCompleteAsync(WebApplicationFactory<Program> factory)
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        dbContext.SystemConfigurations.Add(new CSweet.Domain.Setup.SystemConfiguration
        {
            Id = Guid.NewGuid(),
            IsFirstRunComplete = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
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
                    services.RemoveAll<IGitHubAgentRepositoryClient>();
                    services.AddScoped<IGitHubAgentRepositoryClient, FakeGitHubAgentRepositoryClient>();
                });
            });
    }

    private sealed class FakeGitHubAgentRepositoryClient : IGitHubAgentRepositoryClient
    {
        private static readonly byte[] Manifest = Encoding.UTF8.GetBytes("""
            {
              "manifestVersion": "1.0",
              "id": "com.example.research-agent",
              "name": "Research Agent",
              "version": "1.2.3",
              "publisher": { "id": "com.example", "name": "Example" },
              "runtime": {
                "type": "dotnet-project",
                "projectPath": "src/ResearchAgent/ResearchAgent.csproj",
                "targetFramework": "net10.0",
                "defaultActivationMode": "Periodic"
              },
              "protocol": { "minimumVersion": "1.0", "maximumVersion": "1.x" },
              "capabilities": ["research.execute.v1"]
            }
            """);

        public Task<string> GetDefaultBranchAsync(
            string repositoryOwner,
            string repositoryName,
            CancellationToken cancellationToken) => Task.FromResult("main");

        public Task<string> ResolveCommitShaAsync(
            string repositoryOwner,
            string repositoryName,
            string reference,
            CancellationToken cancellationToken) =>
            Task.FromResult("0123456789abcdef0123456789abcdef01234567");

        public Task<byte[]> GetRootManifestAsync(
            string repositoryOwner,
            string repositoryName,
            string commitSha,
            CancellationToken cancellationToken) => Task.FromResult(Manifest);
    }
}