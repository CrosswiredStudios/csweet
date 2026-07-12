using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoreBusinessDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRunLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PromptHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PromptPreview = table.Column<string>(type: "text", nullable: true),
                    OutputPreview = table.Column<string>(type: "text", nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TokenInputCount = table.Column<int>(type: "integer", nullable: true),
                    TokenOutputCount = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRunLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoreOrganizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Industry = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Mission = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Stage = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PrimaryGoal = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ConstraintsJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanningWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TaskDefinitionJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoreOrganizationUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PermissionLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreOrganizationUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreOrganizationUsers_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoreRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ResponsibilitiesJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    AuthorityLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreRoles_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoreStrategicObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreStrategicObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreStrategicObjectives_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoreWorkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    WorkerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExecutionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    CostModelJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    EndpointConfigurationJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresHumanApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreWorkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreWorkers_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlanningDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Content = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    StructuredJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    Summary = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsLatest = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedByTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningDocuments_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SystemPrompt = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    UserPrompt = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    OutputContent = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    OutputStructuredJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    InputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    OutputTokenCount = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningTasks_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoreWorkTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategicObjectiveId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedWorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreWorkTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreWorkTasks_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoreWorkTasks_CoreRoles_AssignedRoleId",
                        column: x => x.AssignedRoleId,
                        principalTable: "CoreRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CoreWorkTasks_CoreStrategicObjectives_StrategicObjectiveId",
                        column: x => x.StrategicObjectiveId,
                        principalTable: "CoreStrategicObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CoreWorkTasks_CoreWorkers_AssignedWorkerId",
                        column: x => x.AssignedWorkerId,
                        principalTable: "CoreWorkers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CoreTaskRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InputJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    OutputJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    CostAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    CostCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreTaskRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreTaskRuns_CoreWorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "CoreWorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoreTaskRuns_CoreWorkers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "CoreWorkers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CoreArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Content = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreArtifacts_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoreArtifacts_CoreTaskRuns_TaskRunId",
                        column: x => x.TaskRunId,
                        principalTable: "CoreTaskRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CoreArtifacts_CoreWorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "CoreWorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CoreApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Comment = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoreApprovals_CoreArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "CoreArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoreApprovals_ArtifactId",
                table: "CoreApprovals",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreArtifacts_OrganizationId",
                table: "CoreArtifacts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreArtifacts_TaskId",
                table: "CoreArtifacts",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreArtifacts_TaskRunId",
                table: "CoreArtifacts",
                column: "TaskRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreOrganizationUsers_OrganizationId",
                table: "CoreOrganizationUsers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreRoles_OrganizationId_Name",
                table: "CoreRoles",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoreStrategicObjectives_OrganizationId",
                table: "CoreStrategicObjectives",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreTaskRuns_TaskId",
                table: "CoreTaskRuns",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreTaskRuns_WorkerId",
                table: "CoreTaskRuns",
                column: "WorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreWorkers_OrganizationId",
                table: "CoreWorkers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreWorkTasks_AssignedRoleId",
                table: "CoreWorkTasks",
                column: "AssignedRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreWorkTasks_AssignedWorkerId",
                table: "CoreWorkTasks",
                column: "AssignedWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreWorkTasks_OrganizationId",
                table: "CoreWorkTasks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CoreWorkTasks_StrategicObjectiveId",
                table: "CoreWorkTasks",
                column: "StrategicObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningDocuments_OrganizationId_DocumentType_IsLatest",
                table: "PlanningDocuments",
                columns: new[] { "OrganizationId", "DocumentType", "IsLatest" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningTasks_OrganizationId_TaskKey",
                table: "PlanningTasks",
                columns: new[] { "OrganizationId", "TaskKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningWorkflows_Key",
                table: "PlanningWorkflows",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRunLogs");

            migrationBuilder.DropTable(
                name: "CoreApprovals");

            migrationBuilder.DropTable(
                name: "CoreOrganizationUsers");

            migrationBuilder.DropTable(
                name: "PlanningDocuments");

            migrationBuilder.DropTable(
                name: "PlanningTasks");

            migrationBuilder.DropTable(
                name: "PlanningWorkflows");

            migrationBuilder.DropTable(
                name: "CoreArtifacts");

            migrationBuilder.DropTable(
                name: "CoreTaskRuns");

            migrationBuilder.DropTable(
                name: "CoreWorkTasks");

            migrationBuilder.DropTable(
                name: "CoreRoles");

            migrationBuilder.DropTable(
                name: "CoreStrategicObjectives");

            migrationBuilder.DropTable(
                name: "CoreWorkers");

            migrationBuilder.DropTable(
                name: "CoreOrganizations");
        }
    }
}
