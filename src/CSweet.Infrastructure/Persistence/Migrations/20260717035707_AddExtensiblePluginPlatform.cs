using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtensiblePluginPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_OrganizationUserId_Provider",
                table: "NotificationPreferences");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationConnections_OrganizationId_Provider",
                table: "CommunicationConnections");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationConnections_Provider_WorkspaceExternalId",
                table: "CommunicationConnections");

            migrationBuilder.AddColumn<string>(
                name: "ProviderKey",
                table: "NotificationPreferences",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManagedRootExternalId",
                table: "CommunicationConnections",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PluginInstallationId",
                table: "CommunicationConnections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderKey",
                table: "CommunicationConnections",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "NotificationPreferences"
                SET "ProviderKey" = CASE "Provider"
                    WHEN 'InApp' THEN 'in-app'
                    ELSE lower("Provider") END;

                UPDATE "CommunicationConnections"
                SET "ProviderKey" = CASE "Provider"
                        WHEN 'InApp' THEN 'in-app'
                        ELSE lower("Provider") END,
                    "Status" = CASE WHEN "Provider" = 'Discord' THEN 'Degraded' ELSE "Status" END,
                    "ConfigurationJson" = CASE WHEN "Provider" = 'Discord'
                        THEN jsonb_set(coalesce("ConfigurationJson", '{}'::jsonb), '{migrationRequired}', 'true'::jsonb, true)
                        ELSE "ConfigurationJson" END;
                """);

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "CommunicationConnections");

            migrationBuilder.DropColumn(
                name: "RelayPairingId",
                table: "CommunicationConnections");

            migrationBuilder.AddColumn<string>(
                name: "ManifestFileName",
                table: "AgentPackageVersions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "csweet-agent.json");

            migrationBuilder.AddColumn<string>(
                name: "PluginKind",
                table: "AgentPackageVersions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Agent");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "AgentInstallations",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Organization");

            migrationBuilder.AddColumn<string>(
                name: "RequestedCapabilitiesJson",
                table: "AgentInstallationGrants",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "CommunicationIngressReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ResultMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationIngressReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PluginOrganizationGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginOrganizationGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginOrganizationGrants_AgentInstallations_PluginInstallat~",
                        column: x => x.PluginInstallationId,
                        principalTable: "AgentInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginSecrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProtectedValue = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginSecrets_AgentInstallations_PluginInstallationId",
                        column: x => x.PluginInstallationId,
                        principalTable: "AgentInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_OrganizationUserId_ProviderKey",
                table: "NotificationPreferences",
                columns: new[] { "OrganizationUserId", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConnections_OrganizationId_ProviderKey",
                table: "CommunicationConnections",
                columns: new[] { "OrganizationId", "ProviderKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConnections_PluginInstallationId_OrganizationI~",
                table: "CommunicationConnections",
                columns: new[] { "PluginInstallationId", "OrganizationId", "ProviderKey", "WorkspaceExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationIngressReceipts_PluginInstallationId_Idempoten~",
                table: "CommunicationIngressReceipts",
                columns: new[] { "PluginInstallationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_PluginInstallationId_ApplicationUserId",
                table: "ExternalIdentities",
                columns: new[] { "PluginInstallationId", "ApplicationUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_PluginInstallationId_ProviderKey_Externa~",
                table: "ExternalIdentities",
                columns: new[] { "PluginInstallationId", "ProviderKey", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginOrganizationGrants_PluginInstallationId_OrganizationId",
                table: "PluginOrganizationGrants",
                columns: new[] { "PluginInstallationId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginSecrets_PluginInstallationId_Key",
                table: "PluginSecrets",
                columns: new[] { "PluginInstallationId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationIngressReceipts");

            migrationBuilder.DropTable(
                name: "ExternalIdentities");

            migrationBuilder.DropTable(
                name: "PluginOrganizationGrants");

            migrationBuilder.DropTable(
                name: "PluginSecrets");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_OrganizationUserId_ProviderKey",
                table: "NotificationPreferences");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationConnections_OrganizationId_ProviderKey",
                table: "CommunicationConnections");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationConnections_PluginInstallationId_OrganizationI~",
                table: "CommunicationConnections");

            migrationBuilder.DropColumn(
                name: "ManagedRootExternalId",
                table: "CommunicationConnections");

            migrationBuilder.DropColumn(
                name: "PluginInstallationId",
                table: "CommunicationConnections");

            migrationBuilder.DropColumn(
                name: "ManifestFileName",
                table: "AgentPackageVersions");

            migrationBuilder.DropColumn(
                name: "PluginKind",
                table: "AgentPackageVersions");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "AgentInstallations");

            migrationBuilder.DropColumn(
                name: "RequestedCapabilitiesJson",
                table: "AgentInstallationGrants");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "NotificationPreferences",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "CommunicationConnections",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "NotificationPreferences"
                SET "Provider" = CASE "ProviderKey"
                    WHEN 'in-app' THEN 'InApp'
                    ELSE initcap("ProviderKey") END;

                UPDATE "CommunicationConnections"
                SET "Provider" = CASE "ProviderKey"
                    WHEN 'discord' THEN 'Discord'
                    WHEN 'slack' THEN 'Slack'
                    WHEN 'whatsapp' THEN 'WhatsApp'
                    WHEN 'in-app' THEN 'InApp'
                    ELSE initcap("ProviderKey") END;
                """);

            migrationBuilder.DropColumn(
                name: "ProviderKey",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "ProviderKey",
                table: "CommunicationConnections");

            migrationBuilder.AddColumn<string>(
                name: "RelayPairingId",
                table: "CommunicationConnections",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_OrganizationUserId_Provider",
                table: "NotificationPreferences",
                columns: new[] { "OrganizationUserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConnections_OrganizationId_Provider",
                table: "CommunicationConnections",
                columns: new[] { "OrganizationId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConnections_Provider_WorkspaceExternalId",
                table: "CommunicationConnections",
                columns: new[] { "Provider", "WorkspaceExternalId" },
                unique: true);
        }
    }
}
