using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveAgentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_AgentRuntimeInstances_ActiveInstallation",
                table: "AgentRuntimeInstances",
                column: "AgentInstallationId",
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Starting', 'WaitingForBrokerRegistration', 'Running', 'CompletionReported', 'Stopping')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AgentRuntimeInstances_ActiveInstallation",
                table: "AgentRuntimeInstances");
        }
    }
}
