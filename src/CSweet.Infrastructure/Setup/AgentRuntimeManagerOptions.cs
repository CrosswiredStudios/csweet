namespace CSweet.Infrastructure.Setup;

public sealed class AgentRuntimeManagerOptions
{
    public const string SectionName = "CSweet:AgentRuntime";
    public string BrokerEndpoint { get; set; } = "http://agenthost:8080";
    public string DockerNetworkName { get; set; } = "csweet_default";
    public int MaximumScheduleClaimsPerIteration { get; set; } = 10;
}
