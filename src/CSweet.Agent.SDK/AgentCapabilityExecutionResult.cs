namespace CSweet.Agent.SDK;

public sealed record AgentCapabilityExecutionResult(
    bool Succeeded,
    string ContentType,
    byte[] Payload,
    string? Error)
{
    public static AgentCapabilityExecutionResult Success(
        byte[] payload,
        string contentType = "application/json") =>
        new(true, contentType, payload, null);

    public static AgentCapabilityExecutionResult Failure(string error) =>
        new(false, "application/json", [], error);
}
