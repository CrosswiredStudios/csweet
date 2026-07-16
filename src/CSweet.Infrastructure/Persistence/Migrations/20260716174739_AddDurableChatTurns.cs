using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableChatTurns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChatTurnId",
                table: "CoreConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetryOfTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    NextTraceSequence = table.Column<long>(type: "bigint", nullable: false),
                    PartialResponse = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstOutputAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResponseReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatTurns_CoreConversationMessages_AssistantMessageId",
                        column: x => x.AssistantMessageId,
                        principalTable: "CoreConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatTurns_CoreConversationMessages_UserMessageId",
                        column: x => x.UserMessageId,
                        principalTable: "CoreConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatTurns_CoreConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "CoreConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatTurnTraceEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Sensitivity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTurnTraceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatTurnTraceEvents_ChatTurns_ChatTurnId",
                        column: x => x.ChatTurnId,
                        principalTable: "ChatTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoreConversationMessages_ChatTurnId",
                table: "CoreConversationMessages",
                column: "ChatTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatTurns_AssistantMessageId",
                table: "ChatTurns",
                column: "AssistantMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatTurns_ConversationId_CreatedAt",
                table: "ChatTurns",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTurns_Status_CreatedAt",
                table: "ChatTurns",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTurns_UserMessageId",
                table: "ChatTurns",
                column: "UserMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatTurnTraceEvents_ChatTurnId_Sequence",
                table: "ChatTurnTraceEvents",
                columns: new[] { "ChatTurnId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatTurnTraceEvents");

            migrationBuilder.DropTable(
                name: "ChatTurns");

            migrationBuilder.DropIndex(
                name: "IX_CoreConversationMessages_ChatTurnId",
                table: "CoreConversationMessages");

            migrationBuilder.DropColumn(
                name: "ChatTurnId",
                table: "CoreConversationMessages");
        }
    }
}
