using System.Threading.Channels;
using CSweet.Agent.Contracts.Grpc;

namespace CSweet.AgentHost.Broker;

public sealed class AgentSession
{
    private readonly Channel<BrokerToAgentMessage> _outbound;

    public AgentSession(
        string sessionId,
        string agentId,
        string installationId,
        string businessId,
        string runtimeInstanceId,
        string tickId,
        AuthorizedAgentGrant grant)
    {
        SessionId = sessionId;
        AgentId = agentId;
        InstallationId = installationId;
        BusinessId = businessId;
        RuntimeInstanceId = runtimeInstanceId;
        TickId = tickId;
        Grant = grant;
        _outbound = Channel.CreateBounded<BrokerToAgentMessage>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public string SessionId { get; }

    public string AgentId { get; }

    public string InstallationId { get; }

    public string BusinessId { get; }
    public string RuntimeInstanceId { get; }
    public string TickId { get; }

    public string? MemoryTenantId { get; set; }

    public string? MemoryEmployeeId { get; set; }

    public AuthorizedAgentGrant Grant { get; }

    public ChannelReader<BrokerToAgentMessage> Outbound => _outbound.Reader;

    public bool TrySend(BrokerToAgentMessage message) =>
        _outbound.Writer.TryWrite(message);

    public void Complete() => _outbound.Writer.TryComplete();
}
