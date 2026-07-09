using CSweet.Application.Setup;
using CSweet.Contracts.Setup;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Setup;

public sealed class SetupService : ISetupService
{
    public static readonly IReadOnlyList<(string Key, string DisplayName, bool IsRequired)> RequiredSteps =
    [
        ("welcome", "Welcome", true),
        ("deployment-mode", "Deployment Mode", true),
        ("llm-provider", "LLM Provider", true),
        ("model-capability-test", "Model Capability Test", true),
        ("storage", "Storage", true),
        ("worker-runtime", "Worker Runtime", true),
        ("admin-user", "Admin User", true),
        ("finish", "Finish", true)
    ];

    private readonly CSweetDbContext _dbContext;

    public SetupService(CSweetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (!await _dbContext.SystemConfigurations.AnyAsync(cancellationToken))
        {
            _dbContext.SystemConfigurations.Add(new SystemConfiguration
            {
                Id = Guid.NewGuid(),
                IsFirstRunComplete = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var existingSteps = await _dbContext.OnboardingSteps
            .ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var stepDefinition in RequiredSteps)
        {
            if (existingSteps.TryGetValue(stepDefinition.Key, out var existingStep))
            {
                existingStep.DisplayName = stepDefinition.DisplayName;
                existingStep.IsRequired = stepDefinition.IsRequired;
                existingStep.UpdatedAt = now;
                continue;
            }

            _dbContext.OnboardingSteps.Add(new OnboardingStep
            {
                Id = Guid.NewGuid(),
                Key = stepDefinition.Key,
                DisplayName = stepDefinition.DisplayName,
                IsRequired = stepDefinition.IsRequired,
                IsComplete = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SetupStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        var configuration = await GetConfigurationAsync(cancellationToken);
        var steps = await _dbContext.OnboardingSteps
            .OrderBy(x => x.CreatedAt)
            .Select(x => new OnboardingStepStatusDto(
                x.Key,
                x.DisplayName,
                x.IsRequired,
                x.IsComplete))
            .ToListAsync(cancellationToken);

        return new SetupStatusResponse(
            configuration.IsFirstRunComplete,
            configuration.DefaultChatProviderId,
            configuration.DefaultEmbeddingProviderId,
            steps);
    }

    public async Task<SetupActionResponse> CompleteStepAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalizedKey = RequiredSteps
            .FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            .Key;

        if (normalizedKey is null)
        {
            return await FailureAsync("unknown_step", $"Unknown setup step '{key}'.", cancellationToken);
        }

        await EnsureSeededAsync(cancellationToken);

        var step = await _dbContext.OnboardingSteps
            .SingleAsync(x => x.Key == normalizedKey, cancellationToken);

        if (step.Key == "finish" && !await ArePriorRequiredStepsCompleteAsync(cancellationToken))
        {
            return await FailureAsync("finish_prerequisites_missing", "All required setup steps must be complete before finish can be completed.", cancellationToken);
        }

        if (!step.IsComplete)
        {
            var now = DateTimeOffset.UtcNow;
            step.IsComplete = true;
            step.CompletedAt = now;
            step.UpdatedAt = now;

            _dbContext.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                EventType = "setup.step.completed",
                EntityType = nameof(OnboardingStep),
                EntityId = step.Id,
                Summary = $"Setup step completed: {step.Key}",
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await SuccessAsync(cancellationToken);
    }

    public async Task<SetupActionResponse> CompleteFirstRunAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        var configuration = await GetConfigurationAsync(cancellationToken);

        if (!await _dbContext.LlmProviderProfiles.AnyAsync(x => x.IsEnabled, cancellationToken))
        {
            return await FailureAsync("provider_profile_required", "At least one enabled provider profile is required.", cancellationToken);
        }

        if (configuration.DefaultChatProviderId is null)
        {
            return await FailureAsync("default_chat_provider_required", "A default chat provider is required.", cancellationToken);
        }

        var hasSuccessfulChatTest = await _dbContext.ModelCapabilityTests
            .AnyAsync(x => x.ProviderProfileId == configuration.DefaultChatProviderId && x.ChatSucceeded, cancellationToken);

        if (!hasSuccessfulChatTest)
        {
            return await FailureAsync("successful_chat_test_required", "A successful chat capability test is required.", cancellationToken);
        }

        var adminStepComplete = await _dbContext.OnboardingSteps
            .AnyAsync(x => x.Key == "admin-user" && x.IsComplete, cancellationToken);

        if (!adminStepComplete)
        {
            return await FailureAsync("admin_setup_required", "Admin setup must be completed before first-run setup can finish.", cancellationToken);
        }

        if (!await ArePriorRequiredStepsCompleteAsync(cancellationToken))
        {
            return await FailureAsync("required_steps_incomplete", "Required setup steps must be complete before first-run setup can finish.", cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        configuration.IsFirstRunComplete = true;
        configuration.UpdatedAt = now;

        var finishStep = await _dbContext.OnboardingSteps.SingleAsync(x => x.Key == "finish", cancellationToken);
        finishStep.IsComplete = true;
        finishStep.CompletedAt ??= now;
        finishStep.UpdatedAt = now;

        _dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            EventType = "setup.first_run.completed",
            EntityType = nameof(SystemConfiguration),
            EntityId = configuration.Id,
            Summary = "First-run setup completed.",
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await SuccessAsync(cancellationToken);
    }

    private async Task<SystemConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.SystemConfigurations
            .OrderBy(x => x.CreatedAt)
            .FirstAsync(cancellationToken);
    }

    private async Task<bool> ArePriorRequiredStepsCompleteAsync(CancellationToken cancellationToken)
    {
        return !await _dbContext.OnboardingSteps
            .AnyAsync(x => x.IsRequired && x.Key != "finish" && !x.IsComplete, cancellationToken);
    }

    private async Task<SetupActionResponse> SuccessAsync(CancellationToken cancellationToken)
    {
        return new SetupActionResponse(true, null, null, await GetStatusAsync(cancellationToken));
    }

    private async Task<SetupActionResponse> FailureAsync(string errorCode, string message, CancellationToken cancellationToken)
    {
        return new SetupActionResponse(false, errorCode, message, await GetStatusAsync(cancellationToken));
    }
}
