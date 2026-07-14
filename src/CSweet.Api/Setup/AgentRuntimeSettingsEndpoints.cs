using CSweet.Application.Setup;
using CSweet.Contracts.Setup;

namespace CSweet.Api.Setup;

public static class AgentRuntimeSettingsEndpoints
{
    public static IEndpointRouteBuilder MapAgentRuntimeSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/agent-runtime/settings");

        group.MapGet("", async (
            IAgentRuntimeSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var settings = await settingsService.GetAsync(cancellationToken);
            return Results.Ok(settings);
        });

        group.MapPut("", async (
            UpdateAgentRuntimeSettingsRequest request,
            IAgentRuntimeSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var result = await settingsService.UpdateAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
