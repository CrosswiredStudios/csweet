using System.Net;
using System.Net.Http.Json;
using CSweet.Contracts.BusinessOnboarding;
using CSweet.Contracts.Core;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CSweet.IntegrationTests;

public class BusinessOnboardingEndpointTests
{
    [Fact]
    public async Task CompleteOnboarding_IsBlockedUntilFirstRunSetupIsComplete()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/business-onboarding/complete",
            CreateRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_ReturnsOrganizationAndCreatesInitialGraph()
    {
        await using var factory = CreateFactory();
        await MarkSetupCompleteAsync(factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/business-onboarding/complete",
            CreateRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CompleteBusinessOnboardingResponse>();

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OrganizationId);
        Assert.Equal(5, result.CreatedRoleCount);
        Assert.Equal(5, result.CreatedTaskCount);
        Assert.Equal($"/organizations/{result.OrganizationId}/command-center", result.NextRoute);

        var organization = await client.GetFromJsonAsync<OrganizationResponse>($"/api/organizations/{result.OrganizationId}");
        var roles = await client.GetFromJsonAsync<IReadOnlyList<RoleResponse>>($"/api/organizations/{result.OrganizationId}/roles");
        var tasks = await client.GetFromJsonAsync<IReadOnlyList<WorkTaskResponse>>($"/api/organizations/{result.OrganizationId}/tasks");
        var workers = await client.GetFromJsonAsync<IReadOnlyList<WorkerResponse>>($"/api/organizations/{result.OrganizationId}/workers");

        Assert.NotNull(organization);
        Assert.Equal("Example Co", organization.Name);
        Assert.NotNull(roles);
        Assert.Equal(5, roles.Count);
        Assert.NotNull(tasks);
        Assert.Equal(5, tasks.Count);
        Assert.Contains(tasks, x => x.Title == "Create 30-day execution plan" && x.AssignedWorkerId == result.DefaultWorkerId);
        Assert.NotNull(workers);
        Assert.Contains(workers, x => x.Id == result.DefaultWorkerId && x.Name == "Local Strategy Agent");
    }

    private static CompleteBusinessOnboardingRequest CreateRequest() =>
        new(
            "Example Co",
            "Software",
            "Idea",
            "Launch a paid MVP in 30 days",
            ["solo founder", "limited budget"],
            "Balanced and practical");

    private static async Task MarkSetupCompleteAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        dbContext.SystemConfigurations.Add(new SystemConfiguration
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
                });
            });
    }
}
