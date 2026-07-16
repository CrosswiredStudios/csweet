using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEmployeeMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoreOrganizationUsers_AgentInstallationId",
                table: "CoreOrganizationUsers");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "CoreOrganizationUsers"
                        WHERE "AgentInstallationId" IS NOT NULL
                        GROUP BY "AgentInstallationId"
                        HAVING COUNT(*) > 1)
                    THEN
                        RAISE EXCEPTION 'An agent installation is linked to more than one employee. Resolve the ambiguous ownership before applying AddAgentEmployeeMemory.';
                    END IF;
                END $$;

                UPDATE "AgentInstallations" AS installation
                SET "BusinessId" = employee."OrganizationId"::text,
                    "UpdatedAt" = NOW()
                FROM "CoreOrganizationUsers" AS employee
                WHERE employee."AgentInstallationId" = installation."Id"
                  AND installation."BusinessId" <> employee."OrganizationId"::text;
                """);

            migrationBuilder.CreateTable(
                name: "MemoryCaptureOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EpisodeCapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EnrichedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryCaptureOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryCaptureOutbox_CoreConversationMessages_ConversationMe~",
                        column: x => x.ConversationMessageId,
                        principalTable: "CoreConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoreOrganizationUsers_AgentInstallationId",
                table: "CoreOrganizationUsers",
                column: "AgentInstallationId",
                unique: true,
                filter: "\"AgentInstallationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryCaptureOutbox_ConversationMessageId",
                table: "MemoryCaptureOutbox",
                column: "ConversationMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryCaptureOutbox_Status_NextAttemptAt",
                table: "MemoryCaptureOutbox",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryCaptureOutbox");

            migrationBuilder.DropIndex(
                name: "IX_CoreOrganizationUsers_AgentInstallationId",
                table: "CoreOrganizationUsers");

            migrationBuilder.CreateIndex(
                name: "IX_CoreOrganizationUsers_AgentInstallationId",
                table: "CoreOrganizationUsers",
                column: "AgentInstallationId");
        }
    }
}
