using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using CSweet.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public class SetupServiceTests
{
    [Fact]
    public async Task CreatingInitialSetupState_ReturnsIncompleteSetup()
    {
        await using var dbContext = CreateDbContext();
        var service = new SetupService(dbContext);

        var status = await service.GetStatusAsync();

        Assert.False(status.IsFirstRunComplete);
        Assert.Null(status.DefaultChatProviderId);
        Assert.Contains(status.Steps, x => x.Key == "llm-provider" && !x.IsComplete);
    }

    [Fact]
    public async Task CompletingRequiredStep_SetsCompleteAndCompletedAt()
    {
        await using var dbContext = CreateDbContext();
        var service = new SetupService(dbContext);

        var result = await service.CompleteStepAsync("llm-provider");

        var step = await dbContext.OnboardingSteps.SingleAsync(x => x.Key == "llm-provider");
        Assert.True(result.Succeeded);
        Assert.True(step.IsComplete);
        Assert.NotNull(step.CompletedAt);
    }

    [Fact]
    public async Task FinishSetup_FailsWhenNoDefaultChatProviderExists()
    {
        await using var dbContext = CreateDbContext();
        var service = new SetupService(dbContext);
        await service.EnsureSeededAsync();

        dbContext.LlmProviderProfiles.Add(CreateEnabledProvider());
        await CompletePriorRequiredStepsAsync(dbContext);

        var result = await service.CompleteFirstRunAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("default_chat_provider_required", result.ErrorCode);
    }

    [Fact]
    public async Task FinishSetup_FailsWhenNoSuccessfulChatTestExists()
    {
        await using var dbContext = CreateDbContext();
        var service = new SetupService(dbContext);
        await service.EnsureSeededAsync();

        var provider = CreateEnabledProvider();
        dbContext.LlmProviderProfiles.Add(provider);
        var configuration = await dbContext.SystemConfigurations.SingleAsync();
        configuration.DefaultChatProviderId = provider.Id;
        await CompletePriorRequiredStepsAsync(dbContext);

        var result = await service.CompleteFirstRunAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("successful_chat_test_required", result.ErrorCode);
    }

    [Fact]
    public async Task FinishSetup_SucceedsWhenPrerequisitesExistAndWritesAuditEvent()
    {
        await using var dbContext = CreateDbContext();
        var service = new SetupService(dbContext);
        await service.EnsureSeededAsync();

        var provider = CreateEnabledProvider();
        dbContext.LlmProviderProfiles.Add(provider);
        dbContext.ModelCapabilityTests.Add(new ModelCapabilityTest
        {
            Id = Guid.NewGuid(),
            ProviderProfileId = provider.Id,
            ConnectionSucceeded = true,
            ChatSucceeded = true,
            TestedAt = DateTimeOffset.UtcNow
        });

        var configuration = await dbContext.SystemConfigurations.SingleAsync();
        configuration.DefaultChatProviderId = provider.Id;
        await CompletePriorRequiredStepsAsync(dbContext);

        var result = await service.CompleteFirstRunAsync();

        Assert.True(result.Succeeded);
        Assert.True((await dbContext.SystemConfigurations.SingleAsync()).IsFirstRunComplete);
        Assert.Contains(await dbContext.AuditEvents.ToListAsync(), x => x.EventType == "setup.first_run.completed");
    }

    private static CSweetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CSweetDbContext(options);
    }

    private static LlmProviderProfile CreateEnabledProvider()
    {
        return new LlmProviderProfile
        {
            Id = Guid.NewGuid(),
            Name = "Local LM Studio",
            ProviderType = LlmProviderType.LmStudio,
            BaseUrl = "http://localhost:1234/v1",
            DefaultChatModel = "local-model",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task CompletePriorRequiredStepsAsync(CSweetDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var steps = await dbContext.OnboardingSteps
            .Where(x => x.IsRequired && x.Key != "finish")
            .ToListAsync();

        foreach (var step in steps)
        {
            step.IsComplete = true;
            step.CompletedAt = now;
            step.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync();
    }
}
