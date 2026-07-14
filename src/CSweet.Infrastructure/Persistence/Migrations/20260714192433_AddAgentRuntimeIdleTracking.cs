using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRuntimeIdleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IdleDeadlineAt",
                table: "AgentRuntimeInstances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInteractive",
                table: "AgentRuntimeInstances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastInteractiveActivityAt",
                table: "AgentRuntimeInstances",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdleDeadlineAt",
                table: "AgentRuntimeInstances");

            migrationBuilder.DropColumn(
                name: "IsInteractive",
                table: "AgentRuntimeInstances");

            migrationBuilder.DropColumn(
                name: "LastInteractiveActivityAt",
                table: "AgentRuntimeInstances");
        }
    }
}
