using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class TaskRunService : ITaskRunService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public TaskRunService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyList<TaskRunResponse>> ListByTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CoreTaskRuns
            .Where(x => x.TaskId == taskId)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskRunResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.CoreTaskRuns
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return run?.ToResponse();
    }

    public async Task<CoreActionResponse> CreateAsync(Guid taskId, CreateTaskRunRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.CoreWorkTasks
            .SingleOrDefaultAsync(x => x.Id == taskId, cancellationToken);

        if (task is null)
        {
            return Failure("task_not_found", "Task was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var run = new TaskRun
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            WorkerId = request.WorkerId,
            Status = TaskRunStatus.Pending,
            StartedAt = now,
            InputJson = request.InputJson,
            OutputJson = null,
            FailureMessage = null,
            CostAmount = null,
            CostCurrency = null
        };

        _dbContext.CoreTaskRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "task_run.created",
            "TaskRun",
            run.Id,
            $"Task run created for task {taskId}.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Task run created successfully.", TaskRun: run.ToResponse());
    }

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
