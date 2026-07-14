namespace CSweet.Domain.Setup;

public sealed class AgentRuntimeEvent
{
    public Guid Id { get; set; }
    public Guid AgentRuntimeInstanceId { get; set; }
    public AgentRuntimeStatus Status { get; set; }
    public string? Reason { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public AgentRuntimeInstance? AgentRuntimeInstance { get; set; }
}
