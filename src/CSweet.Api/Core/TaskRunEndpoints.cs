using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class TaskRunEndpoints
{
    public static IEndpointRouteBuilder MapTaskRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/task-runs");
        var phaseTaskGroup = endpoints.MapGroup("/api/tasks/{taskId:guid}/runs");

        group.MapGet("/task/{taskId:guid}", async (Guid taskId, ITaskRunService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByTaskAsync(taskId, cancellationToken)));
        phaseTaskGroup.MapGet("", async (Guid taskId, ITaskRunService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByTaskAsync(taskId, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, ITaskRunService service, CancellationToken cancellationToken) =>
        {
            var run = await service.GetAsync(id, cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        group.MapPost("/task/{taskId:guid}", async (Guid taskId, CreateTaskRunRequest request, ITaskRunService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(taskId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/task-runs/{result.TaskRun!.Id}", result.TaskRun)
                : Results.BadRequest(result);
        });
        phaseTaskGroup.MapPost("", async (Guid taskId, CreateTaskRunRequest request, ITaskRunService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(taskId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/tasks/{taskId}/runs/{result.TaskRun!.Id}", result.TaskRun)
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
