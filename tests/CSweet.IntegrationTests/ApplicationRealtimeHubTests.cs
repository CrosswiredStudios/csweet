using System.Net.Http.Json;
using System.Text.Json;
using CSweet.Application.Notifications;
using CSweet.Contracts.Auth;
using CSweet.Contracts.Realtime;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Auth;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CSweet.IntegrationTests;

public sealed class ApplicationRealtimeHubTests
{
    [Fact]
    public async Task AuthenticatedConnections_ReceiveOnlyEventsForTheirOrganizationUserGroup()
    {
        await using var factory = CreateFactory();
        var first = await CreateUserAndLoginAsync(factory, "first@example.com");
        var second = await CreateUserAndLoginAsync(factory, "second@example.com");
        Guid firstOrganizationUserId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
            var organization = new Organization { Id = Guid.NewGuid(), Name = "Realtime Co",
                Status = OrganizationStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var firstMember = Member(organization.Id, first.UserId, "First");
            var secondMember = Member(organization.Id, second.UserId, "Second");
            firstOrganizationUserId = firstMember.Id;
            db.AddRange(organization, firstMember, secondMember);
            await db.SaveChangesAsync();
        }

        await using var firstConnection = Connection(factory, first.Cookie);
        await using var secondConnection = Connection(factory, second.Cookie);
        var firstEvent = new TaskCompletionSource<AppRealtimeEventEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEvent = new TaskCompletionSource<AppRealtimeEventEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        firstConnection.On<AppRealtimeEventEnvelope>("AppEvent", value => firstEvent.TrySetResult(value));
        secondConnection.On<AppRealtimeEventEnvelope>("AppEvent", value => secondEvent.TrySetResult(value));
        await firstConnection.StartAsync();
        await secondConnection.StartAsync();

        using var document = JsonDocument.Parse("{\"value\":1}");
        var envelope = new AppRealtimeEventEnvelope(Guid.NewGuid(), 1, "test.event.v1", null,
            "test/subject", DateTimeOffset.UtcNow, document.RootElement.Clone());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IApplicationRealtimePublisher>();
            await publisher.PublishAsync(new ApplicationRealtimePublication(envelope, [firstOrganizationUserId]));
        }

        Assert.Equal(envelope.EventId, (await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(5))).EventId);
        await Task.Delay(300);
        Assert.False(secondEvent.Task.IsCompleted);
    }

    private static OrganizationUser Member(Guid organizationId, Guid applicationUserId, string name) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, ApplicationUserId = applicationUserId,
        DisplayName = name, EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Contributor,
        IsActive = true, CreatedAt = DateTimeOffset.UtcNow
    };

    private static HubConnection Connection(WebApplicationFactory<Program> factory, string cookie) =>
        new HubConnectionBuilder().WithUrl("http://localhost/hubs/app-events", options =>
        {
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            options.Headers["Cookie"] = cookie;
        }).Build();

    private static async Task<(Guid UserId, string Cookie)> CreateUserAndLoginAsync(
        WebApplicationFactory<Program> factory, string email)
    {
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email,
                EmailConfirmed = true, CreatedAt = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
            id = user.Id;
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Strong!Pass123"));
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(x => x.Contains("CSweet.Auth", StringComparison.Ordinal)).Split(';')[0];
        return (id, cookie);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseName = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("AuthenticationTesting");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CSweetDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CSweetDbContext>>();
                services.AddDbContext<CSweetDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
    }
}
