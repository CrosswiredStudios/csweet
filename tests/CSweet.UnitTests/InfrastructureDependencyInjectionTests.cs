using CSweet.AI.Providers;
using CSweet.Infrastructure;
using CSweet.Infrastructure.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CSweet.UnitTests;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void ResolvingLlmSecretStore_DoesNotRequireWritableParentDirectory()
    {
        var blocker = Path.GetTempFileName();
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Production
            });
            builder.Configuration["ConnectionStrings:Postgres"] =
                "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";
            builder.Configuration["CSweet:Secrets:FilePath"] =
                Path.Combine(blocker, "provider-secrets.json");
            builder.AddCSweetInfrastructure();

            using var host = builder.Build();
            var store = host.Services.GetRequiredService<ILlmProviderSecretStore>();

            Assert.IsType<FileLlmProviderSecretStore>(store);
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}
