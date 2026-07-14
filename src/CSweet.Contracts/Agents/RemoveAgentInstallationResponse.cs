namespace CSweet.Contracts.Agents;

public sealed record RemoveAgentInstallationResponse(
    Guid InstallationId,
    bool PackageRemoved,
    bool SourceRemoved,
    int CleanupWarnings);
