namespace CSweet.Contracts.Core;

public sealed record WorkerResponse(
    Guid Id,
    Guid? OrganizationId,
    string Name,
    string Description,
    int WorkerType,
    int ExecutionMode,
    string CapabilitiesJson,
    string? CostModelJson,
    string? EndpointConfigurationJson,
    bool IsEnabled,
    bool RequiresHumanApproval,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
