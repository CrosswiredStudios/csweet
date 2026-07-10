using CSweet.Application.Llm;
using CSweet.Application.Setup;
using CSweet.AI.AgentFramework;
using CSweet.AI.Providers;
using CSweet.Infrastructure.Llm;
using CSweet.Infrastructure.Persistence;
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

            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException("ConnectionStrings:Postgres must be configured in production.");
            }

            var sqliteConnectionString = builder.Configuration.GetConnectionString("Sqlite")
                ?? BuildLocalSqliteConnectionString(builder.Configuration);

            options.UseSqlite(sqliteConnectionString);
        });

        builder.Services.AddScoped<ISetupService, SetupService>();
        builder.Services.AddScoped<IAuditEventWriter, AuditEventWriter>();
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
        builder.Services.AddScoped<IAgentRunLogWriter, AgentRunLogWriter>();
        builder.Services.AddScoped<IAgentRunner, AgentFrameworkAgentRunner>();
        builder.Services.AddScoped<IAgentWorkflowRunner, AgentFrameworkWorkflowRunner>();

        return builder;
    }

    private static string BuildLocalSqliteConnectionString(IConfiguration configuration)
    {
        var databasePath = GetLocalStateFilePath(configuration, "CSweet:Database:FilePath", "csweet-dev.db");
        return $"Data Source={databasePath}";
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
