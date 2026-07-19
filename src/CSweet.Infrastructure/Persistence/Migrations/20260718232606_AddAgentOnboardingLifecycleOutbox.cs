using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentOnboardingLifecycleOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentOnboardingEventOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HiringOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentOnboardingEventOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentOnboardingEventOutbox_CoreConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "CoreConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentOnboardingEventOutbox_CoreOrganizationUsers_AgentOrgan~",
                        column: x => x.AgentOrganizationUserId,
                        principalTable: "CoreOrganizationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentOnboardingEventOutbox_CoreOrganizationUsers_HiringOrga~",
                        column: x => x.HiringOrganizationUserId,
                        principalTable: "CoreOrganizationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentOnboardingEventOutbox_AgentOrganizationUserId",
                table: "AgentOnboardingEventOutbox",
                column: "AgentOrganizationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentOnboardingEventOutbox_ConversationId",
                table: "AgentOnboardingEventOutbox",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentOnboardingEventOutbox_HiringOrganizationUserId",
                table: "AgentOnboardingEventOutbox",
                column: "HiringOrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentOnboardingEventOutbox_Status_NextAttemptAt",
                table: "AgentOnboardingEventOutbox",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentOnboardingEventOutbox");
        }
    }
}
