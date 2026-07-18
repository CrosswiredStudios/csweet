using System.Text.Json;
using CSweet.Application.Communications;
using CSweet.Contracts.Plugins;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Communications;

public sealed class AgentCommunicationOnboardingService(CSweetDbContext db) : IAgentCommunicationOnboardingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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
                .Include(x => x.Messages)
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

        var idempotencyKey = $"agent-onboarding:{agent.Id:D}";
        var introExists = conversation.Messages.Any(x => x.IdempotencyKey == idempotencyKey) ||
            await db.CoreConversationMessages.AnyAsync(x => x.ConversationId == conversation.Id && x.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (!introExists)
        {
            var content = await BuildIntroductionAsync(agent, cancellationToken);
            conversation.Messages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderOrganizationUserId = agent.Id,
                Role = ConversationRole.Assistant,
                Content = content,
                CorrelationId = Guid.NewGuid(),
                DeliveryIntent = CommunicationDeliveryIntent.RequestResponse,
                SourceProvider = "InApp",
                IdempotencyKey = idempotencyKey,
                CreatedAt = now
            });
            conversation.UpdatedAt = now;
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

    private async Task<string> BuildIntroductionAsync(OrganizationUser agent, CancellationToken cancellationToken)
    {
        var package = await db.AgentInstallations.AsNoTracking()
            .Where(x => x.Id == agent.AgentInstallationId)
            .Select(x => new { x.PackageVersion!.ManifestJson, x.PackageVersion.AgentName })
            .SingleAsync(cancellationToken);
        var role = agent.RoleId.HasValue
            ? await db.CoreRoles.AsNoTracking().Where(x => x.Id == agent.RoleId)
                .Select(x => new { x.Name, x.Description }).SingleOrDefaultAsync(cancellationToken)
            : null;

        PluginOnboarding onboarding;
        try { onboarding = JsonSerializer.Deserialize<PluginManifest>(package.ManifestJson, JsonOptions)?.Onboarding ?? new(); }
        catch (JsonException) { onboarding = new(); }

        var jobName = role?.Name ?? agent.DisplayName ?? package.AgentName;
        var introduction = Clean(onboarding.Introduction)
            ?? $"Thank you for hiring me as your {jobName}. I'm ready to get started and help move the work forward.";
        var question = Clean(onboarding.StartingQuestion)
            ?? $"What is the most important outcome you would like me to focus on first as your {jobName}?";
        return $"{introduction}\n\n{question}"[..Math.Min(32768, introduction.Length + question.Length + 2)];
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

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static AgentCommunicationOnboardingResult Failure(string code, string message) => new(false, code, message);
}
