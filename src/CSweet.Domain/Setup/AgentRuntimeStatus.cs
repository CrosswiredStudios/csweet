namespace CSweet.Domain.Setup;

public enum AgentRuntimeStatus
{
    Queued,
    Starting,
    WaitingForBrokerRegistration,
    Running,
    CompletionReported,
    Stopping,
    Completed,
    StartFailed,
    BrokerRegistrationTimedOut,
    RuntimeTimedOut,
    ExitedWithoutCompletion,
    Failed,
    Cancelled,
    PolicyDenied,
    Skipped
}
