using CSweet.Contracts.Llm;

namespace CSweet.AI.Providers;

public interface ILlmConnectionTester
{
    Task<ModelCapabilityTestResult> TestAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);
}
