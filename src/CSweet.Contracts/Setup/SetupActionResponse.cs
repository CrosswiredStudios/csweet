namespace CSweet.Contracts.Setup;

public sealed record SetupActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    SetupStatusResponse? Status);
