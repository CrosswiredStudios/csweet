using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateAgentCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__AgentConversationMerge" (
                    "DuplicateId" uuid PRIMARY KEY,
                    "CanonicalId" uuid NOT NULL
                ) ON COMMIT DROP;

                WITH ranked AS (
                    SELECT
                        conversation."Id",
                        FIRST_VALUE(conversation."Id") OVER (
                            PARTITION BY conversation."OrganizationId",
                                conversation."InitiatedByOrganizationUserId",
                                conversation."AgentOrganizationUserId"
                            ORDER BY
                                CASE WHEN onboarding."ConversationId" IS NOT NULL THEN 0 ELSE 1 END,
                                CASE WHEN conversation."IsDeletionProtected" THEN 0 ELSE 1 END,
                                conversation."CreatedAt",
                                conversation."Id") AS "CanonicalId"
                    FROM "CoreConversations" AS conversation
                    LEFT JOIN "AgentOnboardingEventOutbox" AS onboarding
                        ON onboarding."ConversationId" = conversation."Id"
                    WHERE conversation."Kind" = 'DirectHumanAgent'
                        AND conversation."AgentOrganizationUserId" IS NOT NULL
                )
                INSERT INTO "__AgentConversationMerge" ("DuplicateId", "CanonicalId")
                SELECT "Id", "CanonicalId"
                FROM ranked
                WHERE "Id" <> "CanonicalId";

                WITH participant_state AS (
                    SELECT
                        merge."CanonicalId",
                        participant."OrganizationUserId",
                        MAX(participant."LastReadMessageSequence") AS "LastReadMessageSequence",
                        MIN(participant."JoinedAt") AS "JoinedAt",
                        BOOL_OR(participant."LeftAt" IS NULL) AS "IsActive",
                        BOOL_OR(participant."Role" = 'Coordinator') AS "IsCoordinator"
                    FROM "ConversationParticipants" AS participant
                    JOIN "__AgentConversationMerge" AS merge
                        ON merge."DuplicateId" = participant."ConversationId"
                    GROUP BY merge."CanonicalId", participant."OrganizationUserId"
                )
                UPDATE "ConversationParticipants" AS canonical
                SET
                    "LastReadMessageSequence" = GREATEST(canonical."LastReadMessageSequence", state."LastReadMessageSequence"),
                    "JoinedAt" = LEAST(canonical."JoinedAt", state."JoinedAt"),
                    "LeftAt" = CASE WHEN state."IsActive" THEN NULL ELSE canonical."LeftAt" END,
                    "Role" = CASE WHEN state."IsCoordinator" THEN 'Coordinator' ELSE canonical."Role" END
                FROM participant_state AS state
                WHERE canonical."ConversationId" = state."CanonicalId"
                    AND canonical."OrganizationUserId" = state."OrganizationUserId";

                DELETE FROM "ConversationParticipants" AS duplicate
                USING "__AgentConversationMerge" AS merge
                WHERE duplicate."ConversationId" = merge."DuplicateId"
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM "ConversationParticipants" AS canonical
                            WHERE canonical."ConversationId" = merge."CanonicalId"
                                AND canonical."OrganizationUserId" = duplicate."OrganizationUserId")
                        OR EXISTS (
                            SELECT 1
                            FROM "ConversationParticipants" AS earlier
                            JOIN "__AgentConversationMerge" AS earlier_merge
                                ON earlier_merge."DuplicateId" = earlier."ConversationId"
                            WHERE earlier_merge."CanonicalId" = merge."CanonicalId"
                                AND earlier."OrganizationUserId" = duplicate."OrganizationUserId"
                                AND earlier."Id" < duplicate."Id"));

                UPDATE "ConversationParticipants" AS participant
                SET "ConversationId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE participant."ConversationId" = merge."DuplicateId";

                UPDATE "CoreConversationMessages" AS item
                SET "ConversationId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE item."ConversationId" = merge."DuplicateId";

                UPDATE "ChatTurns" AS item
                SET "ConversationId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE item."ConversationId" = merge."DuplicateId";

                UPDATE "AgentMemoryRecallUses" AS item
                SET "ConversationId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE item."ConversationId" = merge."DuplicateId";

                UPDATE "AgentOnboardingEventOutbox" AS item
                SET "ConversationId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE item."ConversationId" = merge."DuplicateId";

                UPDATE "ExecutiveBriefingDeliveries" AS item
                SET "ConversationId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE item."ConversationId" = merge."DuplicateId";

                UPDATE "CommunicationEventOutbox" AS item
                SET "ChatId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE item."ChatId" = merge."DuplicateId";

                UPDATE "ApplicationRealtimeOutbox" AS item
                SET "ChatId" = merge."CanonicalId"
                FROM "__AgentConversationMerge" AS merge
                WHERE item."ChatId" = merge."DuplicateId";

                WITH merged_timestamps AS (
                    SELECT
                        merge."CanonicalId",
                        MIN(duplicate."CreatedAt") AS "CreatedAt",
                        MAX(duplicate."UpdatedAt") AS "UpdatedAt"
                    FROM "__AgentConversationMerge" AS merge
                    JOIN "CoreConversations" AS duplicate ON duplicate."Id" = merge."DuplicateId"
                    GROUP BY merge."CanonicalId"
                )
                UPDATE "CoreConversations" AS canonical
                SET
                    "CreatedAt" = LEAST(canonical."CreatedAt", timestamps."CreatedAt"),
                    "UpdatedAt" = GREATEST(canonical."UpdatedAt", timestamps."UpdatedAt"),
                    "ArchivedAt" = NULL,
                    "IsPrivate" = TRUE,
                    "IsDeletionProtected" = TRUE
                FROM merged_timestamps AS timestamps
                WHERE canonical."Id" = timestamps."CanonicalId";

                DELETE FROM "CoreConversations" AS duplicate
                USING "__AgentConversationMerge" AS merge
                WHERE duplicate."Id" = merge."DuplicateId";

                UPDATE "CoreConversations"
                SET "ArchivedAt" = NULL, "IsPrivate" = TRUE, "IsDeletionProtected" = TRUE
                WHERE "Kind" = 'DirectHumanAgent' AND "AgentOrganizationUserId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Conversation history consolidation is intentionally irreversible.
        }
    }
}
