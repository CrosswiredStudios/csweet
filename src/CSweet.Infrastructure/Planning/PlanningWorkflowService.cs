using CSweet.Application.Planning;
using CSweet.Contracts.Planning;
using CSweet.Domain.Planning;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Planning;

public sealed class PlanningWorkflowService : IPlanningWorkflowService
{
    private readonly CSweetDbContext _dbContext;

    public static readonly IReadOnlyList<(string Key, string DisplayName, string Description)> DefaultWorkflows =
    [
        ("business-planning", "Comprehensive Business Planning", "Full business planning workflow with 10 analysis tasks covering situation analysis through executive summary."),
        ("quick-assessment", "Quick Business Assessment", "Rapid assessment with 5 core tasks for a high-level business overview."),
        ("strategic-review", "Strategic Review", "Focused strategic review covering direction, competitive positioning, and implementation."),
    ];

    public PlanningWorkflowService(CSweetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PlanningWorkflowResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<PlanningWorkflow>()
            .Where(w => w.IsEnabled)
            .OrderBy(w => w.SortOrder)
            .Select(w => w.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningWorkflowResponse?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var workflow = await _dbContext.Set<PlanningWorkflow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.Key == key, cancellationToken);

        return workflow?.ToResponse();
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var existingKeys = await _dbContext.Set<PlanningWorkflow>()
            .Select(w => w.Key)
            .ToHashSetAsync(cancellationToken);

        foreach (var (key, displayName, description) in DefaultWorkflows)
        {
            if (existingKeys.Contains(key))
                continue;

            _dbContext.Set<PlanningWorkflow>().Add(new PlanningWorkflow
            {
                Id = Guid.NewGuid(),
                Key = key,
                DisplayName = displayName,
                Description = description,
                IsEnabled = true,
                SortOrder = DefaultWorkflows.ToList().FindIndex(x => x.Key == key),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
