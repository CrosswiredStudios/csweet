using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokeredMcpDecisionsAndHiring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "WorkforcePlans",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RecommendedCandidateId",
                table: "WorkforcePlans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RequestingInstallationId",
                table: "WorkforcePlans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "WorkforcePlans",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "WorkforcePlans",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByOrganizationUserId",
                table: "StaffingActionProposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "StaffingActionProposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "StaffingActionProposals",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestingInstallationId",
                table: "StaffingActionProposals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ResultOrganizationUserId",
                table: "StaffingActionProposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "WorkforcePlans"
                SET "IdempotencyKey" = 'legacy:' || "Id"::text,
                    "Title" = LEFT("Objective", 256),
                    "UpdatedAt" = "CreatedAt"
                WHERE "IdempotencyKey" = '';

                UPDATE "StaffingActionProposals"
                SET "IdempotencyKey" = 'legacy:' || "Id"::text
                WHERE "IdempotencyKey" = '';
                """);

            migrationBuilder.CreateTable(
                name: "ExecutiveDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestingInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RecommendedOptionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SupersededByDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedOptionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FreeTextAnswer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AnsweredByOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NextChatTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutiveDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutiveDecisions_ChatTurns_ChatTurnId",
                        column: x => x.ChatTurnId,
                        principalTable: "ChatTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutiveDecisions_CoreConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "CoreConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforcePlans_OrganizationId_RequestingInstallationId_Idem~",
                table: "WorkforcePlans",
                columns: new[] { "OrganizationId", "RequestingInstallationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffingActionProposals_OrganizationId_RequestingInstallati~",
                table: "StaffingActionProposals",
                columns: new[] { "OrganizationId", "RequestingInstallationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveDecisions_ChatTurnId",
                table: "ExecutiveDecisions",
                column: "ChatTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveDecisions_ConversationId_RequestingInstallationId_~",
                table: "ExecutiveDecisions",
                columns: new[] { "ConversationId", "RequestingInstallationId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveDecisions_RequestingInstallationId_IdempotencyKey",
                table: "ExecutiveDecisions",
                columns: new[] { "RequestingInstallationId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutiveDecisions");

            migrationBuilder.DropIndex(
                name: "IX_WorkforcePlans_OrganizationId_RequestingInstallationId_Idem~",
                table: "WorkforcePlans");

            migrationBuilder.DropIndex(
                name: "IX_StaffingActionProposals_OrganizationId_RequestingInstallati~",
                table: "StaffingActionProposals");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "WorkforcePlans");

            migrationBuilder.DropColumn(
                name: "RecommendedCandidateId",
                table: "WorkforcePlans");

            migrationBuilder.DropColumn(
                name: "RequestingInstallationId",
                table: "WorkforcePlans");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "WorkforcePlans");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkforcePlans");

            migrationBuilder.DropColumn(
                name: "ApprovedByOrganizationUserId",
                table: "StaffingActionProposals");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "StaffingActionProposals");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "StaffingActionProposals");

            migrationBuilder.DropColumn(
                name: "RequestingInstallationId",
                table: "StaffingActionProposals");

            migrationBuilder.DropColumn(
                name: "ResultOrganizationUserId",
                table: "StaffingActionProposals");
        }
    }
}
