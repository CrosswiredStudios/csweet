using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Contracts.Agents;
using Google.Protobuf;

namespace CSweet.Api.Agents;

public static class AgentManagementEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CapabilityTimeout = TimeSpan.FromSeconds(15);

    public static IServiceCollection AddAgentManagement(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentCatalogOptions>()
            .Bind(configuration.GetSection(AgentCatalogOptions.SectionName));
        services.AddScoped<IAgentCatalogService, AgentCatalogService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAgentManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/agents");

        group.MapGet("", async (
            IAgentCatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await catalog.ListAsync(cancellationToken));
        });

        group.MapGet("/{agentId}/configuration", async (
            string agentId,
            IAgentBrokerClient broker,
            CancellationToken cancellationToken) =>
        {
            var result = await InvokeAgentConfigurationCapabilityAsync(
                broker,
                agentId,
                AgentConfigurationCapabilities.Describe,
                payload: [],
                cancellationToken);

            return ToHttpResult<AgentConfigurationSchemaResponse>(result);
        });

        group.MapPost("/{agentId}/configuration", async (
            string agentId,
            UpdateAgentConfigurationRequest request,
            IAgentBrokerClient broker,
            CancellationToken cancellationToken) =>
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions);
            var result = await InvokeAgentConfigurationCapabilityAsync(
                broker,
                agentId,
                AgentConfigurationCapabilities.Update,
                payload,
                cancellationToken);

            return ToHttpResult<AgentConfigurationUpdateResponse>(result);
        });

        return endpoints;
    }

    private static async Task<CapabilityResult> InvokeAgentConfigurationCapabilityAsync(
        IAgentBrokerClient broker,
        string agentId,
        string capability,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CapabilityTimeout);

        return await broker.InvokeCapabilityAsync(
            new RequestCapability
            {
                Capability = capability,
                TargetAgentId = agentId,
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(payload)
            },
            correlationId: Guid.NewGuid().ToString("N"),
            timeout.Token);
    }

    private static IResult ToHttpResult<T>(CapabilityResult result)
    {
        if (!result.Succeeded)
        {
            return Results.Conflict(new
            {
                error = string.IsNullOrWhiteSpace(result.Error)
                    ? "The agent could not complete the configuration request."
                    : result.Error
            });
        }

        var response = JsonSerializer.Deserialize<T>(
            result.Payload.ToByteArray(),
            SerializerOptions);

        return response is null
            ? Results.Conflict(new { error = "The agent returned an empty configuration response." })
            : Results.Ok(response);
    }
}
