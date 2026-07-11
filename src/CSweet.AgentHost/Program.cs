using CSweet.AgentHost.Broker;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddGrpc();
builder.Services
    .AddOptions<AgentBrokerPolicyOptions>()
    .Bind(builder.Configuration.GetSection(AgentBrokerPolicyOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IAgentAuthorizationPolicy, ConfiguredAgentAuthorizationPolicy>();
builder.Services.AddSingleton<AgentSessionRegistry>();

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
