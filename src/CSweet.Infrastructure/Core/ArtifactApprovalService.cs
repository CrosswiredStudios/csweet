using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class ArtifactApprovalService : IArtifactApprovalService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public ArtifactApprovalService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyList<ApprovalResponse>> ListByArtifactAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CoreApprovals
            .Where(x => x.ArtifactId == artifactId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<CoreActionResponse> ApproveAsync(Guid artifactId, string? comment = null, CancellationToken cancellationToken = default)
    {
        var artifact = await _dbContext.CoreArtifacts
            .SingleOrDefaultAsync(x => x.Id == artifactId, cancellationToken);

        if (artifact is null)
        {
            return Failure("not_found", "Artifact was not found.");
        }

        // Check if artifact is already approved - cannot re-approve without revision
        if (artifact.ApprovalStatus == ApprovalStatus.Approved)
        {
            return Failure("approval_conflict", "Artifact is already approved.");
        }

        var now = DateTimeOffset.UtcNow;
        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            ArtifactId = artifactId,
            Status = ApprovalStatus.Approved,
            Comment = comment,
            DecidedAt = now,
            CreatedAt = now
        };

        artifact.ApprovalStatus = ApprovalStatus.Approved;
        artifact.UpdatedAt = now;

        _dbContext.CoreApprovals.Add(approval);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "artifact.approved",
            "Approval",
            approval.Id,
            $"Artifact '{artifact.Title}' approved.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Artifact approved successfully.", Approval: approval.ToResponse());
    }

    public async Task<CoreActionResponse> RejectAsync(Guid artifactId, string? comment = null, CancellationToken cancellationToken = default)
    {
        var artifact = await _dbContext.CoreArtifacts
            .SingleOrDefaultAsync(x => x.Id == artifactId, cancellationToken);

        if (artifact is null)
        {
            return Failure("not_found", "Artifact was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            ArtifactId = artifactId,
            Status = ApprovalStatus.Rejected,
            Comment = comment,
            DecidedAt = now,
            CreatedAt = now
        };

        artifact.ApprovalStatus = ApprovalStatus.Rejected;
        artifact.UpdatedAt = now;

        _dbContext.CoreApprovals.Add(approval);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "artifact.rejected",
            "Approval",
            approval.Id,
            $"Artifact '{artifact.Title}' rejected.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Artifact rejected successfully.", Approval: approval.ToResponse());
    }

    public async Task<CoreActionResponse> RequestRevisionAsync(Guid artifactId, string? comment = null, CancellationToken cancellationToken = default)
    {
        var artifact = await _dbContext.CoreArtifacts
            .SingleOrDefaultAsync(x => x.Id == artifactId, cancellationToken);

        if (artifact is null)
        {
            return Failure("not_found", "Artifact was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            ArtifactId = artifactId,
            Status = ApprovalStatus.RevisionRequested,
            Comment = comment,
            DecidedAt = now,
            CreatedAt = now
        };

        artifact.ApprovalStatus = ApprovalStatus.RevisionRequested;
        artifact.UpdatedAt = now;

        _dbContext.CoreApprovals.Add(approval);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "artifact.revision_requested",
            "Approval",
            approval.Id,
            $"Artifact '{artifact.Title}' revision requested.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Revision requested successfully.", Approval: approval.ToResponse());
    }

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
