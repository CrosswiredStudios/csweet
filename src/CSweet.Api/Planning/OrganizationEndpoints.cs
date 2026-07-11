using CSweet.Application.Planning;
using CSweet.Contracts.Planning;

namespace CSweet.Api.Planning;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/planning/organizations");

        group.MapGet("", async (IOrganizationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var org = await service.GetAsync(id, cancellationToken);
            return org is null ? Results.NotFound() : Results.Ok(org);
        });

        group.MapPost("", async (CreateOrganizationRequest request, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/planning/organizations/{result.Organization!.Id}", result.Organization)
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateOrganizationRequest request, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Organization)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IOrganizationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
