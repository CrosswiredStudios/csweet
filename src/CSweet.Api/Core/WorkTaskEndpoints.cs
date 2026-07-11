using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class WorkTaskEndpoints
{
    public static IEndpointRouteBuilder MapWorkTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/tasks");
        var phaseOrganizationGroup = endpoints.MapGroup("/api/organizations/{organizationId:guid}/tasks");
        var phaseTaskGroup = endpoints.MapGroup("/api/tasks");

        group.MapGet("/organization/{organizationId:guid}", async (Guid organizationId, IWorkTaskService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));
        phaseOrganizationGroup.MapGet("", async (Guid organizationId, IWorkTaskService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IWorkTaskService service, CancellationToken cancellationToken) =>
        {
            var task = await service.GetAsync(id, cancellationToken);
            return task is null ? Results.NotFound() : Results.Ok(task);
        });
        phaseTaskGroup.MapGet("/{id:guid}", async (Guid id, IWorkTaskService service, CancellationToken cancellationToken) =>
        {
            var task = await service.GetAsync(id, cancellationToken);
            return task is null ? Results.NotFound() : Results.Ok(task);
        });

        group.MapPost("/organization/{organizationId:guid}", async (Guid organizationId, CreateWorkTaskRequest request, IWorkTaskService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/tasks/{result.WorkTask!.Id}", result.WorkTask)
                : Results.BadRequest(result);
        });
        phaseOrganizationGroup.MapPost("", async (Guid organizationId, CreateWorkTaskRequest request, IWorkTaskService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/tasks/{result.WorkTask!.Id}", result.WorkTask)
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkTaskRequest request, IWorkTaskService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.WorkTask)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IWorkTaskService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
