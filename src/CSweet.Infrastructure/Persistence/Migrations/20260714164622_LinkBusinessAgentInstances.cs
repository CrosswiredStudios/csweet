using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkBusinessAgentInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgentInstallationId",
                table: "CoreOrganizationUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoreOrganizationUsers_AgentInstallationId",
                table: "CoreOrganizationUsers",
                column: "AgentInstallationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CoreOrganizationUsers_AgentInstallations_AgentInstallationId",
                table: "CoreOrganizationUsers",
                column: "AgentInstallationId",
                principalTable: "AgentInstallations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoreOrganizationUsers_AgentInstallations_AgentInstallationId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropIndex(
                name: "IX_CoreOrganizationUsers_AgentInstallationId",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropColumn(
                name: "AgentInstallationId",
                table: "CoreOrganizationUsers");
        }
    }
}
