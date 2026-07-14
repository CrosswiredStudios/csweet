using CSweet.Domain.Core;
using CSweet.Domain.Planning;
using CSweet.Domain.Setup;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Persistence;

public sealed class CSweetDbContext : DbContext
{
    public CSweetDbContext(DbContextOptions<CSweetDbContext> options)
        : base(options)
    {
    }

    // Setup entities
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<LlmProviderProfile> LlmProviderProfiles => Set<LlmProviderProfile>();
    public DbSet<ModelCapabilityTest> ModelCapabilityTests => Set<ModelCapabilityTest>();
    public DbSet<OnboardingStep> OnboardingSteps => Set<OnboardingStep>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AgentRunLog> AgentRunLogs => Set<AgentRunLog>();
    public DbSet<AgentRuntimeGlobalSettings> AgentRuntimeGlobalSettings => Set<AgentRuntimeGlobalSettings>();
    public DbSet<AgentPackageSource> AgentPackageSources => Set<AgentPackageSource>();
    public DbSet<AgentPackageVersion> AgentPackageVersions => Set<AgentPackageVersion>();

    // Planning entities
    public DbSet<PlanningTask> PlanningTasks => Set<PlanningTask>();
    public DbSet<PlanningDocument> PlanningDocuments => Set<PlanningDocument>();
    public DbSet<PlanningWorkflow> PlanningWorkflows => Set<PlanningWorkflow>();

    // Core business domain entities
    public DbSet<Domain.Core.Organization> CoreOrganizations => Set<Domain.Core.Organization>();
    public DbSet<OrganizationUser> CoreOrganizationUsers => Set<OrganizationUser>();
    public DbSet<Role> CoreRoles => Set<Role>();
    public DbSet<StrategicObjective> CoreStrategicObjectives => Set<StrategicObjective>();
    public DbSet<Worker> CoreWorkers => Set<Worker>();
    public DbSet<WorkTask> CoreWorkTasks => Set<WorkTask>();
    public DbSet<TaskRun> CoreTaskRuns => Set<TaskRun>();
    public DbSet<Artifact> CoreArtifacts => Set<Artifact>();
    public DbSet<Approval> CoreApprovals => Set<Approval>();

    // Conversation entities
    public DbSet<Conversation> CoreConversations => Set<Conversation>();
    public DbSet<ConversationMessage> CoreConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply planning entity configurations
        PlanningConfigurations.Apply(modelBuilder);
        
        // Apply core business domain entity configurations
        CoreConfigurations.Apply(modelBuilder);
        modelBuilder.Entity<SystemConfiguration>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<LlmProviderProfile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ProviderType).HasConversion<string>().HasMaxLength(64).IsRequired();
            entity.Property(x => x.BaseUrl).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.ApiKeySecretName).HasMaxLength(256);
            entity.Property(x => x.DefaultChatModel).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DefaultEmbeddingModel).HasMaxLength(256);
        });

        modelBuilder.Entity<ModelCapabilityTest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FailureMessage).HasMaxLength(2048);
            entity.HasOne(x => x.ProviderProfile)
                .WithMany()
                .HasForeignKey(x => x.ProviderProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OnboardingStep>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(1024);
        });

        modelBuilder.Entity<AgentRunLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AgentKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PromptHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.FailureMessage).HasMaxLength(2048);
        });

        modelBuilder.Entity<AgentRuntimeGlobalSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DefaultActivationMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.DefaultOverlapPolicy).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.DefaultRestartPolicy).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.DefaultNetworkPolicy).HasMaxLength(64).IsRequired();
            entity.Property(x => x.AllowedPackageFeedHosts).HasMaxLength(2048);
            entity.Property(x => x.BlockedNetworkCidrs).HasMaxLength(2048);
            entity.Property(x => x.AgentSourceRootPath).HasMaxLength(1024);
            entity.Property(x => x.AgentPackageCachePath).HasMaxLength(1024);
            entity.Property(x => x.DotNetBuilderImage).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DotNetRuntimeBaseImage).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<AgentPackageSource>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RepositoryUrl).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.Host).HasMaxLength(253).IsRequired();
            entity.Property(x => x.RepositoryOwner).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RepositoryName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DefaultBranch).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.RepositoryUrl).IsUnique();
        });

        modelBuilder.Entity<AgentPackageVersion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CommitSha).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ManifestDigest).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ManifestJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.AgentId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AgentName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Version).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PublisherId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PublisherName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RuntimeType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProjectPath).HasMaxLength(1024);
            entity.Property(x => x.TargetFramework).HasMaxLength(64);
            entity.Property(x => x.DefaultActivationMode).HasMaxLength(32);
            entity.Property(x => x.WarningsJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.PackageSourceId, x.CommitSha, x.ManifestDigest }).IsUnique();
            entity.HasOne(x => x.PackageSource)
                .WithMany()
                .HasForeignKey(x => x.PackageSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
