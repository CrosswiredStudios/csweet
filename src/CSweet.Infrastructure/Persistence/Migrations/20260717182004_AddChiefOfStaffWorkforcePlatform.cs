using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChiefOfStaffWorkforcePlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CoreOrganizations",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                // Existing organizations remain active and receive an opt-in Chief setup path.
                defaultValue: "Active");

            migrationBuilder.CreateTable(
                name: "ActionProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    RiskClass = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    LimitAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessDiscoveryAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedFactsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AssumptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    MissingQuestionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SelectedPatternsJson = table.Column<string>(type: "jsonb", nullable: false),
                    NextQuestion = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessDiscoveryAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    TargetCustomersJson = table.Column<string>(type: "jsonb", nullable: true),
                    OfferingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    RevenueModel = table.Column<string>(type: "text", nullable: true),
                    JurisdictionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    OperatingStyle = table.Column<string>(type: "text", nullable: true),
                    ToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    RiskPreference = table.Column<string>(type: "text", nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProvenanceJson = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Completeness = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinancialOperatingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RevenueTarget = table.Column<decimal>(type: "numeric", nullable: true),
                    ProfitTarget = table.Column<decimal>(type: "numeric", nullable: true),
                    OwnerCompensationTarget = table.Column<decimal>(type: "numeric", nullable: true),
                    MinimumRunwayMonths = table.Column<decimal>(type: "numeric", nullable: true),
                    MaximumMonthlyWorkforceSpend = table.Column<decimal>(type: "numeric", nullable: true),
                    PerEngagementCap = table.Column<decimal>(type: "numeric", nullable: true),
                    MaximumConcurrentHires = table.Column<int>(type: "integer", nullable: true),
                    RoutingPreference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialOperatingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialOperatingProfiles_CoreOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CoreOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadershipAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadershipAssignments_CoreOrganizationUsers_OrganizationUse~",
                        column: x => x.OrganizationUserId,
                        principalTable: "CoreOrganizationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagementCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DailyCheckInLocalTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DailyDueLocalTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    WeeklyReviewDay = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WeeklyReviewLocalTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    QuietHoursStart = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    QuietHoursEnd = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    NextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workstreams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategicObjectiveId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountableManagerOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    SuccessCriteriaJson = table.Column<string>(type: "jsonb", nullable: false),
                    LifecycleStage = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ManagerTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RequiredCapabilitiesJson = table.Column<string>(type: "jsonb", nullable: false),
                    RisksJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TargetDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    BudgetCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workstreams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetReservations_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionProposals_OrganizationId_IdempotencyKey",
                table: "ActionProposals",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReservations_BudgetId",
                table: "BudgetReservations",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReservations_OrganizationId_IdempotencyKey",
                table: "BudgetReservations",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_OrganizationId_ScopeType_ScopeId_PeriodStart_Period~",
                table: "Budgets",
                columns: new[] { "OrganizationId", "ScopeType", "ScopeId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessDiscoveryAssessments_OrganizationId",
                table: "BusinessDiscoveryAssessments",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_OrganizationId",
                table: "BusinessProfiles",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperatingProfiles_OrganizationId",
                table: "FinancialOperatingProfiles",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipAssignments_OrganizationId_PositionKey",
                table: "LeadershipAssignments",
                columns: new[] { "OrganizationId", "PositionKey" },
                unique: true,
                filter: "\"EndsAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipAssignments_OrganizationUserId",
                table: "LeadershipAssignments",
                column: "OrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementCycles_OrganizationId",
                table: "ManagementCycles",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workstreams_OrganizationId_Status",
                table: "Workstreams",
                columns: new[] { "OrganizationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionProposals");

            migrationBuilder.DropTable(
                name: "BudgetReservations");

            migrationBuilder.DropTable(
                name: "BusinessDiscoveryAssessments");

            migrationBuilder.DropTable(
                name: "BusinessProfiles");

            migrationBuilder.DropTable(
                name: "FinancialOperatingProfiles");

            migrationBuilder.DropTable(
                name: "LeadershipAssignments");

            migrationBuilder.DropTable(
                name: "ManagementCycles");

            migrationBuilder.DropTable(
                name: "Workstreams");

            migrationBuilder.DropTable(
                name: "Budgets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CoreOrganizations");
        }
    }
}
