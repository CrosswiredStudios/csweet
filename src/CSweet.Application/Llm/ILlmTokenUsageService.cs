using CSweet.Contracts.Llm;

namespace CSweet.Application.Llm;

public interface ILlmTokenUsageService
{
    Task<LlmTokenUsageSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
}
