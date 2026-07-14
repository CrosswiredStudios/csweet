using Microsoft.Extensions.Hosting;
using CSweet.WorkerHost;
using CSweet.Infrastructure;
using CSweet.Infrastructure.Setup;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults (health checks, OpenTelemetry, resilience)
builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();

builder.Services.AddHostedService<AgentBuildWorker>();
builder.Services.AddHostedService<AgentScheduleWorker>();

var host = builder.Build();
host.Run();
