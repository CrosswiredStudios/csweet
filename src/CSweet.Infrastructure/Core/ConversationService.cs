using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class ConversationService : IConversationService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public ConversationService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<ConversationResponse?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.CoreConversations
            .SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        return conversation?.ToResponse();
    }

    public async Task<IReadOnlyList<ConversationMessageResponse>> ListMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CoreConversationMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> GetDefaultProviderProfileIdAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.LlmProviderProfiles
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return profile?.Id;
    }

    public Task<bool> IsProviderProfileEnabledAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default) =>
        _dbContext.LlmProviderProfiles.AnyAsync(
            x => x.Id == providerProfileId && x.IsEnabled,
            cancellationToken);

    public Task<Guid?> GetAgentInstallationIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default) =>
        _dbContext.CoreConversations
            .Where(x => x.Id == conversationId)
            .Select(x => x.AgentOrganizationUser!.AgentInstallationId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ConversationActionResponse> StartAsync(
        Guid organizationId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var agent = await _dbContext.CoreOrganizationUsers
            .SingleOrDefaultAsync(
                x => x.Id == request.AgentOrganizationUserId && x.OrganizationId == organizationId,
                cancellationToken);

        if (agent is null)
        {
            return new ConversationActionResponse(false, "agent_not_found",
                "The agent employee was not found in this organization.");
        }

        if (agent.EmployeeType != EmployeeType.Agent)
        {
            return new ConversationActionResponse(false, "not_an_agent",
                "Conversations can only be started with agent employees.");
        }

        // The initiator is the org's "Self" human owner. Auth is out of scope for now.
        var self = await _dbContext.CoreOrganizationUsers
            .Where(x => x.OrganizationId == organizationId && x.EmployeeType == EmployeeType.Human)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (self is null)
        {
            return new ConversationActionResponse(false, "no_owner",
                "This organization has no human owner to initiate the conversation.");
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AgentOrganizationUserId = agent.Id,
            InitiatedByOrganizationUserId = self.Id,
            Title = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.CoreConversations.Add(conversation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "conversation.started",
            "Conversation",
            conversation.Id,
            $"Conversation started with agent '{agent.DisplayName}'.",
            cancellationToken: cancellationToken);

        return new ConversationActionResponse(true, null, "Conversation started.",
            conversation.ToResponse());
    }

    public async Task<ConversationMessageResponse> AppendMessageAsync(
        Guid conversationId,
        ConversationRole role,
        string content,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.CoreConversations
            .SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation {conversationId} was not found.");

        var now = DateTimeOffset.UtcNow;
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = now
        };

        conversation.UpdatedAt = now;

        // Use the first user message as the conversation title (nice-to-have).
        if (conversation.Title is null && role == ConversationRole.User)
        {
            conversation.Title = content.Length <= 80 ? content : content[..80];
        }

        _dbContext.CoreConversationMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return message.ToResponse();
    }
}
