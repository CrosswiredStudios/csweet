using CSweet.Application.Llm;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Llm;

public sealed class LlmTokenUsageService : ILlmTokenUsageService
{
    private const string Last30DaysLabel = "Last 30 days";

    private readonly CSweetDbContext _dbContext;

    public LlmTokenUsageService(CSweetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LlmTokenUsageSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var since30Days = now.AddDays(-30);

        var logs = await _dbContext.AgentRunLogs
            .AsNoTracking()
            .Where(log => log.StartedAt >= since30Days)
            .ToListAsync(cancellationToken);

        var providerNames = await _dbContext.LlmProviderProfiles
            .AsNoTracking()
            .ToDictionaryAsync(profile => profile.Id, profile => profile.Name, cancellationToken);

        return new LlmTokenUsageSummaryResponse(
            now,
            BuildWindow("Last 24 hours", logs.Where(log => log.StartedAt >= now.AddHours(-24))),
            BuildWindow("Last 7 days", logs.Where(log => log.StartedAt >= now.AddDays(-7))),
            BuildWindow(Last30DaysLabel, logs),
            BuildProviderBreakdown(logs, providerNames),
            BuildAgentBreakdown(logs));
    }

    private static IReadOnlyList<LlmProviderTokenUsageResponse> BuildProviderBreakdown(
        IEnumerable<AgentRunLog> logs,
        IReadOnlyDictionary<Guid, string> providerNames)
    {
        return logs
            .GroupBy(log => log.ProviderProfileId)
            .Select(group => new LlmProviderTokenUsageResponse(
                group.Key,
                providerNames.TryGetValue(group.Key, out var providerName) ? providerName : "Deleted provider",
                BuildWindow(Last30DaysLabel, group)))
            .OrderByDescending(provider => provider.Usage.TotalTokens)
            .ThenBy(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AgentTokenUsageResponse> BuildAgentBreakdown(IEnumerable<AgentRunLog> logs)
    {
        return logs
            .GroupBy(log => log.AgentKey, StringComparer.Ordinal)
            .Select(group => new AgentTokenUsageResponse(
                group.Key,
                BuildWindow(Last30DaysLabel, group)))
            .OrderByDescending(agent => agent.Usage.TotalTokens)
            .ThenBy(agent => agent.AgentKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static LlmTokenUsageWindowResponse BuildWindow(string label, IEnumerable<AgentRunLog> logs)
    {
        var items = logs.ToList();
        var inputTokens = items.Sum(log => (long)(log.TokenInputCount ?? 0));
        var outputTokens = items.Sum(log => (long)(log.TokenOutputCount ?? 0));

        return new LlmTokenUsageWindowResponse(
            label,
            items.Count,
            inputTokens,
            outputTokens,
            inputTokens + outputTokens);
    }
}
