using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class WorkerService : IWorkerService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public WorkerService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyList<WorkerResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CoreWorkers
            .Where(x => x.OrganizationId == organizationId || x.OrganizationId == null)
            .OrderBy(x => x.Name)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkerResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var worker = await _dbContext.CoreWorkers
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return worker?.ToResponse();
    }

    public async Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateWorkerRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.CoreOrganizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
        {
            return Failure("organization_not_found", "Organization was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Failure("validation_error", "Worker name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var worker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Description = request.Description ?? string.Empty,
            WorkerType = (WorkerType)request.WorkerType,
            ExecutionMode = (WorkerExecutionMode)request.ExecutionMode,
            CapabilitiesJson = request.CapabilitiesJson ?? "[]",
            CostModelJson = request.CostModelJson,
            EndpointConfigurationJson = request.EndpointConfigurationJson,
            IsEnabled = request.IsEnabled,
            RequiresHumanApproval = request.RequiresHumanApproval,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.CoreWorkers.Add(worker);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "worker.created",
            "Worker",
            worker.Id,
            $"Worker '{worker.Name}' created.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Worker created successfully.", Worker: worker.ToResponse());
    }

    public async Task<CoreActionResponse> UpdateAsync(Guid id, UpdateWorkerRequest request, CancellationToken cancellationToken = default)
    {
        var worker = await _dbContext.CoreWorkers
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (worker is null)
        {
            return Failure("not_found", "Worker was not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            worker.Name = request.Name.Trim();
        if (!string.IsNullOrEmpty(request.Description))
            worker.Description = request.Description;
        if (request.WorkerType.HasValue)
            worker.WorkerType = (WorkerType)request.WorkerType.Value;
        if (request.ExecutionMode.HasValue)
            worker.ExecutionMode = (WorkerExecutionMode)request.ExecutionMode.Value;
        if (!string.IsNullOrEmpty(request.CapabilitiesJson))
            worker.CapabilitiesJson = request.CapabilitiesJson;
        if (request.CostModelJson is not null)
            worker.CostModelJson = request.CostModelJson;
        if (request.EndpointConfigurationJson is not null)
            worker.EndpointConfigurationJson = request.EndpointConfigurationJson;
        if (request.IsEnabled.HasValue)
            worker.IsEnabled = request.IsEnabled.Value;
        if (request.RequiresHumanApproval.HasValue)
            worker.RequiresHumanApproval = request.RequiresHumanApproval.Value;

        worker.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "worker.updated",
            "Worker",
            worker.Id,
            $"Worker '{worker.Name}' updated.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Worker updated successfully.", Worker: worker.ToResponse());
    }

    public async Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var worker = await _dbContext.CoreWorkers
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (worker is null)
        {
            return Failure("not_found", "Worker was not found.");
        }

        var name = worker.Name;
        _dbContext.CoreWorkers.Remove(worker);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "worker.deleted",
            "Worker",
            worker.Id,
            $"Worker '{name}' deleted.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Worker deleted successfully.");
    }

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
