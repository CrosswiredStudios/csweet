using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class ArtifactEndpoints
{
    public static IEndpointRouteBuilder MapArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/artifacts");
        var phaseGroup = endpoints.MapGroup("/api/artifacts");

        group.MapGet("/organization/{organizationId:guid}", async (Guid organizationId, IArtifactService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByOrganizationAsync(organizationId, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IArtifactService service, CancellationToken cancellationToken) =>
        {
            var artifact = await service.GetAsync(id, cancellationToken);
            return artifact is null ? Results.NotFound() : Results.Ok(artifact);
        });
        phaseGroup.MapGet("/{id:guid}", async (Guid id, IArtifactService service, CancellationToken cancellationToken) =>
        {
            var artifact = await service.GetAsync(id, cancellationToken);
            return artifact is null ? Results.NotFound() : Results.Ok(artifact);
        });

        group.MapPost("/organization/{organizationId:guid}", async (Guid organizationId, CreateArtifactRequest request, IArtifactService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/artifacts/{result.Artifact!.Id}", result.Artifact)
                : Results.BadRequest(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateArtifactRequest request, IArtifactService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Artifact)
                : Results.BadRequest(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IArtifactService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.BadRequest(result);
        });

        // Approval endpoints
        group.MapGet("/{artifactId:guid}/approvals", async (Guid artifactId, IArtifactApprovalService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByArtifactAsync(artifactId, cancellationToken)));

        group.MapPost("/{artifactId:guid}/approve", async (Guid artifactId, CreateApprovalRequest request, IArtifactApprovalService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(artifactId, request.Comment, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Approval)
                : Results.BadRequest(result);
        });
        phaseGroup.MapPost("/{artifactId:guid}/approve", async (Guid artifactId, CreateApprovalRequest request, IArtifactApprovalService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(artifactId, request.Comment, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Approval)
                : Results.BadRequest(result);
        });

        group.MapPost("/{artifactId:guid}/reject", async (Guid artifactId, CreateApprovalRequest request, IArtifactApprovalService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(artifactId, request.Comment, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Approval)
                : Results.BadRequest(result);
        });
        phaseGroup.MapPost("/{artifactId:guid}/reject", async (Guid artifactId, CreateApprovalRequest request, IArtifactApprovalService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(artifactId, request.Comment, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Approval)
                : Results.BadRequest(result);
        });

        group.MapPost("/{artifactId:guid}/request-revision", async (Guid artifactId, CreateApprovalRequest request, IArtifactApprovalService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestRevisionAsync(artifactId, request.Comment, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Approval)
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
