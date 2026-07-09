using Microsoft.Extensions.Hosting;
using CSweet.WorkerHost;
using CSweet.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults (health checks, OpenTelemetry, resilience)
builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
