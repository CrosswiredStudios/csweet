using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentBuildPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BuiltAt",
                table: "AgentPackageVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageDigest",
                table: "AgentPackageVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackagePath",
                table: "AgentPackageVersions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentBuildJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceWorkspacePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PackagePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PackageDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LogPath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentBuildJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentBuildJobs_AgentPackageVersions_PackageVersionId",
                        column: x => x.PackageVersionId,
                        principalTable: "AgentPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentBuildJobs_PackageVersionId_Attempt",
                table: "AgentBuildJobs",
                columns: new[] { "PackageVersionId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentBuildJobs_Status_QueuedAt",
                table: "AgentBuildJobs",
                columns: new[] { "Status", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentBuildJobs");

            migrationBuilder.DropColumn(
                name: "BuiltAt",
                table: "AgentPackageVersions");

            migrationBuilder.DropColumn(
                name: "PackageDigest",
                table: "AgentPackageVersions");

            migrationBuilder.DropColumn(
                name: "PackagePath",
                table: "AgentPackageVersions");
        }
    }
}
