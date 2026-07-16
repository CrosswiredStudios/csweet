namespace CSweet.Domain.Core;

public sealed class AgentMemoryNamespaceRegistration
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? UserId { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
