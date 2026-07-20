using CSweet.Api.Auth;
using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class HiringEndpoints
{
    public static IEndpointRouteBuilder MapHiringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/organizations/{organizationId:guid}/hiring");

        group.MapGet("", async (Guid organizationId, IHiringService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDashboardAsync(organizationId, cancellationToken)));

        group.MapPost("/workflows/{workflowId:guid}/confirm", async (Guid organizationId, Guid workflowId,
            ConfirmHiringWorkflowRequest request, HttpContext http, IHiringService service,
            CancellationToken cancellationToken) =>
        {
            var applicationUserId = http.User.GetApplicationUserId();
            if (!applicationUserId.HasValue) return Results.Forbid();
            try
            {
                var result = await service.ConfirmWorkflowAsync(organizationId, workflowId,
                    applicationUserId.Value, request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (UnauthorizedAccessException exception)
            {
                return Results.Json(new { error = "owner_required", message = exception.Message },
                    statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = "approval_invalidated", message = exception.Message });
            }
        });
        return endpoints;
    }
}
