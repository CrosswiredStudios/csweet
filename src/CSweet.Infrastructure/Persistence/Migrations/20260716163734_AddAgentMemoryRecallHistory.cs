using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentMemoryRecallHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentMemoryRecallUses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Layer = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMemoryRecallUses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemoryRecallUses_ConversationId_UsedAt",
                table: "AgentMemoryRecallUses",
                columns: new[] { "ConversationId", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMemoryRecallUses_OrganizationId_EmployeeId_MemoryId_Us~",
                table: "AgentMemoryRecallUses",
                columns: new[] { "OrganizationId", "EmployeeId", "MemoryId", "UsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentMemoryRecallUses");
        }
    }
}
