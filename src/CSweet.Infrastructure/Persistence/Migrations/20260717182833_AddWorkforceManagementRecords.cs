using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkforceManagementRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatternKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LifecycleStage = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ApplicableBusinessTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                    JurisdictionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    WorkstreamsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TeamRecipeJson = table.Column<string>(type: "jsonb", nullable: false),
                    RisksJson = table.Column<string>(type: "jsonb", nullable: false),
                    FinancialConsiderationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagementCheckInRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagementCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedFromOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckInType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TopicsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementCheckInRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagementStatusReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagementCheckInRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    OutcomesJson = table.Column<string>(type: "jsonb", nullable: false),
                    BlockersJson = table.Column<string>(type: "jsonb", nullable: false),
                    RisksJson = table.Column<string>(type: "jsonb", nullable: false),
                    DecisionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementStatusReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceNeedReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagementStatusReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkstreamId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReporterOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BusinessOutcome = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceNeedReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceNeeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstreamId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredCapabilitiesJson = table.Column<string>(type: "jsonb", nullable: false),
                    BusinessOutcome = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MandatoryHuman = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceNeeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Responsibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstreamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Responsibilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffingActionProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkforcePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CandidateSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CandidateId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffingActionProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkforceCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkforcePlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalCandidateId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IsHuman = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    ExplanationJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkforceCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkforcePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstreamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Objective = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AssignmentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RejectedAlternativesJson = table.Column<string>(type: "jsonb", nullable: false),
                    EstimatedMonthlyCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkforcePlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPatterns_PatternKey_Version",
                table: "BusinessPatterns",
                columns: new[] { "PatternKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagementCheckInRequests_OrganizationId_Status_DueAt",
                table: "ManagementCheckInRequests",
                columns: new[] { "OrganizationId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementStatusReports_ManagementCheckInRequestId",
                table: "ManagementStatusReports",
                column: "ManagementCheckInRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceNeedReports_OrganizationId_Status_ReportedAt",
                table: "ResourceNeedReports",
                columns: new[] { "OrganizationId", "Status", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceNeeds_OrganizationId_Status",
                table: "ResourceNeeds",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Responsibilities_OrganizationId_OrganizationUserId_Status",
                table: "Responsibilities",
                columns: new[] { "OrganizationId", "OrganizationUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffingActionProposals_OrganizationId_Status",
                table: "StaffingActionProposals",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforceCandidates_OrganizationId_Source_ExternalCandidate~",
                table: "WorkforceCandidates",
                columns: new[] { "OrganizationId", "Source", "ExternalCandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkforcePlans_OrganizationId_Status",
                table: "WorkforcePlans",
                columns: new[] { "OrganizationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessPatterns");

            migrationBuilder.DropTable(
                name: "ManagementCheckInRequests");

            migrationBuilder.DropTable(
                name: "ManagementStatusReports");

            migrationBuilder.DropTable(
                name: "ResourceNeedReports");

            migrationBuilder.DropTable(
                name: "ResourceNeeds");

            migrationBuilder.DropTable(
                name: "Responsibilities");

            migrationBuilder.DropTable(
                name: "StaffingActionProposals");

            migrationBuilder.DropTable(
                name: "WorkforceCandidates");

            migrationBuilder.DropTable(
                name: "WorkforcePlans");
        }
    }
}
