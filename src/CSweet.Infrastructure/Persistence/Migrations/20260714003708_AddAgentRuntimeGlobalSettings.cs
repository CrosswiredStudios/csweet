using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRuntimeGlobalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRuntimeGlobalSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnableImportedAgents = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultActivationMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultTickFrequencySeconds = table.Column<int>(type: "integer", nullable: false),
                    MinimumTickFrequencySeconds = table.Column<int>(type: "integer", nullable: false),
                    DefaultMaxRuntimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    DefaultOverlapPolicy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AllowAlwaysOnCommunityAgents = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultRestartPolicy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GlobalMaxActiveContainers = table.Column<int>(type: "integer", nullable: false),
                    PerBusinessMaxActiveContainers = table.Column<int>(type: "integer", nullable: false),
                    PerInstallationMaxActiveContainers = table.Column<int>(type: "integer", nullable: false),
                    DefaultContainerMemoryMb = table.Column<int>(type: "integer", nullable: false),
                    MaximumContainerMemoryMb = table.Column<int>(type: "integer", nullable: false),
                    DefaultContainerCpuPercent = table.Column<int>(type: "integer", nullable: false),
                    MaximumContainerCpuPercent = table.Column<int>(type: "integer", nullable: false),
                    DefaultContainerPidsLimit = table.Column<int>(type: "integer", nullable: false),
                    DefaultContainerLogLimitMb = table.Column<int>(type: "integer", nullable: false),
                    ContainerStartTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    BrokerRegistrationTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    ContainerStopGraceSeconds = table.Column<int>(type: "integer", nullable: false),
                    DefaultNetworkPolicy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AllowPublicInternetByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedPackageFeedHosts = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    BlockedNetworkCidrs = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AgentSourceRootPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    AgentPackageCachePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DotNetBuilderImage = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DotNetRuntimeBaseImage = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BuildTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    BuildMemoryMb = table.Column<int>(type: "integer", nullable: false),
                    BuildCpuPercent = table.Column<int>(type: "integer", nullable: false),
                    MaximumRepositorySizeMb = table.Column<int>(type: "integer", nullable: false),
                    MaximumBuildLogMb = table.Column<int>(type: "integer", nullable: false),
                    KeepFailedBuildWorkspaces = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedRuntimeRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    FailedRuntimeRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    BuildLogRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    RemoveContainersAfterCompletion = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveWorkspacesAfterCompletion = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuntimeGlobalSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRuntimeGlobalSettings");
        }
    }
}
