using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CSweet.Contracts.Security;
using CSweet.Domain.Core;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CSweet.IntegrationTests;

public sealed class SecurityAuditEndpointTests
{
    [Fact]
    public async Task Timeline_AllowsHumanManagerAndDeniesAgentManager()
    {
        await using var factory = CreateFactory();
        var organizationId = Guid.NewGuid();
        var humanId = Guid.NewGuid();
        var agentIdentityId = Guid.NewGuid();
        await SeedAsync(factory, organizationId, humanId, agentIdentityId);

        var human = factory.CreateClient();
        human.DefaultRequestHeaders.Add("X-Test-UserId", humanId.ToString("D"));
        var allowed = await human.GetAsync($"/api/organizations/{organizationId:D}/security/events");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var page = await allowed.Content.ReadFromJsonAsync<SecurityEventPageResponse>();
        Assert.Contains(page!.Items, x => x.EventType == "security.timeline.viewed" && x.Outcome == "Accepted");

        var agent = factory.CreateClient();
        agent.DefaultRequestHeaders.Add("X-Test-UserId", agentIdentityId.ToString("D"));
        var denied = await agent.GetAsync($"/api/organizations/{organizationId:D}/security/events");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        Assert.Contains(await db.AuditEvents.Where(x => x.OrganizationId == organizationId).ToListAsync(),
            x => x.EventType == "security.timeline.viewed" && x.Outcome == "Denied" &&
                 x.ActorApplicationUserId == agentIdentityId);
    }

    private static async Task SeedAsync(WebApplicationFactory<Program> factory, Guid organizationId, Guid humanId, Guid agentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.SystemConfigurations.Add(new SystemConfiguration { Id = Guid.NewGuid(), IsFirstRunComplete = true, CreatedAt = now, UpdatedAt = now });
        db.CoreOrganizations.Add(new Organization { Id = organizationId, Name = "Audited company", Status = OrganizationStatus.Active, CreatedAt = now, UpdatedAt = now });
        db.CoreOrganizationUsers.AddRange(
            new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, ApplicationUserId = humanId, DisplayName = "Human manager", EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Manager, CreatedAt = now },
            new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, ApplicationUserId = agentId, DisplayName = "Agent manager", EmployeeType = EmployeeType.Agent, PermissionLevel = OrganizationPermissionLevel.Manager, CreatedAt = now });
        await db.SaveChangesAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseName = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CSweetDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CSweetDbContext>>();
                services.AddDbContext<CSweetDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            });
        });
    }
}

file sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SecurityAuditTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var value) || !Guid.TryParse(value, out var id))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString("D"))], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
