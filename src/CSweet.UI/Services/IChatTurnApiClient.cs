using CSweet.Contracts.Core;

namespace CSweet.UI.Services;

public interface IChatTurnApiClient
{
    Task<IReadOnlyList<ChatTurnResponse>> ListTurnsAsync(Guid organizationId, Guid chatId, CancellationToken cancellationToken = default);
    Task<ChatTurnResponse?> GetTurnAsync(Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatTurnTraceEventResponse>> GetTurnTraceAsync(Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatTurnTraceEventResponse> StreamTurnEventsAsync(Guid organizationId, Guid chatId, Guid turnId, long afterSequence = -1, CancellationToken cancellationToken = default);
    Task<ChatTurnStartResponse> RetryTurnAsync(Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default);
    Task<ChatTurnResponse> CancelTurnAsync(Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default);
}
