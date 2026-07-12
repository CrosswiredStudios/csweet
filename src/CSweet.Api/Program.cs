using CSweet.Api.BusinessOnboarding;
using CSweet.Api.Core;
using CSweet.Api.Llm;
using CSweet.Api.Planning;
using CSweet.Api.Setup;
using CSweet.Application.Planning;
using CSweet.Infrastructure;
using CSweet.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentBlazorApp", policy =>
    {
        policy.SetIsOriginAllowed(IsDevelopmentLoopbackOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
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

app.UseMiddleware<FirstRunSetupGuardMiddleware>();

app.MapGet("/api/health", () => new { status = "ok", service = "CSweet.Api" });

app.MapHealthChecks("/health");

app.MapLlmProviderProfileEndpoints();
app.MapSetupEndpoints();
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
