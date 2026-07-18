using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWorkstreamManagerInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Workstreams_ExecutionRequiresManager",
                table: "Workstreams",
                sql: "\"Status\" NOT IN ('Approved', 'Active') OR \"AccountableManagerOrganizationUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Workstreams_ExecutionRequiresManager",
                table: "Workstreams");
        }
    }
}
