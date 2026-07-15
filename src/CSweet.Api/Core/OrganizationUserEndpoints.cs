using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class OrganizationUserEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/organizations/{organizationId:guid}/users");

        group.MapGet("", async (Guid organizationId, IOrganizationUserService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IOrganizationUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.GetAsync(id, cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapPost("", async (Guid organizationId, CreateOrganizationUserRequest request, IOrganizationUserService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/organization-users/{result.OrganizationUser!.Id}", result.OrganizationUser)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IOrganizationUserService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}/role", async (Guid organizationId, Guid id, UpdateOrganizationUserRoleRequest request, IOrganizationUserService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRoleAsync(organizationId, id, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.OrganizationUser)
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
