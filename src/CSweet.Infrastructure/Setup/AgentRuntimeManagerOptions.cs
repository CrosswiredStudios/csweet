namespace CSweet.Infrastructure.Setup;

public sealed class AgentRuntimeManagerOptions
{
    public const string SectionName = "CSweet:AgentRuntime";
    public const string DefaultBrokerEndpoint = "http://agenthost:8080";
    public string BrokerEndpoint { get; set; } = DefaultBrokerEndpoint;
    public string DockerNetworkName { get; set; } = "csweet-runtime";
    public string BrokerGatewayContainer { get; set; } = "agenthost";
    public int MaximumScheduleClaimsPerIteration { get; set; } = 10;
    public int InteractiveIdleTimeoutSeconds { get; set; } = 300;
}
