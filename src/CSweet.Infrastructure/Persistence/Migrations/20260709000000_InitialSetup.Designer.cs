using System;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CSweetDbContext))]
[Migration("20260709000000_InitialSetup")]
partial class InitialSetup
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4");

        modelBuilder.Entity("CSweet.Domain.Setup.AuditEvent", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid?>("EntityId").HasColumnType("uuid");
            b.Property<string>("EntityType").IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("EventType").IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("MetadataJson").HasColumnType("text");
            b.Property<string>("Summary").HasMaxLength(1024).HasColumnType("character varying(1024)");
            b.HasKey("Id");
            b.ToTable("AuditEvents");
        });

        modelBuilder.Entity("CSweet.Domain.Setup.LlmProviderProfile", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("ApiKeySecretName").HasMaxLength(256).HasColumnType("character varying(256)");
            b.Property<string>("BaseUrl").IsRequired().HasMaxLength(2048).HasColumnType("character varying(2048)");
            b.Property<int?>("ContextWindowTokens").HasColumnType("integer");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("DefaultChatModel").IsRequired().HasMaxLength(256).HasColumnType("character varying(256)");
            b.Property<string>("DefaultEmbeddingModel").HasMaxLength(256).HasColumnType("character varying(256)");
            b.Property<bool>("IsEnabled").HasColumnType("boolean");
            b.Property<DateTimeOffset?>("LastSuccessfulConnectionAt").HasColumnType("timestamp with time zone");
            b.Property<int?>("MaxOutputTokens").HasColumnType("integer");
            b.Property<string>("Name").IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("ProviderType").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<bool>("SupportsStreaming").HasColumnType("boolean");
            b.Property<bool>("SupportsStructuredOutput").HasColumnType("boolean");
            b.Property<bool>("SupportsToolCalling").HasColumnType("boolean");
            b.Property<bool>("SupportsVision").HasColumnType("boolean");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.ToTable("LlmProviderProfiles");
        });

        modelBuilder.Entity("CSweet.Domain.Setup.ModelCapabilityTest", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<bool>("ChatSucceeded").HasColumnType("boolean");
            b.Property<bool>("ConnectionSucceeded").HasColumnType("boolean");
            b.Property<string>("FailureMessage").HasMaxLength(2048).HasColumnType("character varying(2048)");
            b.Property<Guid>("ProviderProfileId").HasColumnType("uuid");
            b.Property<string>("RawResult").HasColumnType("text");
            b.Property<bool>("StreamingSucceeded").HasColumnType("boolean");
            b.Property<bool>("StructuredOutputSucceeded").HasColumnType("boolean");
            b.Property<DateTimeOffset>("TestedAt").HasColumnType("timestamp with time zone");
            b.Property<bool>("ToolCallingSucceeded").HasColumnType("boolean");
            b.HasKey("Id");
            b.HasIndex("ProviderProfileId");
            b.ToTable("ModelCapabilityTests");
        });

        modelBuilder.Entity("CSweet.Domain.Setup.OnboardingStep", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<bool>("IsComplete").HasColumnType("boolean");
            b.Property<bool>("IsRequired").HasColumnType("boolean");
            b.Property<string>("Key").IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("Key").IsUnique();
            b.ToTable("OnboardingSteps");
        });

        modelBuilder.Entity("CSweet.Domain.Setup.SystemConfiguration", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid?>("DefaultChatProviderId").HasColumnType("uuid");
            b.Property<Guid?>("DefaultEmbeddingProviderId").HasColumnType("uuid");
            b.Property<bool>("IsFirstRunComplete").HasColumnType("boolean");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.ToTable("SystemConfigurations");
        });

        modelBuilder.Entity("CSweet.Domain.Setup.ModelCapabilityTest", b =>
        {
            b.HasOne("CSweet.Domain.Setup.LlmProviderProfile", "ProviderProfile")
                .WithMany()
                .HasForeignKey("ProviderProfileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("ProviderProfile");
        });
    }
}
