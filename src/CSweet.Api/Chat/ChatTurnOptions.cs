namespace CSweet.Api.Chat;

public sealed class ChatTurnOptions
{
    public TimeSpan HardTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan AgentResponseStartTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan FirstOutputTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan DirectFallbackTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan MemoryOperationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan StreamHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan CapabilityRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan CapabilityRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
