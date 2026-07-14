namespace CSweet.Domain.Setup;

public enum AgentBuildStatus
{
    Queued,
    Cloning,
    Building,
    Succeeded,
    Failed,
    Cancelled
}
