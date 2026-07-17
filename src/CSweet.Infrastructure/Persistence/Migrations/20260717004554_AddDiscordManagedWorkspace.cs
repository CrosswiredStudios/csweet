using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordManagedWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "CoreOrganizationUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CoreOrganizationUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentOrganizationUserId",
                table: "CoreConversations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "CoreConversations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "CoreConversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "CoreConversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CausationId",
                table: "CoreConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "CoreConversationMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DeliveryIntent",
                table: "CoreConversationMessages",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HopCount",
                table: "CoreConversationMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "CoreConversationMessages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "CoreConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SenderOrganizationUserId",
                table: "CoreConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceChannelExternalId",
                table: "CoreConversationMessages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceProvider",
                table: "CoreConversationMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetAgentOrganizationUserId",
                table: "ChatTurns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE \"CoreOrganizationUsers\" SET \"IsActive\" = TRUE;");
            migrationBuilder.Sql("UPDATE \"CoreConversations\" SET \"Kind\" = 'DirectHumanAgent';");
            migrationBuilder.Sql("""
                UPDATE "CoreConversationMessages"
                SET "CorrelationId" = "Id",
                    "DeliveryIntent" = CASE WHEN "Role" = 'User' THEN 'RequestResponse' ELSE 'Response' END,
                    "SourceProvider" = 'InApp';
                """);
            migrationBuilder.Sql("""
                UPDATE "ChatTurns" AS turn
                SET "TargetAgentOrganizationUserId" = conversation."AgentOrganizationUserId"
                FROM "CoreConversations" AS conversation
                WHERE turn."ConversationId" = conversation."Id";
                """);

            migrationBuilder.CreateTable(
                name: "CommunicationConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    WorkspaceExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkspaceMode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RelayPairingId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExternalReceiptId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastError = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_CoreConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "CoreConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_CoreOrganizationUsers_Organization~",
                        column: x => x.OrganizationUserId,
                        principalTable: "CoreOrganizationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIdentityLinkCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentityLinkCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveDirectAgentOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentityLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalMessageReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MessageExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ThreadExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsInbound = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalMessageReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagedExternalResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ParentExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedExternalResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    QuietHoursStart = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    QuietHoursEnd = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MinimumSeverity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginatingAgentOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    ActionUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DismissedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoreConversationMessages_IdempotencyKey",
                table: "CoreConversationMessages",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConnections_OrganizationId_Provider",
                table: "CommunicationConnections",
                columns: new[] { "OrganizationId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConnections_Provider_WorkspaceExternalId",
                table: "CommunicationConnections",
                columns: new[] { "Provider", "WorkspaceExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeliveries_IdempotencyKey",
                table: "CommunicationDeliveries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeliveries_Status_NextAttemptAt",
                table: "CommunicationDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ConversationId_OrganizationUserId",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "OrganizationUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_OrganizationUserId",
                table: "ConversationParticipants",
                column: "OrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinkCodes_CodeHash",
                table: "ExternalIdentityLinkCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinkCodes_ConnectionId_ExpiresAt",
                table: "ExternalIdentityLinkCodes",
                columns: new[] { "ConnectionId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_ConnectionId_ExternalUserId",
                table: "ExternalIdentityLinks",
                columns: new[] { "ConnectionId", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_ConnectionId_OrganizationUserId",
                table: "ExternalIdentityLinks",
                columns: new[] { "ConnectionId", "OrganizationUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalMessageReferences_ConnectionId_MessageExternalId",
                table: "ExternalMessageReferences",
                columns: new[] { "ConnectionId", "MessageExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagedExternalResources_ConnectionId_ExternalId",
                table: "ManagedExternalResources",
                columns: new[] { "ConnectionId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagedExternalResources_ConnectionId_OrganizationUserId_Ki~",
                table: "ManagedExternalResources",
                columns: new[] { "ConnectionId", "OrganizationUserId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_OrganizationUserId_Provider",
                table: "NotificationPreferences",
                columns: new[] { "OrganizationUserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_OrganizationId_DeduplicationKey",
                table: "UserNotifications",
                columns: new[] { "OrganizationId", "DeduplicationKey" },
                unique: true,
                filter: "\"DeduplicationKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_RecipientOrganizationUserId_ReadAt_Create~",
                table: "UserNotifications",
                columns: new[] { "RecipientOrganizationUserId", "ReadAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationConnections");

            migrationBuilder.DropTable(
                name: "CommunicationDeliveries");

            migrationBuilder.DropTable(
                name: "ConversationParticipants");

            migrationBuilder.DropTable(
                name: "ExternalIdentityLinkCodes");

            migrationBuilder.DropTable(
                name: "ExternalIdentityLinks");

            migrationBuilder.DropTable(
                name: "ExternalMessageReferences");

            migrationBuilder.DropTable(
                name: "ManagedExternalResources");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_CoreConversationMessages_IdempotencyKey",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CoreOrganizationUsers");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CoreConversations");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "CoreConversations");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "CoreConversations");

            migrationBuilder.DropColumn(
                name: "CausationId",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "DeliveryIntent",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "HopCount",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "SenderOrganizationUserId",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "SourceChannelExternalId",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "SourceProvider",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "TargetAgentOrganizationUserId",
                table: "ChatTurns");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentOrganizationUserId",
                table: "CoreConversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
