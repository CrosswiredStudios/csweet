using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNativeCommunicationHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "CoreConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CoreConversations",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "CoreConversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CoreConversations_OrganizationId_ArchivedAt_UpdatedAt",
                table: "CoreConversations",
                columns: new[] { "OrganizationId", "ArchivedAt", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoreConversations_OrganizationId_ArchivedAt_UpdatedAt",
                table: "CoreConversations");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "CoreConversations");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CoreConversations");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "CoreConversations");
        }
    }
}
