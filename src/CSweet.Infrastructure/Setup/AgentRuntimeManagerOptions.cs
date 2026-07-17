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

    public static string ResolveBrokerEndpoint(
        string configuredEndpoint,
        string? discoveredHttpEndpoint,
        string? discoveredHttpsEndpoint)
    {
        if (!string.Equals(configuredEndpoint, DefaultBrokerEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            return configuredEndpoint;
        }

        var discoveredEndpoint = discoveredHttpEndpoint ?? discoveredHttpsEndpoint;
        if (!Uri.TryCreate(discoveredEndpoint, UriKind.Absolute, out var uri))
        {
            return configuredEndpoint;
        }

        if (uri.Host is "localhost" or "127.0.0.1" or "::1")
        {
            var builder = new UriBuilder(uri)
            {
                Host = "host.docker.internal"
            };
            return builder.Uri.ToString().TrimEnd('/');
        }

        return uri.ToString().TrimEnd('/');
    }
}
