namespace CSweet.Domain.Core;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AgentOrganizationUserId { get; set; }
    public Guid InitiatedByOrganizationUserId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
    public OrganizationUser? AgentOrganizationUser { get; set; }
    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
