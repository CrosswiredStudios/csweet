using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using IAgentBrokerClient = CSweet.Agent.SDK.IAgentBrokerClient;
using CSweet.Application.Setup;
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

        group.MapPost("/imports/preview", async (
            PreviewAgentImportRequest request,
            IAgentImportPreviewService importPreviewService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var preview = await importPreviewService.PreviewAsync(request, cancellationToken);
                return Results.Ok(preview);
            }
            catch (AgentImportPreviewException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting(AgentRateLimiting.ImportPolicy);

        group.MapPost("/imports/{importId:guid}/install", async (
            Guid importId,
            InstallAgentRequest request,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
            await ExecuteInstallationActionAsync(
                () => installationService.InstallAsync(importId, request, cancellationToken)))
            .RequireRateLimiting(AgentRateLimiting.BuildPolicy);

        group.MapGet("/installations", async (
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
            Results.Ok(await installationService.ListAsync(cancellationToken)));

        group.MapGet("/installations/{installationId:guid}", async (
            Guid installationId,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
        {
            var installation = await installationService.GetAsync(installationId, cancellationToken);
            return installation is null ? Results.NotFound() : Results.Ok(installation);
        });

        group.MapPut("/installations/{installationId:guid}/schedule", async (
            Guid installationId,
            UpdateAgentScheduleRequest request,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
            await ExecuteInstallationActionAsync(
                () => installationService.UpdateScheduleAsync(installationId, request, cancellationToken)));

        group.MapPost("/installations/{installationId:guid}/run-now", async (
            Guid installationId,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
            await ExecuteInstallationActionAsync(
                () => installationService.RunNowAsync(installationId, cancellationToken)))
            .RequireRateLimiting(AgentRateLimiting.RunPolicy);

        group.MapPost("/installations/{installationId:guid}/disable", async (
            Guid installationId,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
            await ExecuteInstallationActionAsync(
                () => installationService.DisableAsync(installationId, cancellationToken)));

        group.MapPost("/installations/{installationId:guid}/enable", async (
            Guid installationId,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
            await ExecuteInstallationActionAsync(
                () => installationService.EnableAsync(installationId, cancellationToken)));

        group.MapGet("/installations/{installationId:guid}/runs", async (
            Guid installationId,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await installationService.ListRunsAsync(installationId, cancellationToken)); }
            catch (AgentInstallationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        });

        group.MapGet("/installations/{installationId:guid}/build-log", async (
            Guid installationId,
            IAgentInstallationService installationService,
            CancellationToken cancellationToken) =>
        {
            var log = await installationService.GetBuildLogAsync(installationId, cancellationToken);
            return log is null ? Results.NotFound() : Results.Ok(log);
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

    private static async Task<IResult> ExecuteInstallationActionAsync(
        Func<Task<AgentInstallationResponse>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (AgentInstallationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
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
