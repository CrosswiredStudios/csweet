using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentOnboardingUnreadRealtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentInstallations_PackageVersionId_BusinessId",
                table: "AgentInstallations");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeletionProtected",
                table: "CoreConversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "CoreConversationMessages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "LastReadMessageSequence",
                table: "ConversationParticipants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ApplicationRealtimeOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientOrganizationUserIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DataJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRealtimeOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoreConversations_OrganizationId_InitiatedByOrganizationUse~",
                table: "CoreConversations",
                columns: new[] { "OrganizationId", "InitiatedByOrganizationUserId", "AgentOrganizationUserId" },
                unique: true,
                filter: "\"IsDeletionProtected\" = TRUE AND \"AgentOrganizationUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CoreConversationMessages_Sequence",
                table: "CoreConversationMessages",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallations_PackageVersionId_BusinessId",
                table: "AgentInstallations",
                columns: new[] { "PackageVersionId", "BusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRealtimeOutbox_OrganizationId_ChatId_Sequence",
                table: "ApplicationRealtimeOutbox",
                columns: new[] { "OrganizationId", "ChatId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRealtimeOutbox_RecipientOrganizationUserId_Seque~",
                table: "ApplicationRealtimeOutbox",
                columns: new[] { "RecipientOrganizationUserId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRealtimeOutbox_Sequence",
                table: "ApplicationRealtimeOutbox",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRealtimeOutbox_Status_NextAttemptAt_Sequence",
                table: "ApplicationRealtimeOutbox",
                columns: new[] { "Status", "NextAttemptAt", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationRealtimeOutbox");

            migrationBuilder.DropIndex(
                name: "IX_CoreConversations_OrganizationId_InitiatedByOrganizationUse~",
                table: "CoreConversations");

            migrationBuilder.DropIndex(
                name: "IX_CoreConversationMessages_Sequence",
                table: "CoreConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_AgentInstallations_PackageVersionId_BusinessId",
                table: "AgentInstallations");

            migrationBuilder.DropColumn(
                name: "IsDeletionProtected",
                table: "CoreConversations");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "LastReadMessageSequence",
                table: "ConversationParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallations_PackageVersionId_BusinessId",
                table: "AgentInstallations",
                columns: new[] { "PackageVersionId", "BusinessId" },
                unique: true);
        }
    }
}
