using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class WorkerEndpoints
{
    public static IEndpointRouteBuilder MapWorkerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/workers");
        var phaseGroup = endpoints.MapGroup("/api/organizations/{organizationId:guid}/workers");

        group.MapGet("/organization/{organizationId:guid}", async (Guid organizationId, IWorkerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));
        phaseGroup.MapGet("", async (Guid organizationId, IWorkerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IWorkerService service, CancellationToken cancellationToken) =>
        {
            var worker = await service.GetAsync(id, cancellationToken);
            return worker is null ? Results.NotFound() : Results.Ok(worker);
        });

        group.MapPost("/organization/{organizationId:guid}", async (Guid organizationId, CreateWorkerRequest request, IWorkerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/workers/{result.Worker!.Id}", result.Worker)
                : Results.BadRequest(result);
        });
        phaseGroup.MapPost("", async (Guid organizationId, CreateWorkerRequest request, IWorkerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/organizations/{organizationId}/workers/{result.Worker!.Id}", result.Worker)
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkerRequest request, IWorkerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Worker)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IWorkerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
