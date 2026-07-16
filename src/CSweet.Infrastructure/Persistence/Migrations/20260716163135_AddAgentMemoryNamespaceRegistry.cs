using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentMemoryNamespaceRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentMemoryNamespaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartitionKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMemoryNamespaces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemoryNamespaces_OrganizationId_EmployeeId_UserId",
                table: "AgentMemoryNamespaces",
                columns: new[] { "OrganizationId", "EmployeeId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemoryNamespaces_PartitionKey",
                table: "AgentMemoryNamespaces",
                column: "PartitionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentMemoryNamespaces");
        }
    }
}
