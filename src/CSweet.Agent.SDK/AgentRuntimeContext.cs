namespace CSweet.Agent.SDK;

public sealed record AgentRuntimeContext(
    string BusinessId,
    string InstallationId,
    string RuntimeInstanceId,
    string TickId,
    IAgentBrokerClient Broker);
