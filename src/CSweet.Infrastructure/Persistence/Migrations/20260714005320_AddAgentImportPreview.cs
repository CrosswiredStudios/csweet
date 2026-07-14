using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentImportPreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentPackageSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    RepositoryOwner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RepositoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultBranch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPackageSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentPackageVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ManifestDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ManifestJson = table.Column<string>(type: "text", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublisherId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PublisherName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuntimeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProjectPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TargetFramework = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DefaultActivationMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPackageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentPackageVersions_AgentPackageSources_PackageSourceId",
                        column: x => x.PackageSourceId,
                        principalTable: "AgentPackageSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPackageSources_RepositoryUrl",
                table: "AgentPackageSources",
                column: "RepositoryUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentPackageVersions_PackageSourceId_CommitSha_ManifestDige~",
                table: "AgentPackageVersions",
                columns: new[] { "PackageSourceId", "CommitSha", "ManifestDigest" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPackageVersions");

            migrationBuilder.DropTable(
                name: "AgentPackageSources");
        }
    }
}
