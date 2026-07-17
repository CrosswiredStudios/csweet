using CSweet.Domain.Core;
using CSweet.Domain.Communications;
using CSweet.Domain.Planning;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Auth;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Persistence;

public sealed class CSweetDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
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
    public DbSet<AgentInstallation> AgentInstallations => Set<AgentInstallation>();
    public DbSet<AgentInstallationGrant> AgentInstallationGrants => Set<AgentInstallationGrant>();
    public DbSet<AgentInstallationConfiguration> AgentInstallationConfigurations => Set<AgentInstallationConfiguration>();
    public DbSet<AgentSchedule> AgentSchedules => Set<AgentSchedule>();
    public DbSet<AgentBuildJob> AgentBuildJobs => Set<AgentBuildJob>();
    public DbSet<AgentRuntimeInstance> AgentRuntimeInstances => Set<AgentRuntimeInstance>();
    public DbSet<AgentRuntimeEvent> AgentRuntimeEvents => Set<AgentRuntimeEvent>();
    public DbSet<PluginOrganizationGrant> PluginOrganizationGrants => Set<PluginOrganizationGrant>();
    public DbSet<PluginSecret> PluginSecrets => Set<PluginSecret>();

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
    public DbSet<ChatTurn> ChatTurns => Set<ChatTurn>();
    public DbSet<ChatTurnTraceEvent> ChatTurnTraceEvents => Set<ChatTurnTraceEvent>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<CommunicationConnection> CommunicationConnections => Set<CommunicationConnection>();
    public DbSet<ManagedExternalResource> ManagedExternalResources => Set<ManagedExternalResource>();
    public DbSet<ExternalIdentityLink> ExternalIdentityLinks => Set<ExternalIdentityLink>();
    public DbSet<ExternalIdentityLinkCode> ExternalIdentityLinkCodes => Set<ExternalIdentityLinkCode>();
    public DbSet<ExternalMessageReference> ExternalMessageReferences => Set<ExternalMessageReference>();
    public DbSet<CommunicationDelivery> CommunicationDeliveries => Set<CommunicationDelivery>();
    public DbSet<CommunicationIngressReceipt> CommunicationIngressReceipts => Set<CommunicationIngressReceipt>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<MemoryCaptureOutboxItem> MemoryCaptureOutbox => Set<MemoryCaptureOutboxItem>();
    public DbSet<AgentMemoryNamespaceRegistration> AgentMemoryNamespaces => Set<AgentMemoryNamespaceRegistration>();
    public DbSet<AgentMemoryRecallUse> AgentMemoryRecallUses => Set<AgentMemoryRecallUse>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<RootRecoveryCode> RootRecoveryCodes => Set<RootRecoveryCode>();
    public DbSet<EmailDeliveryConfiguration> EmailDeliveryConfigurations => Set<EmailDeliveryConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasIndex(x => x.IsInitialAdministrator)
                .IsUnique()
                .HasFilter("\"IsInitialAdministrator\" = TRUE");
        });

        modelBuilder.Entity<RootRecoveryCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodeHash).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.ConcurrencyStamp).HasMaxLength(64).IsConcurrencyToken();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ApplicationUserId, x.UsedAt });
        });

        modelBuilder.Entity<EmailDeliveryConfiguration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Host).HasMaxLength(253).IsRequired();
            entity.Property(x => x.UserName).HasMaxLength(320);
            entity.Property(x => x.EncryptedPassword).HasMaxLength(4096);
            entity.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
            entity.Property(x => x.FromName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PublicAppUrl).HasMaxLength(2048).IsRequired();
        });

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
            entity.Property(x => x.PluginKind).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.ManifestFileName).HasMaxLength(80).IsRequired();
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
            entity.Property(x => x.PackageDigest).HasMaxLength(64);
            entity.Property(x => x.PackagePath).HasMaxLength(2048);
            entity.HasIndex(x => new { x.PackageSourceId, x.CommitSha, x.ManifestDigest }).IsUnique();
            entity.HasOne(x => x.PackageSource)
                .WithMany()
                .HasForeignKey(x => x.PackageSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentInstallation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BusinessId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(24).IsRequired();
            entity.HasIndex(x => new { x.PackageVersionId, x.BusinessId }).IsUnique();
            entity.HasOne(x => x.PackageVersion)
                .WithMany()
                .HasForeignKey(x => x.PackageVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentInstallationGrant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CapabilitiesJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.RequestedCapabilitiesJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.SubscriptionsJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.PublicationsJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.PermissionsJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.NetworkAccessJson).HasColumnType("text").IsRequired();
            entity.HasIndex(x => x.AgentInstallationId).IsUnique();
            entity.HasOne(x => x.AgentInstallation)
                .WithOne(x => x.Grant)
                .HasForeignKey<AgentInstallationGrant>(x => x.AgentInstallationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PluginOrganizationGrant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PluginInstallationId, x.OrganizationId }).IsUnique();
            entity.HasOne(x => x.PluginInstallation).WithMany()
                .HasForeignKey(x => x.PluginInstallationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PluginSecret>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ProtectedValue).HasMaxLength(8192).IsRequired();
            entity.HasIndex(x => new { x.PluginInstallationId, x.Key }).IsUnique();
            entity.HasOne(x => x.PluginInstallation).WithMany()
                .HasForeignKey(x => x.PluginInstallationId).OnDelete(DeleteBehavior.Cascade);
        });

            modelBuilder.Entity<AgentInstallationConfiguration>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SchemaVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.SettingsJson).HasColumnType("text").IsRequired();
                entity.HasIndex(x => x.AgentInstallationId).IsUnique();
                entity.HasOne(x => x.AgentInstallation)
                .WithOne(x => x.Configuration)
                .HasForeignKey<AgentInstallationConfiguration>(x => x.AgentInstallationId)
                .OnDelete(DeleteBehavior.Cascade);
            });

        modelBuilder.Entity<AgentSchedule>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActivationMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.OverlapPolicy).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.NextTickAt).IsConcurrencyToken();
            entity.HasIndex(x => x.AgentInstallationId).IsUnique();
            entity.HasOne(x => x.AgentInstallation)
                .WithOne(x => x.Schedule)
                .HasForeignKey<AgentSchedule>(x => x.AgentInstallationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentBuildJob>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.SourceWorkspacePath).HasMaxLength(2048);
            entity.Property(x => x.PackagePath).HasMaxLength(2048);
            entity.Property(x => x.PackageDigest).HasMaxLength(64);
            entity.Property(x => x.LogPath).HasMaxLength(2048);
            entity.Property(x => x.FailureMessage).HasMaxLength(2048);
            entity.HasIndex(x => new { x.PackageVersionId, x.Attempt }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.QueuedAt });
            entity.HasOne(x => x.PackageVersion)
                .WithMany(x => x.BuildJobs)
                .HasForeignKey(x => x.PackageVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentRuntimeInstance>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(48).IsRequired();
            entity.Property(x => x.WorkloadTokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ContainerId).HasMaxLength(128);
            entity.Property(x => x.ContainerName).HasMaxLength(128);
            entity.Property(x => x.Reason).HasMaxLength(2048);
            entity.Property(x => x.LogExcerpt).HasColumnType("text");
            entity.HasIndex(x => x.TickId).IsUnique();
            entity.HasIndex(x => new { x.AgentInstallationId, x.Status });
            entity.HasIndex(x => x.AgentInstallationId)
                .HasDatabaseName("UX_AgentRuntimeInstances_ActiveInstallation")
                .IsUnique()
                .HasFilter("\"Status\" IN ('Queued', 'Starting', 'WaitingForBrokerRegistration', 'Running', 'CompletionReported', 'Stopping')");
            entity.HasOne(x => x.AgentInstallation)
                .WithMany(x => x.RuntimeInstances)
                .HasForeignKey(x => x.AgentInstallationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentRuntimeEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(48).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(2048);
            entity.Property(x => x.PayloadJson).HasColumnType("text");
            entity.HasIndex(x => new { x.AgentRuntimeInstanceId, x.OccurredAt });
            entity.HasOne(x => x.AgentRuntimeInstance)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.AgentRuntimeInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
