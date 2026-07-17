using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImmutablePluginInstallationRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstallationKey",
                table: "AgentInstallations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "AgentInstallations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RevisionStatus",
                table: "AgentInstallations",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesInstallationId",
                table: "AgentInstallations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AgentInstallations"
                SET "InstallationKey" = "Id",
                    "RevisionNumber" = 1,
                    "RevisionStatus" = CASE
                        WHEN EXISTS (
                            SELECT 1 FROM "AgentPackageVersions" p
                            WHERE p."Id" = "AgentInstallations"."PackageVersionId"
                              AND p."ManifestFileName" = 'csweet-plugin.json')
                        THEN 'Active' ELSE 'Retired' END,
                    "IsEnabled" = CASE
                        WHEN EXISTS (
                            SELECT 1 FROM "AgentPackageVersions" p
                            WHERE p."Id" = "AgentInstallations"."PackageVersionId"
                              AND p."ManifestFileName" = 'csweet-plugin.json')
                        THEN "IsEnabled" ELSE FALSE END;

                UPDATE "AgentPackageVersions"
                SET "PluginKind" = 'Service'
                WHERE "PluginKind" = 'CommunicationProvider';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallations_InstallationKey_RevisionNumber",
                table: "AgentInstallations",
                columns: new[] { "InstallationKey", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentInstallations_InstallationKey_RevisionNumber",
                table: "AgentInstallations");

            migrationBuilder.DropColumn(
                name: "InstallationKey",
                table: "AgentInstallations");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "AgentInstallations");

            migrationBuilder.DropColumn(
                name: "RevisionStatus",
                table: "AgentInstallations");

            migrationBuilder.DropColumn(
                name: "SupersedesInstallationId",
                table: "AgentInstallations");
        }
    }
}
