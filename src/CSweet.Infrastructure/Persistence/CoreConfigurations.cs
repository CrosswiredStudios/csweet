using CSweet.Domain.Core;
using CSweet.Domain.Communications;
using CSweet.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSweet.Infrastructure.Persistence;

internal static class CoreConfigurations
{
    public static void Apply(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(ConfigureCoreOrganization);
        modelBuilder.Entity<OrganizationUser>(ConfigureOrganizationUser);
        modelBuilder.Entity<Role>(ConfigureRole);
        modelBuilder.Entity<StrategicObjective>(ConfigureStrategicObjective);
        modelBuilder.Entity<Worker>(ConfigureWorker);
        modelBuilder.Entity<WorkTask>(ConfigureWorkTask);
        modelBuilder.Entity<TaskRun>(ConfigureTaskRun);
        modelBuilder.Entity<Artifact>(ConfigureArtifact);
        modelBuilder.Entity<Approval>(ConfigureApproval);
        modelBuilder.Entity<Conversation>(ConfigureConversation);
        modelBuilder.Entity<ConversationParticipant>(ConfigureConversationParticipant);
        modelBuilder.Entity<ConversationMessage>(ConfigureConversationMessage);
        modelBuilder.Entity<ChatTurn>(ConfigureChatTurn);
        modelBuilder.Entity<ChatTurnTraceEvent>(ConfigureChatTurnTraceEvent);
        modelBuilder.Entity<MemoryCaptureOutboxItem>(ConfigureMemoryCaptureOutbox);
        modelBuilder.Entity<AgentMemoryNamespaceRegistration>(ConfigureAgentMemoryNamespace);
        modelBuilder.Entity<AgentMemoryRecallUse>(ConfigureAgentMemoryRecallUse);
        modelBuilder.Entity<CommunicationConnection>(ConfigureCommunicationConnection);
        modelBuilder.Entity<ManagedExternalResource>(ConfigureManagedExternalResource);
        modelBuilder.Entity<ExternalIdentityLink>(ConfigureExternalIdentityLink);
        modelBuilder.Entity<ExternalIdentityLinkCode>(ConfigureExternalIdentityLinkCode);
        modelBuilder.Entity<ExternalMessageReference>(ConfigureExternalMessageReference);
        modelBuilder.Entity<CommunicationDelivery>(ConfigureCommunicationDelivery);
        modelBuilder.Entity<UserNotification>(ConfigureUserNotification);
        modelBuilder.Entity<NotificationPreference>(ConfigureNotificationPreference);
    }

    static void ConfigureOrganizationUser(EntityTypeBuilder<OrganizationUser> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(256);
        entity.Property(x => x.EmployeeType).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.PermissionLevel).HasConversion<string>().HasMaxLength(16).IsRequired();

        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.ReportsToOrganizationUser)
            .WithMany()
            .HasForeignKey(x => x.ReportsToOrganizationUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CoreOrganizationUsers_Manager");

        entity.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.Worker)
            .WithMany()
            .HasForeignKey(x => x.WorkerId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.AgentInstallation)
            .WithMany()
            .HasForeignKey(x => x.AgentInstallationId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AgentInstallationId)
            .IsUnique()
            .HasFilter("\"AgentInstallationId\" IS NOT NULL");
        entity.HasIndex(x => new { x.OrganizationId, x.ApplicationUserId })
            .IsUnique()
            .HasFilter("\"ApplicationUserId\" IS NOT NULL");
    }

    static void ConfigureCoreOrganization(EntityTypeBuilder<Organization> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Industry).HasMaxLength(160);
        entity.Property(x => x.Mission).HasMaxLength(4096);
        entity.Property(x => x.Stage).HasMaxLength(80);
        entity.Property(x => x.PrimaryGoal).HasMaxLength(2048);
        entity.Property(x => x.ConstraintsJson).HasMaxLength(65536);
    }

    static void ConfigureRole(EntityTypeBuilder<Role> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(4096).IsRequired();
        entity.Property(x => x.ResponsibilitiesJson).HasMaxLength(65536).IsRequired();
        entity.Property(x => x.AuthorityLevel).HasConversion<string>().HasMaxLength(32).IsRequired();

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
    }

    static void ConfigureStrategicObjective(EntityTypeBuilder<StrategicObjective> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).HasMaxLength(512).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(8192).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    static void ConfigureWorker(EntityTypeBuilder<Worker> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(4096).IsRequired();
        entity.Property(x => x.WorkerType).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(x => x.ExecutionMode).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(x => x.CapabilitiesJson).HasMaxLength(65536).IsRequired();
        entity.Property(x => x.CostModelJson).HasMaxLength(65536);
        entity.Property(x => x.EndpointConfigurationJson).HasMaxLength(65536);

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    static void ConfigureWorkTask(EntityTypeBuilder<WorkTask> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).HasMaxLength(512).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(8192).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.StrategicObjective)
            .WithMany()
            .HasForeignKey(x => x.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.AssignedRole)
            .WithMany()
            .HasForeignKey(x => x.AssignedRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.AssignedWorker)
            .WithMany()
            .HasForeignKey(x => x.AssignedWorkerId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    static void ConfigureTaskRun(EntityTypeBuilder<TaskRun> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.InputJson).HasMaxLength(65536);
        entity.Property(x => x.OutputJson).HasMaxLength(65536);
        entity.Property(x => x.FailureMessage).HasMaxLength(4096);
        entity.Property(x => x.CostCurrency).HasMaxLength(8);

        entity.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Worker)
            .WithMany()
            .HasForeignKey(x => x.WorkerId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    static void ConfigureArtifact(EntityTypeBuilder<Artifact> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(512).IsRequired();
        entity.Property(x => x.Content).HasMaxLength(131072).IsRequired();
        entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(32).IsRequired();

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.TaskRun)
            .WithMany()
            .HasForeignKey(x => x.TaskRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    static void ConfigureApproval(EntityTypeBuilder<Approval> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(x => x.Comment).HasMaxLength(4096);

        entity.HasOne(x => x.Artifact)
            .WithMany()
            .HasForeignKey(x => x.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    static void ConfigureConversation(EntityTypeBuilder<Conversation> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).HasMaxLength(256);
        entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.AgentOrganizationUser)
            .WithMany()
            .HasForeignKey(x => x.AgentOrganizationUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.OrganizationId, x.AgentOrganizationUserId });
    }

    static void ConfigureConversationParticipant(EntityTypeBuilder<ConversationParticipant> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.HasOne(x => x.Conversation).WithMany(x => x.Participants).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.OrganizationUser).WithMany().HasForeignKey(x => x.OrganizationUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.ConversationId, x.OrganizationUserId }).IsUnique();
    }

    static void ConfigureConversationMessage(EntityTypeBuilder<ConversationMessage> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.Content).HasMaxLength(32768).IsRequired();
        entity.Property(x => x.DeliveryIntent).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.Property(x => x.SourceProvider).HasMaxLength(32).IsRequired();
        entity.Property(x => x.SourceChannelExternalId).HasMaxLength(128);
        entity.Property(x => x.IdempotencyKey).HasMaxLength(256);

        entity.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        entity.HasIndex(x => x.ChatTurnId);
        entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }

    static void ConfigureChatTurn(EntityTypeBuilder<ChatTurn> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(x => x.PartialResponse).HasMaxLength(131072).IsRequired();
        entity.Property(x => x.ErrorCode).HasMaxLength(64);
        entity.Property(x => x.ErrorMessage).HasMaxLength(4096);
        entity.Property(x => x.LeaseOwner).HasMaxLength(160);
        entity.HasIndex(x => new { x.Status, x.CreatedAt });
        entity.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        entity.HasIndex(x => x.UserMessageId).IsUnique();
        entity.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.UserMessage).WithMany().HasForeignKey(x => x.UserMessageId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.AssistantMessage).WithMany().HasForeignKey(x => x.AssistantMessageId).OnDelete(DeleteBehavior.SetNull);
    }

    static void ConfigureChatTurnTraceEvent(EntityTypeBuilder<ChatTurnTraceEvent> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Category).HasMaxLength(32).IsRequired();
        entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Summary).HasMaxLength(8192);
        entity.Property(x => x.DetailsJson).HasColumnType("jsonb");
        entity.Property(x => x.Sensitivity).HasMaxLength(32).IsRequired();
        entity.HasIndex(x => new { x.ChatTurnId, x.Sequence }).IsUnique();
        entity.HasOne(x => x.ChatTurn).WithMany(x => x.TraceEvents).HasForeignKey(x => x.ChatTurnId).OnDelete(DeleteBehavior.Cascade);
    }

    static void ConfigureMemoryCaptureOutbox(EntityTypeBuilder<MemoryCaptureOutboxItem> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.LastError).HasMaxLength(2048);
        entity.HasIndex(x => x.ConversationMessageId).IsUnique();
        entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
        entity.HasOne(x => x.ConversationMessage)
            .WithMany()
            .HasForeignKey(x => x.ConversationMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    static void ConfigureAgentMemoryNamespace(EntityTypeBuilder<AgentMemoryNamespaceRegistration> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PartitionKey).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.Scope).HasMaxLength(32).IsRequired();
        entity.HasIndex(x => x.PartitionKey).IsUnique();
        entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.UserId });
    }

    static void ConfigureAgentMemoryRecallUse(EntityTypeBuilder<AgentMemoryRecallUse> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Layer).HasMaxLength(32).IsRequired();
        entity.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.MemoryId, x.UsedAt });
        entity.HasIndex(x => new { x.ConversationId, x.UsedAt });
    }

    static void ConfigureCommunicationConnection(EntityTypeBuilder<CommunicationConnection> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.Property(x => x.WorkspaceMode).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.Property(x => x.WorkspaceExternalId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.RelayPairingId).HasMaxLength(256);
        entity.Property(x => x.ConfigurationJson).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => new { x.Provider, x.WorkspaceExternalId }).IsUnique();
        entity.HasIndex(x => new { x.OrganizationId, x.Provider });
    }

    static void ConfigureManagedExternalResource(EntityTypeBuilder<ManagedExternalResource> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.Property(x => x.Purpose).HasMaxLength(96).IsRequired();
        entity.Property(x => x.ExternalId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.ParentExternalId).HasMaxLength(128);
        entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        entity.Property(x => x.MetadataJson).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => new { x.ConnectionId, x.ExternalId }).IsUnique();
        entity.HasIndex(x => new { x.ConnectionId, x.OrganizationUserId, x.Kind });
    }

    static void ConfigureExternalIdentityLink(EntityTypeBuilder<ExternalIdentityLink> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ExternalUserId).HasMaxLength(128).IsRequired();
        entity.HasIndex(x => new { x.ConnectionId, x.ExternalUserId }).IsUnique();
        entity.HasIndex(x => new { x.ConnectionId, x.OrganizationUserId }).IsUnique();
    }

    static void ConfigureExternalIdentityLinkCode(EntityTypeBuilder<ExternalIdentityLinkCode> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        entity.HasIndex(x => x.CodeHash).IsUnique();
        entity.HasIndex(x => new { x.ConnectionId, x.ExpiresAt });
    }

    static void ConfigureExternalMessageReference(EntityTypeBuilder<ExternalMessageReference> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ChannelExternalId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.MessageExternalId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.ThreadExternalId).HasMaxLength(128);
        entity.HasIndex(x => new { x.ConnectionId, x.MessageExternalId }).IsUnique();
    }

    static void ConfigureCommunicationDelivery(EntityTypeBuilder<CommunicationDelivery> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.Property(x => x.IdempotencyKey).HasMaxLength(256).IsRequired();
        entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.LeaseOwner).HasMaxLength(160);
        entity.Property(x => x.ExternalReceiptId).HasMaxLength(256);
        entity.Property(x => x.LastError).HasMaxLength(4096);
        entity.HasIndex(x => x.IdempotencyKey).IsUnique();
        entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }

    static void ConfigureUserNotification(EntityTypeBuilder<UserNotification> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Body).HasMaxLength(8192).IsRequired();
        entity.Property(x => x.ActionUri).HasMaxLength(2048);
        entity.Property(x => x.DeduplicationKey).HasMaxLength(256);
        entity.HasIndex(x => new { x.RecipientOrganizationUserId, x.ReadAt, x.CreatedAt });
        entity.HasIndex(x => new { x.OrganizationId, x.DeduplicationKey }).IsUnique().HasFilter("\"DeduplicationKey\" IS NOT NULL");
    }

    static void ConfigureNotificationPreference(EntityTypeBuilder<NotificationPreference> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(24).IsRequired();
        entity.Property(x => x.MinimumSeverity).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.QuietHoursStart).HasMaxLength(5);
        entity.Property(x => x.QuietHoursEnd).HasMaxLength(5);
        entity.Property(x => x.TimeZoneId).HasMaxLength(128).IsRequired();
        entity.HasIndex(x => new { x.OrganizationUserId, x.Provider }).IsUnique();
    }
}
