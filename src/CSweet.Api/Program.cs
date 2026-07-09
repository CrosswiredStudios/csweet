using CSweet.Api.Setup;
using CSweet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentBlazorApp", policy =>
    {
        policy.WithOrigins("https://localhost:7125", "http://localhost:5097")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentBlazorApp");
}

app.UseMiddleware<FirstRunSetupGuardMiddleware>();

app.MapGet("/api/health", () => new { status = "ok", service = "CSweet.Api" });

app.MapHealthChecks("/health");

app.MapSetupEndpoints();
app.MapControllers();

app.Run();

public partial class Program;
