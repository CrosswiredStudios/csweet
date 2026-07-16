using System.Text.Json;
using CSweet.Application.Core;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class ChatTurnService(CSweetDbContext db) : IChatTurnService
{
    private static readonly TimeSpan InitialLeaseDuration = TimeSpan.FromMinutes(3);
    private static readonly HashSet<ChatTurnStatus> ActiveStatuses =
    [ChatTurnStatus.Queued, ChatTurnStatus.RecallingMemory, ChatTurnStatus.Dispatching, ChatTurnStatus.Running, ChatTurnStatus.FinalizingMemory];

    public async Task<ChatTurnStartResponse?> StartAsync(Guid organizationId, Guid conversationId, string message, Guid? retryOfTurnId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var conversation = await db.CoreConversations.SingleOrDefaultAsync(
            x => x.Id == conversationId && x.OrganizationId == organizationId, cancellationToken);
        if (conversation is null) return null;
        if (await db.ChatTurns.AnyAsync(x => x.ConversationId == conversationId && ActiveStatuses.Contains(x.Status), cancellationToken))
            throw new InvalidOperationException("This conversation already has an active turn.");

        var now = DateTimeOffset.UtcNow;
        var turnId = Guid.NewGuid();
        var userMessage = new ConversationMessage
        {
            Id = Guid.NewGuid(), ConversationId = conversationId, ChatTurnId = turnId,
            Role = ConversationRole.User, Content = message.Trim(), CreatedAt = now
        };
        var turn = new ChatTurn
        {
            Id = turnId, OrganizationId = organizationId, ConversationId = conversationId,
            UserMessageId = userMessage.Id, RetryOfTurnId = retryOfTurnId, Status = ChatTurnStatus.Queued,
            CreatedAt = now, UpdatedAt = now, LastActivityAt = now
        };
        conversation.UpdatedAt = now;
        conversation.Title ??= userMessage.Content.Length <= 80 ? userMessage.Content : userMessage.Content[..80];
        db.CoreConversationMessages.Add(userMessage);
        db.ChatTurns.Add(turn);
        db.MemoryCaptureOutbox.Add(new MemoryCaptureOutboxItem
        {
            Id = Guid.NewGuid(), ConversationMessageId = userMessage.Id, Status = MemoryCaptureStatus.Pending,
            CreatedAt = now, NextAttemptAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        return new ChatTurnStartResponse(ToResponse(turn), userMessage.ToResponse());
    }

    public async Task<ChatTurnResponse?> GetAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default) =>
        await db.ChatTurns.Where(x => x.Id == turnId && x.OrganizationId == organizationId)
            .Select(x => ToResponse(x)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatTurnTraceEventResponse>> ListEventsAsync(Guid organizationId, Guid turnId, long afterSequence = -1, CancellationToken cancellationToken = default) =>
        await db.ChatTurnTraceEvents.Where(x => x.ChatTurnId == turnId && x.ChatTurn!.OrganizationId == organizationId && x.Sequence > afterSequence)
            .OrderBy(x => x.Sequence).Select(x => ToResponse(x)).ToListAsync(cancellationToken);

    public async Task<bool> CancelAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default)
    {
        var turn = await db.ChatTurns.SingleOrDefaultAsync(x => x.Id == turnId && x.OrganizationId == organizationId, cancellationToken);
        if (turn is null || !ActiveStatuses.Contains(turn.Status)) return false;
        turn.Status = ChatTurnStatus.Cancelled;
        turn.CompletedAt = turn.UpdatedAt = DateTimeOffset.UtcNow;
        turn.LeaseOwner = null; turn.LeaseUntil = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ChatTurnStartResponse?> RetryAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default)
    {
        var original = await db.ChatTurns.Include(x => x.UserMessage)
            .SingleOrDefaultAsync(x => x.Id == turnId && x.OrganizationId == organizationId, cancellationToken);
        if (original is null || original.Status is not (ChatTurnStatus.Failed or ChatTurnStatus.Cancelled or ChatTurnStatus.CompletedWithWarnings)) return null;
        return await StartAsync(organizationId, original.ConversationId, original.UserMessage!.Content, original.Id, cancellationToken);
    }

    public async Task<Guid?> ClaimNextAsync(string leaseOwner, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var turn = await db.ChatTurns.Where(x => x.Status == ChatTurnStatus.Queued ||
                (ActiveStatuses.Contains(x.Status) && x.LeaseUntil < now))
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (turn is null) return null;
        turn.LeaseOwner = leaseOwner;
        var recovering = turn.Status != ChatTurnStatus.Queued;
        turn.LeaseUntil = now.Add(InitialLeaseDuration);
        turn.Attempt++;
        if (recovering)
        {
            turn.PartialResponse = string.Empty;
            turn.FirstOutputAt = null;
            turn.ErrorCode = null;
            turn.ErrorMessage = null;
        }
        turn.Status = ChatTurnStatus.RecallingMemory;
        turn.StartedAt ??= now;
        turn.LastActivityAt = turn.UpdatedAt = now;
        turn.LeaseUntil = now.Add(InitialLeaseDuration);
        await db.SaveChangesAsync(cancellationToken);
        return turn.Id;
    }

    public async Task<ChatTurnTraceEventResponse> TraceAsync(Guid turnId, string category, string eventType, string status, string title, string? summary = null, object? details = null, string sensitivity = "Internal", long? durationMs = null, CancellationToken cancellationToken = default)
    {
        var turn = await db.ChatTurns.SingleAsync(x => x.Id == turnId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var traceEvent = new ChatTurnTraceEvent
        {
            Id = Guid.NewGuid(), ChatTurnId = turnId, Sequence = turn.NextTraceSequence++, Category = category,
            EventType = eventType, Status = status, Title = title, Summary = summary,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details), Sensitivity = sensitivity,
            DurationMs = durationMs, OccurredAt = now
        };
        turn.LastActivityAt = turn.UpdatedAt = now;
        db.ChatTurnTraceEvents.Add(traceEvent);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(traceEvent);
    }

    public async Task SetStatusAsync(Guid turnId, string status, string? errorCode = null, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var turn = await db.ChatTurns.SingleAsync(x => x.Id == turnId, cancellationToken);
        turn.Status = Enum.Parse<ChatTurnStatus>(status, true);
        turn.ErrorCode = errorCode; turn.ErrorMessage = errorMessage;
        var now = DateTimeOffset.UtcNow;
        turn.UpdatedAt = now;
        turn.LastActivityAt = now;
        if (turn.Status is ChatTurnStatus.Failed or ChatTurnStatus.Cancelled) turn.CompletedAt = turn.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendOutputAsync(Guid turnId, string delta, CancellationToken cancellationToken = default)
    {
        var turn = await db.ChatTurns.SingleAsync(x => x.Id == turnId, cancellationToken);
        turn.PartialResponse += delta;
        var now = DateTimeOffset.UtcNow;
        turn.FirstOutputAt ??= now;
        turn.LastActivityAt = now;
        turn.UpdatedAt = now;
        turn.LeaseUntil = now.AddMinutes(1);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid turnId, Guid assistantMessageId, bool memoryWarning, CancellationToken cancellationToken = default)
    {
        var turn = await db.ChatTurns.SingleAsync(x => x.Id == turnId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        turn.AssistantMessageId = assistantMessageId;
        turn.Status = memoryWarning ? ChatTurnStatus.CompletedWithWarnings : ChatTurnStatus.Completed;
        turn.ResponseReadyAt = turn.CompletedAt = turn.UpdatedAt = now;
        turn.LeaseOwner = null; turn.LeaseUntil = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ChatTurnResponse ToResponse(ChatTurn x) => new(
        x.Id, x.OrganizationId, x.ConversationId, x.UserMessageId, x.AssistantMessageId,
        x.Status.ToString(), x.Attempt, x.PartialResponse, x.ErrorCode, x.ErrorMessage,
        x.CreatedAt, x.StartedAt, x.FirstOutputAt, x.ResponseReadyAt, x.CompletedAt, x.NextTraceSequence - 1);

    private static ChatTurnTraceEventResponse ToResponse(ChatTurnTraceEvent x)
    {
        JsonElement? details = null;
        if (!string.IsNullOrWhiteSpace(x.DetailsJson)) details = JsonSerializer.Deserialize<JsonElement>(x.DetailsJson);
        return new(x.Id, x.ChatTurnId, x.Sequence, x.Category, x.EventType, x.Status, x.Title,
            x.Summary, details, x.Sensitivity, x.DurationMs, x.OccurredAt);
    }
}
