using CSweet.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSweet.Infrastructure.Persistence;

internal static class PlanningConfigurations
{
    public static void Apply(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(ConfigureOrganization);
        modelBuilder.Entity<PlanningTask>(ConfigurePlanningTask);
        modelBuilder.Entity<PlanningDocument>(ConfigurePlanningDocument);
        modelBuilder.Entity<PlanningWorkflow>(ConfigurePlanningWorkflow);
    }

    static void ConfigureOrganization(EntityTypeBuilder<Organization> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Industry).HasMaxLength(160);
        entity.Property(x => x.Description).HasMaxLength(4096);
        entity.Property(x => x.Stage).HasMaxLength(80);
        entity.Property(x => x.Location).HasMaxLength(256);
        entity.Property(x => x.TeamSize).HasMaxLength(80);
        entity.Property(x => x.AnnualRevenue).HasMaxLength(128);
        entity.Property(x => x.StrategicGoals).HasMaxLength(4096);
        entity.Property(x => x.KeyChallenges).HasMaxLength(4096);
        entity.Property(x => x.CompetitiveAdvantages).HasMaxLength(4096);
    }

    static void ConfigurePlanningTask(EntityTypeBuilder<PlanningTask> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.TaskKey).HasMaxLength(128).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(x => x.AgentKey).HasMaxLength(128);
        entity.Property(x => x.SystemPrompt).HasMaxLength(8192);
        entity.Property(x => x.UserPrompt).HasMaxLength(8192);
        entity.Property(x => x.OutputContent).HasMaxLength(65536);
        entity.Property(x => x.OutputStructuredJson).HasMaxLength(65536);
        entity.Property(x => x.FailureMessage).HasMaxLength(4096);

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.OrganizationId, x.TaskKey });
    }

    static void ConfigurePlanningDocument(EntityTypeBuilder<PlanningDocument> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).HasMaxLength(512).IsRequired();
        entity.Property(x => x.DocumentType).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Content).HasMaxLength(131072).IsRequired();
        entity.Property(x => x.StructuredJson).HasMaxLength(65536);
        entity.Property(x => x.Summary).HasMaxLength(8192);

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.OrganizationId, x.DocumentType, x.IsLatest });
    }

    static void ConfigurePlanningWorkflow(EntityTypeBuilder<PlanningWorkflow> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2048);
        entity.Property(x => x.TaskDefinitionJson).HasMaxLength(65536);

        entity.HasIndex(x => x.Key).IsUnique();
    }
}
