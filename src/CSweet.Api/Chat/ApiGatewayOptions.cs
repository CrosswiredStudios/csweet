namespace CSweet.Api.Chat;

public sealed class ApiGatewayOptions
{
    public const string SectionName = "CSweet:ApiGateway";

    public string AgentId { get; set; } = "com.csweet.api-gateway";

    public string Version { get; set; } = "0.1.0";

    public string BrokerEndpoint { get; set; } = "https+http://agenthost";

    public string InstallationId { get; set; } = $"api-{Environment.MachineName}";

    public string BusinessId { get; set; } = "default";
}
