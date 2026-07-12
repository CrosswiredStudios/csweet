using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeType",
                table: "CoreOrganizationUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Human");

            migrationBuilder.AddColumn<Guid>(
                name: "ReportsToOrganizationUserId",
                table: "CoreOrganizationUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "CoreOrganizationUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkerId",
                table: "CoreOrganizationUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoreOrganizationUsers_ReportsToOrganizationUserId",
                table: "CoreOrganizationUsers",
                column: "ReportsToOrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreOrganizationUsers_RoleId",
                table: "CoreOrganizationUsers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreOrganizationUsers_WorkerId",
                table: "CoreOrganizationUsers",
                column: "WorkerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CoreOrganizationUsers_Manager",
                table: "CoreOrganizationUsers",
                column: "ReportsToOrganizationUserId",
                principalTable: "CoreOrganizationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CoreOrganizationUsers_CoreRoles_RoleId",
                table: "CoreOrganizationUsers",
                column: "RoleId",
                principalTable: "CoreRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CoreOrganizationUsers_CoreWorkers_WorkerId",
                table: "CoreOrganizationUsers",
                column: "WorkerId",
                principalTable: "CoreWorkers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoreOrganizationUsers_Manager",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_CoreOrganizationUsers_CoreRoles_RoleId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_CoreOrganizationUsers_CoreWorkers_WorkerId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropIndex(
                name: "IX_CoreOrganizationUsers_ReportsToOrganizationUserId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropIndex(
                name: "IX_CoreOrganizationUsers_RoleId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropIndex(
                name: "IX_CoreOrganizationUsers_WorkerId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropColumn(
                name: "EmployeeType",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropColumn(
                name: "ReportsToOrganizationUserId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropColumn(
                name: "WorkerId",
                table: "CoreOrganizationUsers");
        }
    }
}
