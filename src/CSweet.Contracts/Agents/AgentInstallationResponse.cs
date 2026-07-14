namespace CSweet.Contracts.Agents;

public sealed record AgentInstallationResponse(
    Guid Id,
    Guid PackageVersionId,
    string BusinessId,
    string AgentId,
    string AgentName,
    string AgentVersion,
    string PublisherName,
    string CommitSha,
    bool IsEnabled,
    IReadOnlyList<string> GrantedCapabilities,
    IReadOnlyList<string> GrantedSubscriptions,
    IReadOnlyList<string> GrantedPublications,
    IReadOnlyList<string> GrantedPermissions,
    IReadOnlyList<string> GrantedNetworkAccess,
    int MemoryMb,
    int CpuPercent,
    AgentScheduleResponse Schedule,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);