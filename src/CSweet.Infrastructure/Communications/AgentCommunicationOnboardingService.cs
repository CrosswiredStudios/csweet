using CSweet.Application.Communications;
using CSweet.Domain.Communications;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Communications;

public sealed class AgentCommunicationOnboardingService(CSweetDbContext db) : IAgentCommunicationOnboardingService
{
    public async Task<AgentCommunicationOnboardingResult> EnsureAsync(
        Guid organizationId,
        OrganizationUser agent,
        Guid? hiringApplicationUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (agent.OrganizationId != organizationId || agent.EmployeeType != EmployeeType.Agent || !agent.AgentInstallationId.HasValue)
            return Failure("agent_instance_required", "An active agent instance is required for communication onboarding.");

        var hiringUser = await ResolveHiringUserAsync(organizationId, hiringApplicationUserId, cancellationToken);
        if (hiringUser is null)
            return Failure("hiring_user_not_found", "An active human owner or hiring user is required to start the agent conversation.");

        var existing = db.CoreConversations.Local.FirstOrDefault(x =>
            x.OrganizationId == organizationId && x.InitiatedByOrganizationUserId == hiringUser.Id &&
            x.AgentOrganizationUserId == agent.Id && x.Kind == ConversationKind.DirectHumanAgent)
            ?? await db.CoreConversations
                .Include(x => x.Participants)
                .SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
                    x.InitiatedByOrganizationUserId == hiringUser.Id && x.AgentOrganizationUserId == agent.Id &&
                    x.Kind == ConversationKind.DirectHumanAgent, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var conversation = existing ?? new Conversation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            InitiatedByOrganizationUserId = hiringUser.Id,
            AgentOrganizationUserId = agent.Id,
            Kind = ConversationKind.DirectHumanAgent,
            IsPrivate = true,
            IsDeletionProtected = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        conversation.IsPrivate = true;
        conversation.IsDeletionProtected = true;
        conversation.ArchivedAt = null;
        EnsureParticipant(conversation, hiringUser, ConversationParticipantRole.Coordinator, now);
        EnsureParticipant(conversation, agent, ConversationParticipantRole.Member, now);

        var eventExists = db.AgentOnboardingEventOutbox.Local.Any(x => x.AgentOrganizationUserId == agent.Id) ||
            await db.AgentOnboardingEventOutbox.AnyAsync(x => x.AgentOrganizationUserId == agent.Id, cancellationToken);
        if (!eventExists)
        {
            db.AgentOnboardingEventOutbox.Add(new AgentOnboardingEventOutboxItem
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                AgentOrganizationUserId = agent.Id,
                HiringOrganizationUserId = hiringUser.Id,
                ConversationId = conversation.Id,
                Status = AgentOnboardingEventOutboxStatus.Pending,
                NextAttemptAt = now,
                OccurredAt = now
            });
        }

        if (existing is null) db.CoreConversations.Add(conversation);
        return new AgentCommunicationOnboardingResult(true, null, "Agent conversation initialized.", conversation.Id);
    }

    private async Task<OrganizationUser?> ResolveHiringUserAsync(
        Guid organizationId,
        Guid? applicationUserId,
        CancellationToken cancellationToken)
    {
        if (applicationUserId.HasValue)
        {
            var actor = await db.CoreOrganizationUsers.SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
                x.ApplicationUserId == applicationUserId && x.EmployeeType == EmployeeType.Human && x.IsActive, cancellationToken);
            if (actor is not null) return actor;
        }

        return await db.CoreOrganizationUsers
            .Where(x => x.OrganizationId == organizationId && x.EmployeeType == EmployeeType.Human && x.IsActive)
            .OrderByDescending(x => x.PermissionLevel == OrganizationPermissionLevel.Owner)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void EnsureParticipant(Conversation conversation, OrganizationUser user,
        ConversationParticipantRole role, DateTimeOffset now)
    {
        var participant = conversation.Participants.FirstOrDefault(x => x.OrganizationUserId == user.Id);
        if (participant is null)
            conversation.Participants.Add(new ConversationParticipant
            {
                Id = Guid.NewGuid(), ConversationId = conversation.Id, OrganizationUserId = user.Id,
                OrganizationUser = user, Role = role, JoinedAt = now
            });
        else
        {
            participant.LeftAt = null;
            participant.Role = role;
        }
    }

    private static AgentCommunicationOnboardingResult Failure(string code, string message) => new(false, code, message);
}
