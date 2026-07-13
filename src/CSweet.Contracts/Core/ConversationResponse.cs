namespace CSweet.Contracts.Core;

public sealed record ConversationResponse(
    Guid Id,
    Guid OrganizationId,
    Guid AgentOrganizationUserId,
    Guid InitiatedByOrganizationUserId,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
