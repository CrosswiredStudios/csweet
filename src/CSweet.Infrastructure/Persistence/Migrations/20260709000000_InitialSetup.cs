using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations;

public partial class InitialSetup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                EntityType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                MetadataJson = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LlmProviderProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                ProviderType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                BaseUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                ApiKeySecretName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                DefaultChatModel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                DefaultEmbeddingModel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ContextWindowTokens = table.Column<int>(type: "integer", nullable: true),
                MaxOutputTokens = table.Column<int>(type: "integer", nullable: true),
                SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                SupportsToolCalling = table.Column<bool>(type: "boolean", nullable: false),
                SupportsStructuredOutput = table.Column<bool>(type: "boolean", nullable: false),
                SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LastSuccessfulConnectionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LlmProviderProfiles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OnboardingSteps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OnboardingSteps", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SystemConfigurations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IsFirstRunComplete = table.Column<bool>(type: "boolean", nullable: false),
                DefaultChatProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                DefaultEmbeddingProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SystemConfigurations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ModelCapabilityTests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                ConnectionSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                ChatSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                StreamingSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                ToolCallingSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                StructuredOutputSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                FailureMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                RawResult = table.Column<string>(type: "text", nullable: true),
                TestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelCapabilityTests", x => x.Id);
                table.ForeignKey(
                    name: "FK_ModelCapabilityTests_LlmProviderProfiles_ProviderProfileId",
                    column: x => x.ProviderProfileId,
                    principalTable: "LlmProviderProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ModelCapabilityTests_ProviderProfileId",
            table: "ModelCapabilityTests",
            column: "ProviderProfileId");

        migrationBuilder.CreateIndex(
            name: "IX_OnboardingSteps_Key",
            table: "OnboardingSteps",
            column: "Key",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditEvents");
        migrationBuilder.DropTable(name: "ModelCapabilityTests");
        migrationBuilder.DropTable(name: "OnboardingSteps");
        migrationBuilder.DropTable(name: "SystemConfigurations");
        migrationBuilder.DropTable(name: "LlmProviderProfiles");
    }
}
