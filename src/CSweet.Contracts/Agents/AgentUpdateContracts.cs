namespace CSweet.Contracts.Agents;

public sealed record AgentUpdateAvailabilityResponse(
    Guid InstallationId,
    string AgentId,
    string AgentName,
    string BusinessId,
    string CurrentVersion,
    string CurrentCommitSha,
    bool UpdateAvailable,
    Guid? AvailablePackageVersionId,
    string? AvailableVersion,
    string? AvailableCommitSha,
    DateTimeOffset CheckedAt,
    string? Error = null);

public sealed record UpdateAgentInstallationRequest(Guid PackageVersionId);
