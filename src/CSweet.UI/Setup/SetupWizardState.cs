using CSweet.Contracts.Llm;
using CSweet.Contracts.Setup;
using CSweet.Domain.Setup;

namespace CSweet.UI.Setup;

public static class SetupWizardState
{
    public static string FirstIncompleteStepKey(
        IReadOnlyList<string> orderedStepKeys,
        SetupStatusResponse status)
    {
        return orderedStepKeys.FirstOrDefault(key =>
            !status.Steps.Any(step => step.Key == key && step.IsComplete)) ?? "finish";
    }

    public static bool CanFinish(SetupStatusResponse status)
    {
        return status.DefaultChatProviderId is not null &&
            status.Steps
                .Where(step => step.IsRequired && step.Key != "finish")
                .All(step => step.IsComplete);
    }

    public static LlmProviderSetupDefaults LmStudioDefaults()
    {
        return new LlmProviderSetupDefaults(
            LlmProviderType.LmStudio,
            "Local LM Studio",
            "http://localhost:1234/v1",
            "lm-studio",
            SupportsStreaming: true,
            SupportsToolCalling: false,
            SupportsStructuredOutput: false,
            SupportsVision: false);
    }

    public static string ProviderTestMessage(ModelCapabilityTestResult result)
    {
        if (result.ConnectionSucceeded && result.ChatSucceeded)
        {
            return "Provider test succeeded and default chat provider was selected.";
        }

        return string.IsNullOrWhiteSpace(result.FailureMessage)
            ? "Required provider checks did not pass."
            : result.FailureMessage;
    }
}

public sealed record LlmProviderSetupDefaults(
    LlmProviderType ProviderType,
    string ProviderName,
    string BaseUrl,
    string ApiKey,
    bool SupportsStreaming,
    bool SupportsToolCalling,
    bool SupportsStructuredOutput,
    bool SupportsVision);
