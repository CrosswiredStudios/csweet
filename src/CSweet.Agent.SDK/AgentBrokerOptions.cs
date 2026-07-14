namespace CSweet.Agent.SDK;

public sealed class AgentBrokerOptions
{
    public const string SectionName = "CSweet:Agent";

    public string BrokerEndpoint { get; set; } = "https+http://agenthost";

    public string InstallationId { get; set; } = $"local-{Environment.MachineName}";

    public string BusinessId { get; set; } = "default";

    public string ManifestPath { get; set; } = "csweet-agent.json";

    public string RuntimeInstanceId { get; set; } = string.Empty;
    public string TickId { get; set; } = string.Empty;
    public string WorkloadToken { get; set; } = string.Empty;
}
