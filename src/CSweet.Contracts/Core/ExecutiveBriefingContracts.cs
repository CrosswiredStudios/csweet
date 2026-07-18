namespace CSweet.Contracts.Core;

public sealed record ExecutiveBriefingSettingsResponse(
    Guid OrganizationId,
    Guid ChiefOrganizationUserId,
    Guid? ManagingOrganizationUserId,
    bool IsEnabled,
    bool StartupEnabled,
    string Cadence,
    string WeeklyDay,
    string LocalTime,
    string TimeZone,
    DateTimeOffset? NextBriefingAt,
    ExecutiveBriefingHistoryItem? LatestBriefing);

public sealed record UpdateExecutiveBriefingSettingsRequest(
    Guid ManagingOrganizationUserId,
    bool IsEnabled,
    bool StartupEnabled,
    string Cadence,
    string WeeklyDay,
    string LocalTime,
    string TimeZone);

public sealed record ExecutiveBriefingActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string Message,
    Guid? RequestId = null);

public sealed record ExecutiveBriefingHistoryItem(
    Guid RequestId,
    string TriggerType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueAt,
    int DispatchAttempts,
    string? FailureCode,
    string? FailureMessage,
    Guid? RecipientOrganizationUserId,
    string? DeliveryChannel,
    string? DeliveryStatus,
    Guid? ConversationId,
    DateTimeOffset? DeliveredAt);
