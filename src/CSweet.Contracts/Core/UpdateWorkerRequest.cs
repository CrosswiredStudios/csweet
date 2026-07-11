namespace CSweet.Contracts.Core;

public sealed record UpdateWorkerRequest(
    string? Name,
    string? Description,
    int? WorkerType,
    int? ExecutionMode,
    string? CapabilitiesJson,
    string? CostModelJson,
    string? EndpointConfigurationJson,
    bool? IsEnabled,
    bool? RequiresHumanApproval);
