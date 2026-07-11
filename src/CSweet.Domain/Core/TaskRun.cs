namespace CSweet.Domain.Core;

public sealed class TaskRun
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid? WorkerId { get; set; }
    public TaskRunStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? FailureMessage { get; set; }
    public decimal? CostAmount { get; set; }
    public string? CostCurrency { get; set; }

    // Navigation
    public WorkTask? Task { get; set; }
    public Worker? Worker { get; set; }
}
