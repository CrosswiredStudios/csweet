using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteLeadershipAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeadershipAssignments_CoreOrganizationUsers_OrganizationUse~",
                table: "LeadershipAssignments");

            migrationBuilder.AddForeignKey(
                name: "FK_LeadershipAssignments_CoreOrganizationUsers_OrganizationUse~",
                table: "LeadershipAssignments",
                column: "OrganizationUserId",
                principalTable: "CoreOrganizationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeadershipAssignments_CoreOrganizationUsers_OrganizationUse~",
                table: "LeadershipAssignments");

            migrationBuilder.AddForeignKey(
                name: "FK_LeadershipAssignments_CoreOrganizationUsers_OrganizationUse~",
                table: "LeadershipAssignments",
                column: "OrganizationUserId",
                principalTable: "CoreOrganizationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
