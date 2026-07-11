namespace CSweet.Agent.SDK;

public sealed record AgentRuntimeContext(
    string BusinessId,
    string InstallationId,
    IAgentBrokerClient Broker);
