namespace CSweet.Application.Setup;

public interface IAgentRuntimeManager
{
    Task<int> ProcessDueSchedulesAsync(CancellationToken cancellationToken = default);
    Task<int> ReconcileAsync(CancellationToken cancellationToken = default);
}

public interface IAgentRuntimeSignalService
{
    Task RecordBrokerRegistrationAsync(
        Guid runtimeInstanceId,
        Guid tickId,
        Guid installationId,
        string workloadToken,
        CancellationToken cancellationToken = default);

    Task RecordCompletionAsync(
        Guid runtimeInstanceId,
        Guid tickId,
        Guid installationId,
        string payloadJson,
        CancellationToken cancellationToken = default);
}
