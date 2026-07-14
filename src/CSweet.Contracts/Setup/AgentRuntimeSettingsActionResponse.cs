namespace CSweet.Contracts.Setup;

public sealed record AgentRuntimeSettingsActionResponse(
    bool Succeeded,
    string? Message,
    AgentRuntimeSettingsResponse? Settings);
