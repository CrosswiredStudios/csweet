namespace CSweet.Contracts.Agents;

public sealed record InstallAgentRequest(
    string BusinessId,
    string ActivationMode,
    int TickFrequencySeconds,
    string OverlapPolicy,
    IReadOnlyList<string> GrantedCapabilities,
    IReadOnlyList<string> GrantedSubscriptions,
    IReadOnlyList<string> GrantedPublications,
    IReadOnlyList<string> GrantedPermissions,
    IReadOnlyList<string> GrantedNetworkAccess,
    int MaxRuntimeSeconds,
    int MemoryMb,
    int CpuPercent);