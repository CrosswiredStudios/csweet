using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class ArtifactService : IArtifactService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public ArtifactService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyList<ArtifactResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CoreArtifacts
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<ArtifactResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var artifact = await _dbContext.CoreArtifacts
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return artifact?.ToResponse();
    }

    public async Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateArtifactRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.CoreOrganizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
        {
            return Failure("organization_not_found", "Organization was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Failure("validation_error", "Artifact title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Failure("validation_error", "Artifact content is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            TaskId = request.TaskId,
            TaskRunId = request.TaskRunId,
            Type = (ArtifactType)request.Type,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Version = request.Version > 0 ? request.Version : 1,
            ApprovalStatus = (ApprovalStatus)request.ApprovalStatus,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.CoreArtifacts.Add(artifact);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "artifact.created",
            "Artifact",
            artifact.Id,
            $"Artifact '{artifact.Title}' created.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Artifact created successfully.", Artifact: artifact.ToResponse());
    }

    public async Task<CoreActionResponse> UpdateAsync(Guid id, UpdateArtifactRequest request, CancellationToken cancellationToken = default)
    {
        var artifact = await _dbContext.CoreArtifacts
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (artifact is null)
        {
            return Failure("not_found", "Artifact was not found.");
        }

        // Prevent updating approved artifacts without creating a new version
        if (artifact.ApprovalStatus == ApprovalStatus.Approved)
        {
            return Failure("approval_conflict", "Approved artifact cannot be updated. Create a new version instead.");
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            artifact.Title = request.Title.Trim();
        if (!string.IsNullOrEmpty(request.Content))
            artifact.Content = request.Content.Trim();
        if (request.Version.HasValue && request.Version.Value > 0)
            artifact.Version = request.Version.Value;
        if (request.ApprovalStatus.HasValue)
            artifact.ApprovalStatus = (ApprovalStatus)request.ApprovalStatus.Value;

        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "artifact.updated",
            "Artifact",
            artifact.Id,
            $"Artifact '{artifact.Title}' updated.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Artifact updated successfully.", Artifact: artifact.ToResponse());
    }

    public async Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var artifact = await _dbContext.CoreArtifacts
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (artifact is null)
        {
            return Failure("not_found", "Artifact was not found.");
        }

        var title = artifact.Title;
        _dbContext.CoreArtifacts.Remove(artifact);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "artifact.deleted",
            "Artifact",
            artifact.Id,
            $"Artifact '{title}' deleted.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Artifact deleted successfully.");
    }

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
