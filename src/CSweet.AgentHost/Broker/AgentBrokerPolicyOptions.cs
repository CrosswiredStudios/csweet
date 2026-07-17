namespace CSweet.AgentHost.Broker;

public sealed class AgentBrokerPolicyOptions
{
    public const string SectionName = "CSweet:AgentBroker";

    public Dictionary<string, AgentGrantOptions> Agents { get; set; } =
        new(StringComparer.Ordinal);
}

public sealed class AgentGrantOptions
{
    public bool Enabled { get; set; }

    public List<string> AllowedBusinessIds { get; set; } = [];

    public List<string> Capabilities { get; set; } = [];

    public List<string> RequestedCapabilities { get; set; } = [];

    public List<string> Subscriptions { get; set; } = [];

    public List<string> Publications { get; set; } = [];

    public List<string> Permissions { get; set; } = [];
}

public sealed record AuthorizedAgentGrant(
    IReadOnlySet<string> Capabilities,
    IReadOnlySet<string> Subscriptions,
    IReadOnlySet<string> Publications,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string>? RequestedCapabilities = null);
