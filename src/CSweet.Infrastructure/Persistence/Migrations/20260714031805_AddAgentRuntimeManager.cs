using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRuntimeManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRuntimeInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TickId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    WorkloadTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContainerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BrokerRegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletionReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RuntimeDeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuntimeInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuntimeInstances_AgentInstallations_AgentInstallationId",
                        column: x => x.AgentInstallationId,
                        principalTable: "AgentInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuntimeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRuntimeInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuntimeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuntimeEvents_AgentRuntimeInstances_AgentRuntimeInstan~",
                        column: x => x.AgentRuntimeInstanceId,
                        principalTable: "AgentRuntimeInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuntimeEvents_AgentRuntimeInstanceId_OccurredAt",
                table: "AgentRuntimeEvents",
                columns: new[] { "AgentRuntimeInstanceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuntimeInstances_AgentInstallationId_Status",
                table: "AgentRuntimeInstances",
                columns: new[] { "AgentInstallationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuntimeInstances_TickId",
                table: "AgentRuntimeInstances",
                column: "TickId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRuntimeEvents");

            migrationBuilder.DropTable(
                name: "AgentRuntimeInstances");
        }
    }
}
