using System.Net;
using System.Net.Http.Json;
using CSweet.Contracts.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CSweet.IntegrationTests;

public class SetupEndpointTests
{
    [Fact]
    public async Task Get_SetupStatus_ReturnsOk()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/setup/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FreshDatabase_ReturnsFirstRunIncomplete()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<SetupStatusResponse>("/api/setup/status");

        Assert.NotNull(status);
        Assert.False(status.IsFirstRunComplete);
        Assert.Contains(status.Steps, x => x.Key == "admin-user");
    }

    [Fact]
    public async Task Post_SetupComplete_FailsWhenPrerequisitesAreMissing()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/setup/complete", content: null);
        var result = await response.Content.ReadFromJsonAsync<SetupActionResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Succeeded);
        Assert.Equal("provider_profile_required", result.ErrorCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<CSweetDbContext>>();
                    services.AddDbContext<CSweetDbContext>(options =>
                        options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
                });
            });
    }
}
