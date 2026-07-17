using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginArchiveSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceArchivePath",
                table: "AgentPackageSources",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "AgentPackageSources",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "GitHub");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceArchivePath",
                table: "AgentPackageSources");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "AgentPackageSources");
        }
    }
}
