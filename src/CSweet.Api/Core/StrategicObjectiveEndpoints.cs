using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class StrategicObjectiveEndpoints
{
    public static IEndpointRouteBuilder MapStrategicObjectiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/objectives");

        group.MapGet("/organization/{organizationId:guid}", async (Guid organizationId, IStrategicObjectiveService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IStrategicObjectiveService service, CancellationToken cancellationToken) =>
        {
            var obj = await service.GetAsync(id, cancellationToken);
            return obj is null ? Results.NotFound() : Results.Ok(obj);
        });

        group.MapPost("/organization/{organizationId:guid}", async (Guid organizationId, CreateStrategicObjectiveRequest request, IStrategicObjectiveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/objectives/{result.StrategicObjective!.Id}", result.StrategicObjective)
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateStrategicObjectiveRequest request, IStrategicObjectiveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.StrategicObjective)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IStrategicObjectiveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
