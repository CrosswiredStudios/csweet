namespace CSweet.Domain.Core;

public sealed class AgentMemoryRecallUse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid MemoryId { get; set; }
    public string Layer { get; set; } = string.Empty;
    public DateTimeOffset UsedAt { get; set; }
}
