using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationSecurityAuditLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorAgentId",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorApplicationUserId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorDisplayName",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorInstallationId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorKind",
                table: "AuditEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorOrganizationUserId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorPackageId",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorPackageVersion",
                table: "AuditEvents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorRuntimeInstanceId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorSessionId",
                table: "AuditEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorTickId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "AuditEvents",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "AuditEvents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditEvents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "AuditEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "AuditEvents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "AuditEvents",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalMessageId",
                table: "AuditEvents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRequestId",
                table: "AuditEvents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IdentityVerified",
                table: "AuditEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntegritySeal",
                table: "AuditEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntegrityVersion",
                table: "AuditEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "AuditEvents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "AuditEvents",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentEventId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadPreview",
                table: "AuditEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadSha256",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PayloadSize",
                table: "AuditEvents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayloadTruncated",
                table: "AuditEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreviousRecordHash",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordHash",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemotePeer",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "AuditEvents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "TargetAgentId",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetDisplayName",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetInstallationId",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetKind",
                table: "AuditEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetSessionId",
                table: "AuditEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TraceId",
                table: "AuditEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CorrelationId",
                table: "AuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OrganizationId_Category_Sequence",
                table: "AuditEvents",
                columns: new[] { "OrganizationId", "Category", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OrganizationId_Sequence",
                table: "AuditEvents",
                columns: new[] { "OrganizationId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ParentEventId",
                table: "AuditEvents",
                column: "ParentEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Sequence",
                table: "AuditEvents",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TraceId",
                table: "AuditEvents",
                column: "TraceId");

            migrationBuilder.Sql("""
                UPDATE "AuditEvents"
                SET "Category" = 'Domain',
                    "Direction" = 'Internal',
                    "Outcome" = 'Completed',
                    "ActorKind" = 'Unknown',
                    "OccurredAt" = "CreatedAt",
                    "TraceId" = "Id",
                    "IntegrityVersion" = 0
                WHERE "Category" = '';

                CREATE OR REPLACE FUNCTION csweet_reject_audit_event_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Security audit ledger records are append-only';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_audit_events_append_only
                BEFORE UPDATE OR DELETE ON "AuditEvents"
                FOR EACH ROW EXECUTE FUNCTION csweet_reject_audit_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_events_append_only ON "AuditEvents";
                DROP FUNCTION IF EXISTS csweet_reject_audit_event_mutation();
                """);

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_CorrelationId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_OrganizationId_Category_Sequence",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_OrganizationId_Sequence",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ParentEventId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Sequence",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_TraceId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorAgentId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorApplicationUserId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorDisplayName",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorInstallationId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorKind",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorOrganizationUserId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorPackageId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorPackageVersion",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorRuntimeInstanceId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorSessionId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorTickId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ExternalMessageId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ExternalRequestId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IdentityVerified",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IntegritySeal",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IntegrityVersion",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ParentEventId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "PayloadPreview",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "PayloadSha256",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "PayloadSize",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "PayloadTruncated",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "PreviousRecordHash",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "RecordHash",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "RemotePeer",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TargetAgentId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TargetDisplayName",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TargetInstallationId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TargetKind",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TargetSessionId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "AuditEvents");
        }
    }
}
