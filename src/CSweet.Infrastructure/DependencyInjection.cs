using CSweet.Application.Core;
using CSweet.Application.BusinessOnboarding;
using CSweet.Application.Llm;
using CSweet.Application.Planning;
using CSweet.Application.Setup;
using CSweet.AI.AgentFramework;
using CSweet.AI.Providers;
using CSweet.Infrastructure.BusinessOnboarding;
using CSweet.Infrastructure.Core;
using CSweet.Infrastructure.Llm;
using CSweet.Infrastructure.Persistence;
using CSweet.Infrastructure.Planning;
using CSweet.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CSweet.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCSweetInfrastructure(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? builder.Configuration.GetConnectionString("csweet");

        builder.Services.AddDbContext<CSweetDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql(connectionString);
                return;
            }

            throw new InvalidOperationException("ConnectionStrings:Postgres or ConnectionStrings:csweet must be configured.");
        });

        builder.Services.AddScoped<ISetupService, SetupService>();
        builder.Services.AddScoped<IAuditEventWriter, AuditEventWriter>();
        builder.Services.AddScoped<IAgentRuntimeSettingsService, AgentRuntimeSettingsService>();
        builder.Services.AddScoped<IAgentImportPreviewService, AgentImportPreviewService>();
        builder.Services.AddScoped<IAgentInstallationService, AgentInstallationService>();
        builder.Services.AddScoped<IAgentBuildService, AgentBuildService>();
        builder.Services.AddSingleton<IAgentBuildExecutor, DockerAgentBuildExecutor>();
        builder.Services.AddSingleton<IDockerCommandExecutor, DockerCommandExecutor>();
        builder.Services.AddSingleton<IAgentContainerRunner, DockerAgentContainerRunner>();
        builder.Services.AddScoped<IAgentRuntimeManager, AgentRuntimeManager>();
        builder.Services.AddScoped<IAgentRuntimeSignalService, AgentRuntimeSignalService>();
        builder.Services.AddOptions<AgentRuntimeManagerOptions>()
            .Bind(builder.Configuration.GetSection(AgentRuntimeManagerOptions.SectionName));
        builder.Services.AddHttpClient<IGitHubAgentRepositoryClient, GitHubAgentRepositoryClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CSweet-Agent-Importer/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<ILlmProviderSecretStore>(_ =>
        {
            if (builder.Environment.IsEnvironment("Testing"))
            {
                return new InMemoryLlmProviderSecretStore();
            }

            return new FileLlmProviderSecretStore(GetLocalStateFilePath(
                builder.Configuration,
                "CSweet:Secrets:FilePath",
                "provider-secrets.json"));
        });
        builder.Services.AddScoped(_ => new OpenAiCompatibleProviderClient(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        }));
        builder.Services.AddScoped<ILlmProviderFactory, OpenAiCompatibleLlmProviderFactory>();
        builder.Services.AddScoped<ILlmConnectionTester, LlmConnectionTester>();
        builder.Services.AddScoped<IModelCatalogClient, ModelCatalogClient>();
        builder.Services.AddScoped<ILlmProviderProfileService, LlmProviderProfileService>();
        builder.Services.AddScoped<ILlmTokenUsageService, LlmTokenUsageService>();
        builder.Services.AddScoped<IAgentRunLogWriter, AgentRunLogWriter>();
        builder.Services.AddScoped<IAgentRunner, AgentFrameworkAgentRunner>();
        builder.Services.AddScoped<IAgentWorkflowRunner, AgentFrameworkWorkflowRunner>();

        // Planning services
        builder.Services.AddScoped<IPlanningRunService, PlanningRunService>();
        builder.Services.AddScoped<IPlanningDocumentService, PlanningDocumentService>();
        builder.Services.AddScoped<IPlanningWorkflowService, PlanningWorkflowService>();

        // Core business domain services
        builder.Services.AddScoped<IBusinessOnboardingService, BusinessOnboardingService>();
        builder.Services.AddScoped<ICoreOrganizationService, CoreOrganizationService>();
        builder.Services.AddScoped<IRoleService, RoleService>();
        builder.Services.AddScoped<IStrategicObjectiveService, StrategicObjectiveService>();
        builder.Services.AddScoped<IWorkerService, WorkerService>();
        builder.Services.AddScoped<IWorkTaskService, WorkTaskService>();
        builder.Services.AddScoped<ITaskRunService, TaskRunService>();
        builder.Services.AddScoped<IArtifactService, ArtifactService>();
        builder.Services.AddScoped<IArtifactApprovalService, ArtifactApprovalService>();
        builder.Services.AddScoped<IOrganizationUserService, OrganizationUserService>();
        builder.Services.AddScoped<IConversationService, ConversationService>();

        return builder;
    }

    private static string GetLocalStateFilePath(
        IConfiguration configuration,
        string configurationKey,
        string fileName)
    {
        var configuredPath = configuration[configurationKey];
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(GetLocalStateDirectory(), fileName)
            : configuredPath;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return path;
    }

    private static string GetLocalStateDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, ".csweet")
            : Path.Combine(localAppData, "CSweet");
    }
}
