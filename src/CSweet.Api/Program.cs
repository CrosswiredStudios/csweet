using CSweet.Api.Agents;
using CSweet.Api.BusinessOnboarding;
using CSweet.Api.Auth;
using CSweet.Api.Chat;
using CSweet.Api.Core;
using CSweet.Api.Llm;
using CSweet.Api.Planning;
using CSweet.Api.Setup;
using CSweet.Application.Planning;
using CSweet.Infrastructure;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();
builder.Services.AddChatGateway(builder.Configuration);
builder.Services.AddAgentManagement(builder.Configuration);
builder.Services.AddAgentRateLimiting();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "CSweet.Auth"
        : "__Host-CSweet.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    };
});
var authorization = builder.Services.AddAuthorizationBuilder();
authorization.SetFallbackPolicy(builder.Environment.IsEnvironment("Testing")
    ? new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build()
    : new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "CSweet.Antiforgery"
        : "__Host-CSweet.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.HeaderName = "X-CSWEET-CSRF";
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentBlazorApp", policy =>
    {
        policy.SetIsOriginAllowed(IsDevelopmentLoopbackOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await CSweetDatabaseInitializer.EnsureDatabaseReadyAsync(app.Services);
    app.UseCors("DevelopmentBlazorApp");

    // Seed planning workflows on startup in development
    using (var scope = app.Services.CreateScope())
    {
        var workflowService = scope.ServiceProvider.GetRequiredService<IPlanningWorkflowService>();
        await workflowService.EnsureSeededAsync();
    }
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ApiAntiforgeryMiddleware>();
app.UseMiddleware<FirstRunSetupGuardMiddleware>();

app.MapGet("/api/health", () => new { status = "ok", service = "CSweet.Api" }).AllowAnonymous();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthenticationEndpoints();

app.MapLlmProviderProfileEndpoints();
app.MapSetupEndpoints();
app.MapAgentRuntimeSettingsEndpoints();
app.MapPlanningRunEndpoints();
app.MapPlanningDocumentEndpoints();
app.MapPlanningWorkflowEndpoints();

// Core business domain endpoints
app.MapBusinessOnboardingEndpoints();
app.MapCoreOrganizationEndpoints();
app.MapOrganizationUserEndpoints();
app.MapRoleEndpoints();
app.MapStrategicObjectiveEndpoints();
app.MapWorkerEndpoints();
app.MapWorkTaskEndpoints();
app.MapTaskRunEndpoints();
app.MapArtifactEndpoints();
app.MapConversationEndpoints();
app.MapChatMessageEndpoints();
app.MapAgentManagementEndpoints();

app.MapControllers();

app.Run();

static bool IsDevelopmentLoopbackOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    return uri.Scheme is "http" or "https" &&
        (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase));
}

public partial class Program;
