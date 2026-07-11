using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/roles");
        var phaseGroup = endpoints.MapGroup("/api/organizations/{organizationId:guid}/roles");

        group.MapGet("/organization/{organizationId:guid}", async (Guid organizationId, IRoleService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));
        phaseGroup.MapGet("", async (Guid organizationId, IRoleService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IRoleService service, CancellationToken cancellationToken) =>
        {
            var role = await service.GetAsync(id, cancellationToken);
            return role is null ? Results.NotFound() : Results.Ok(role);
        });

        group.MapPost("/organization/{organizationId:guid}", async (Guid organizationId, CreateRoleRequest request, IRoleService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/roles/{result.Role!.Id}", result.Role)
                : Results.BadRequest(result);
        });
        phaseGroup.MapPost("", async (Guid organizationId, CreateRoleRequest request, IRoleService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/organizations/{organizationId}/roles/{result.Role!.Id}", result.Role)
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest request, IRoleService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Role)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IRoleService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
