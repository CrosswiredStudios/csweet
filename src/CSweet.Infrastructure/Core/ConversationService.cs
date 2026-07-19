using CSweet.Application.Core;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class ConversationService : IConversationService
{
    private readonly CSweetDbContext _dbContext;

    public ConversationService(CSweetDbContext dbContext)
    {
        _dbContext = dbContext;
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

    public Task<Guid?> GetAgentInstallationIdForEmployeeAsync(Guid organizationUserId, CancellationToken cancellationToken = default) =>
        _dbContext.CoreOrganizationUsers.Where(x => x.Id == organizationUserId && x.IsActive)
            .Select(x => x.AgentInstallationId).SingleOrDefaultAsync(cancellationToken);

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
            CreatedAt = now,
            SenderOrganizationUserId = role == ConversationRole.Assistant ? conversation.AgentOrganizationUserId : conversation.InitiatedByOrganizationUserId,
            CorrelationId = Guid.NewGuid(),
            DeliveryIntent = role == ConversationRole.Assistant ? CommunicationDeliveryIntent.Response : CommunicationDeliveryIntent.RequestResponse,
            SourceProvider = "InApp"
        };

        conversation.UpdatedAt = now;

        // Use the first user message as the conversation title (nice-to-have).
        if (conversation.Title is null && role == ConversationRole.User)
        {
            conversation.Title = content.Length <= 80 ? content : content[..80];
        }

        _dbContext.CoreConversationMessages.Add(message);
        _dbContext.MemoryCaptureOutbox.Add(new MemoryCaptureOutboxItem
        {
            Id = Guid.NewGuid(),
            ConversationMessageId = message.Id,
            Status = MemoryCaptureStatus.Pending,
            Attempts = 0,
            CreatedAt = now,
            NextAttemptAt = now
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return message.ToResponse();
    }
}
