using CSweet.Application.Planning;
using CSweet.Contracts.Planning;

namespace CSweet.Api.Planning;

public static class PlanningDocumentEndpoints
{
    public static IEndpointRouteBuilder MapPlanningDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organizations/{organizationId:guid}/documents");

        group.MapGet("", async (
            Guid organizationId,
            IPlanningDocumentService service,
            CancellationToken cancellationToken) =>
        {
            var docs = await service.ListByOrganizationAsync(organizationId, cancellationToken);
            return Results.Ok(docs);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            IPlanningDocumentService service,
            CancellationToken cancellationToken) =>
        {
            var doc = await service.GetAsync(id, cancellationToken);
            return doc is null ? Results.NotFound() : Results.Ok(doc);
        });

        group.MapGet("/latest/{documentType}", async (
            Guid organizationId,
            string documentType,
            IPlanningDocumentService service,
            CancellationToken cancellationToken) =>
        {
            var doc = await service.GetLatestByTypeAsync(organizationId, documentType, cancellationToken);
            return doc is null ? Results.NotFound() : Results.Ok(doc);
        });

        group.MapPost("/generate", async (
            Guid organizationId,
            GeneratePlanningDocumentRequest request,
            IPlanningDocumentService service,
            CancellationToken cancellationToken) =>
        {
            if (request.OrganizationId != organizationId)
                return Results.BadRequest(new PlanningActionResponse(false, "mismatch", "Organization ID mismatch."));

            var result = await service.GenerateAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Document)
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}/content", async (
            Guid id,
            string content,
            IPlanningDocumentService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateContentAsync(id, content, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Document)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IPlanningDocumentService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
