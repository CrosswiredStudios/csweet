using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentInstallations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentInstallations_AgentPackageVersions_PackageVersionId",
                        column: x => x.PackageVersionId,
                        principalTable: "AgentPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentInstallationGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "text", nullable: false),
                    SubscriptionsJson = table.Column<string>(type: "text", nullable: false),
                    PublicationsJson = table.Column<string>(type: "text", nullable: false),
                    PermissionsJson = table.Column<string>(type: "text", nullable: false),
                    NetworkAccessJson = table.Column<string>(type: "text", nullable: false),
                    MaxRuntimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    MemoryMb = table.Column<int>(type: "integer", nullable: false),
                    CpuPercent = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentInstallationGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentInstallationGrants_AgentInstallations_AgentInstallatio~",
                        column: x => x.AgentInstallationId,
                        principalTable: "AgentInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivationMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TickFrequencySeconds = table.Column<int>(type: "integer", nullable: false),
                    NextTickAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastTickAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RunRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaxRuntimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxRetriesPerTick = table.Column<int>(type: "integer", nullable: false),
                    OverlapPolicy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSchedules_AgentInstallations_AgentInstallationId",
                        column: x => x.AgentInstallationId,
                        principalTable: "AgentInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallationGrants_AgentInstallationId",
                table: "AgentInstallationGrants",
                column: "AgentInstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallations_PackageVersionId_BusinessId",
                table: "AgentInstallations",
                columns: new[] { "PackageVersionId", "BusinessId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSchedules_AgentInstallationId",
                table: "AgentSchedules",
                column: "AgentInstallationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentInstallationGrants");

            migrationBuilder.DropTable(
                name: "AgentSchedules");

            migrationBuilder.DropTable(
                name: "AgentInstallations");
        }
    }
}
