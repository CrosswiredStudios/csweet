using CSweet.Application.Planning;
using CSweet.Contracts.Planning;

namespace CSweet.Api.Planning;

public static class PlanningWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapPlanningWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/planning-workflows");

        group.MapGet("", async (IPlanningWorkflowService service, CancellationToken cancellationToken) =>
        {
            await service.EnsureSeededAsync(cancellationToken);
            return Results.Ok(await service.ListAsync(cancellationToken));
        });

        group.MapGet("/{key}", async (string key, IPlanningWorkflowService service, CancellationToken cancellationToken) =>
        {
            await service.EnsureSeededAsync(cancellationToken);
            var workflow = await service.GetAsync(key, cancellationToken);
            return workflow is null ? Results.NotFound() : Results.Ok(workflow);
        });

        return endpoints;
    }
}
