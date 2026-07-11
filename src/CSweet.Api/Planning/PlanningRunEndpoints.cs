using CSweet.Application.Planning;
using CSweet.Contracts.Planning;

namespace CSweet.Api.Planning;

public static class PlanningRunEndpoints
{
    public static IEndpointRouteBuilder MapPlanningRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organizations/{organizationId:guid}/planning-runs");

        group.MapPost("", async (
            Guid organizationId,
            StartPlanningRunRequest request,
            IPlanningRunService service,
            CancellationToken cancellationToken) =>
        {
            // Ensure the organizationId matches
            if (request.OrganizationId != organizationId)
                return Results.BadRequest(new PlanningActionResponse(false, "mismatch", "Organization ID mismatch."));

            var result = await service.StartAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.PlanningRun)
                : Results.BadRequest(result);
        });

        group.MapGet("/{workflowKey}", async (
            Guid organizationId,
            string workflowKey,
            IPlanningRunService service,
            CancellationToken cancellationToken) =>
        {
            var status = await service.GetStatusAsync(organizationId, workflowKey, cancellationToken);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        group.MapPost("/{workflowKey}/run-next", async (
            Guid organizationId,
            string workflowKey,
            IPlanningRunService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RunNextTaskAsync(organizationId, workflowKey, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        group.MapPost("/{workflowKey}/cancel", async (
            Guid organizationId,
            string workflowKey,
            IPlanningRunService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAsync(organizationId, workflowKey, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        group.MapPost("/{workflowKey}/reset", async (
            Guid organizationId,
            string workflowKey,
            IPlanningRunService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ResetAsync(organizationId, workflowKey, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
