using CSweet.UI.Setup;
using CSweet.Contracts.Llm;
using CSweet.Contracts.Setup;
using CSweet.Domain.Setup;

namespace CSweet.UnitTests;

public class SetupWizardStateTests
{
    [Fact]
    public void WizardShowsFirstIncompleteStep()
    {
        var status = Status(
            defaultChatProviderId: null,
            ("welcome", true),
            ("llm-provider", false));

        var step = SetupWizardState.FirstIncompleteStepKey(
            ["welcome", "llm-provider"],
            status);

        Assert.Equal("llm-provider", step);
    }

    [Fact]
    public void LmStudioPresetPopulatesBaseUrl()
    {
        var defaults = SetupWizardState.LmStudioDefaults();

        Assert.Equal(LlmProviderType.LmStudio, defaults.ProviderType);
        Assert.Equal("Local LM Studio", defaults.ProviderName);
        Assert.Equal("http://localhost:1234/v1", defaults.BaseUrl);
        Assert.Equal("lm-studio", defaults.ApiKey);
    }

    [Fact]
    public void FinishIsDisabledUntilRequiredStepsAndDefaultProviderExist()
    {
        var incomplete = Status(
            defaultChatProviderId: Guid.NewGuid(),
            ("welcome", true),
            ("llm-provider", false));

        var noProvider = Status(
            defaultChatProviderId: null,
            ("welcome", true),
            ("llm-provider", true));

        var complete = Status(
            defaultChatProviderId: Guid.NewGuid(),
            ("welcome", true),
            ("llm-provider", true));

        Assert.False(SetupWizardState.CanFinish(incomplete));
        Assert.False(SetupWizardState.CanFinish(noProvider));
        Assert.True(SetupWizardState.CanFinish(complete));
    }

    [Fact]
    public void ProviderTestErrorAppearsClearly()
    {
        var result = new ModelCapabilityTestResult(
            Guid.NewGuid(),
            ConnectionSucceeded: false,
            ChatSucceeded: false,
            StreamingSucceeded: false,
            StructuredOutputSucceeded: false,
            ToolCallingSucceeded: false,
            FailureMessage: "Provider unreachable.");

        Assert.Equal("Provider unreachable.", SetupWizardState.ProviderTestMessage(result));
    }

    private static SetupStatusResponse Status(Guid? defaultChatProviderId, params (string Key, bool Complete)[] steps)
    {
        return new SetupStatusResponse(
            IsFirstRunComplete: false,
            DefaultChatProviderId: defaultChatProviderId,
            DefaultEmbeddingProviderId: null,
            Steps: steps
                .Select(step => new OnboardingStepStatusDto(step.Key, step.Key, IsRequired: true, step.Complete))
                .ToList());
    }
}
