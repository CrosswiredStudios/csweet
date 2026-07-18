using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProactiveExecutiveBriefings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationTopicsJson",
                table: "ManagementStatusReports",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ImmediateActionsJson",
                table: "ManagementStatusReports",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Markdown",
                table: "ManagementStatusReports",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "ManagementStatusReports",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Important");

            migrationBuilder.AddColumn<string>(
                name: "ExecutiveBriefingCadence",
                table: "ManagementCycles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Weekdays");

            migrationBuilder.AddColumn<bool>(
                name: "ExecutiveBriefingEnabled",
                table: "ManagementCycles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutiveBriefingLocalTime",
                table: "ManagementCycles",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "09:00");

            migrationBuilder.AddColumn<string>(
                name: "ExecutiveBriefingWeeklyDay",
                table: "ManagementCycles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Friday");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextExecutiveBriefingAt",
                table: "ManagementCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StartupBriefingEnabled",
                table: "ManagementCycles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DispatchAttempts",
                table: "ManagementCheckInRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "ManagementCheckInRequests",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "ManagementCheckInRequests",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ManagementCheckInRequests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDispatchedAt",
                table: "ManagementCheckInRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "ManagementCheckInRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExecutiveBriefingDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagementCheckInRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagementStatusReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutiveBriefingDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefingDeliveries_CoreConversationMessages_Conver~",
                        column: x => x.ConversationMessageId,
                        principalTable: "CoreConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefingDeliveries_CoreConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "CoreConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefingDeliveries_CoreOrganizationUsers_Recipient~",
                        column: x => x.RecipientOrganizationUserId,
                        principalTable: "CoreOrganizationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefingDeliveries_ManagementCheckInRequests_Manag~",
                        column: x => x.ManagementCheckInRequestId,
                        principalTable: "ManagementCheckInRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefingDeliveries_ManagementStatusReports_Managem~",
                        column: x => x.ManagementStatusReportId,
                        principalTable: "ManagementStatusReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutiveBriefingDeliveries_UserNotifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "UserNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementCheckInRequests_OrganizationId_IdempotencyKey",
                table: "ManagementCheckInRequests",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefingDeliveries_ConversationId",
                table: "ExecutiveBriefingDeliveries",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefingDeliveries_ConversationMessageId",
                table: "ExecutiveBriefingDeliveries",
                column: "ConversationMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefingDeliveries_ManagementCheckInRequestId",
                table: "ExecutiveBriefingDeliveries",
                column: "ManagementCheckInRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefingDeliveries_ManagementStatusReportId",
                table: "ExecutiveBriefingDeliveries",
                column: "ManagementStatusReportId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefingDeliveries_NotificationId",
                table: "ExecutiveBriefingDeliveries",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefingDeliveries_RecipientOrganizationUserId",
                table: "ExecutiveBriefingDeliveries",
                column: "RecipientOrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveBriefingDeliveries_Status_LastAttemptAt",
                table: "ExecutiveBriefingDeliveries",
                columns: new[] { "Status", "LastAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutiveBriefingDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_ManagementCheckInRequests_OrganizationId_IdempotencyKey",
                table: "ManagementCheckInRequests");

            migrationBuilder.DropColumn(
                name: "ConversationTopicsJson",
                table: "ManagementStatusReports");

            migrationBuilder.DropColumn(
                name: "ImmediateActionsJson",
                table: "ManagementStatusReports");

            migrationBuilder.DropColumn(
                name: "Markdown",
                table: "ManagementStatusReports");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "ManagementStatusReports");

            migrationBuilder.DropColumn(
                name: "ExecutiveBriefingCadence",
                table: "ManagementCycles");

            migrationBuilder.DropColumn(
                name: "ExecutiveBriefingEnabled",
                table: "ManagementCycles");

            migrationBuilder.DropColumn(
                name: "ExecutiveBriefingLocalTime",
                table: "ManagementCycles");

            migrationBuilder.DropColumn(
                name: "ExecutiveBriefingWeeklyDay",
                table: "ManagementCycles");

            migrationBuilder.DropColumn(
                name: "NextExecutiveBriefingAt",
                table: "ManagementCycles");

            migrationBuilder.DropColumn(
                name: "StartupBriefingEnabled",
                table: "ManagementCycles");

            migrationBuilder.DropColumn(
                name: "DispatchAttempts",
                table: "ManagementCheckInRequests");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "ManagementCheckInRequests");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                table: "ManagementCheckInRequests");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ManagementCheckInRequests");

            migrationBuilder.DropColumn(
                name: "LastDispatchedAt",
                table: "ManagementCheckInRequests");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "ManagementCheckInRequests");
        }
    }
}
