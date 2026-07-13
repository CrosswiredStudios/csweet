using CSweet.Domain.Core;
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
        modelBuilder.Entity<ConversationMessage>(ConfigureConversationMessage);
    }

    static void ConfigureOrganizationUser(EntityTypeBuilder<OrganizationUser> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(256);
        entity.Property(x => x.EmployeeType).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.PermissionLevel).HasConversion<string>().HasMaxLength(16).IsRequired();

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

    static void ConfigureConversationMessage(EntityTypeBuilder<ConversationMessage> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(x => x.Content).HasMaxLength(32768).IsRequired();

        entity.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.ConversationId, x.CreatedAt });
    }
}
