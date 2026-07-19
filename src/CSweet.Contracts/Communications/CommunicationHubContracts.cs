using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Communications;

public sealed record CommunicationHubResponse(
    Guid CurrentOrganizationUserId,
    bool CanManageChats,
    IReadOnlyList<CommunicationChatResponse> Chats,
    IReadOnlyList<CommunicationPersonResponse> People,
    IReadOnlyList<CommunicationAudienceResponse> Audiences);

public sealed record CommunicationChatResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsDirect,
    bool IsPrivate,
    bool IsDeletionProtected,
    bool CanManage,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CommunicationParticipantResponse> Participants,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);

public sealed record CommunicationParticipantResponse(
    Guid OrganizationUserId,
    string DisplayName,
    string EmployeeType,
    string Role);

public sealed record CommunicationPersonResponse(
    Guid Id,
    string DisplayName,
    string EmployeeType,
    Guid? RoleId,
    string? RoleName);

public sealed record CommunicationAudienceResponse(
    string Kind,
    Guid Id,
    string Name,
    IReadOnlyList<Guid> OrganizationUserIds);

public sealed record CommunicationHubMessageResponse(
    Guid Id,
    long Sequence,
    Guid ChatId,
    Guid? SenderOrganizationUserId,
    string SenderDisplayName,
    string SenderEmployeeType,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record CommunicationUnreadSummaryResponse(
    int TotalUnreadCount,
    IReadOnlyDictionary<Guid, int> ChatUnreadCounts);

public sealed record MarkCommunicationChatReadRequest(long ThroughMessageSequence);

public sealed record CreateCommunicationChatRequest(
    [property: MaxLength(256)] string? Title,
    [property: MaxLength(2048)] string? Description,
    bool IsDirect,
    bool IsPrivate,
    IReadOnlyList<Guid> ParticipantOrganizationUserIds,
    IReadOnlyList<Guid>? AudienceRoleIds = null,
    IReadOnlyList<Guid>? AudienceWorkstreamIds = null);

public sealed record UpdateCommunicationChatRequest(
    [property: Required, MaxLength(256)] string Title,
    [property: MaxLength(2048)] string? Description,
    bool IsPrivate,
    IReadOnlyList<Guid> ParticipantOrganizationUserIds,
    IReadOnlyList<Guid>? AudienceRoleIds = null,
    IReadOnlyList<Guid>? AudienceWorkstreamIds = null);

public sealed record SendCommunicationMessageRequest(
    [property: Required, MaxLength(32768)] string Content,
    [property: MaxLength(160)] string? IdempotencyKey = null);

public sealed record CommunicationHubActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string Message,
    CommunicationChatResponse? Chat = null);

public static class CommunicationHubCapabilities
{
    public const string Read = "communication.chat.read.v1";
    public const string Create = "communication.chat.create.v1";
    public const string Modify = "communication.chat.modify.v1";
    public const string Delete = "communication.chat.delete.v1";
    public const string SendMessage = "communication.message.send.v1";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Read, Create, Modify, Delete, SendMessage
    };
}
