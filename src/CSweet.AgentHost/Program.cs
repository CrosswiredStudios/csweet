using CSweet.AgentHost.Broker;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSweet.Application.Setup;
using CSweet.Infrastructure.Setup;
using CSweet.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http2));
builder.AddServiceDefaults();
builder.Services.AddGrpc();
builder.AddCSweetInfrastructure();
builder.Services
    .AddOptions<AgentBrokerPolicyOptions>()
    .Bind(builder.Configuration.GetSection(AgentBrokerPolicyOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<ConfiguredAgentAuthorizationPolicy>();
builder.Services.AddScoped<IAgentAuthorizationPolicy, PersistedAgentAuthorizationPolicy>();
builder.Services.AddSingleton<AgentSessionRegistry>();
builder.Services.AddScoped<IAgentRuntimeSignalService, AgentRuntimeSignalService>();
builder.Services.AddScoped<PlatformLlmCapabilityHandler>();

var app = builder.Build();

app.MapGrpcService<AgentBrokerService>();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    service = "CSweet.AgentHost",
    status = "ok",
    protocol = "csweet-agent-v1"
}));

app.Run();

public partial class Program;
