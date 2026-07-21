using System.Threading.Channels;
using System.Security.Cryptography;
using System.Text;
using CSweet.Agent.Contracts.Grpc;

namespace CSweet.AgentHost.Broker;

public sealed class AgentSession
{
    private readonly Channel<BrokerToAgentMessage> _outbound;
    private string? _initialMcpAccessToken;
    private readonly Queue<DateTimeOffset> _mcpCalls = new();
    private readonly object _mcpRateLock = new();

    public AgentSession(
        string sessionId,
        string agentId,
        string installationId,
        string businessId,
        string runtimeInstanceId,
        string tickId,
        AuthorizedAgentGrant grant,
        string? agentVersion = null)
        : this(sessionId, agentId, installationId, businessId, runtimeInstanceId, tickId,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), grant, agentVersion)
    {
    }

    public AgentSession(
        string sessionId,
        string agentId,
        string installationId,
        string businessId,
        string runtimeInstanceId,
        string tickId,
        string workloadToken,
        AuthorizedAgentGrant grant,
        string? agentVersion = null)
    {
        SessionId = sessionId;
        AgentId = agentId;
        InstallationId = installationId;
        BusinessId = businessId;
        RuntimeInstanceId = runtimeInstanceId;
        TickId = tickId;
        AgentVersion = agentVersion;
        WorkloadTokenHash = HashToken(workloadToken);
        _initialMcpAccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        McpAccessTokenHash = HashToken(_initialMcpAccessToken);
        McpTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
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
    public string? AgentVersion { get; }

    public string WorkloadTokenHash { get; }
    public string McpAccessTokenHash { get; }
    public DateTimeOffset McpTokenExpiresAt { get; }

    public string? MemoryTenantId { get; set; }

    public string? MemoryEmployeeId { get; set; }

    public AuthorizedAgentGrant Grant { get; }

    public ChannelReader<BrokerToAgentMessage> Outbound => _outbound.Reader;

    public bool TrySend(BrokerToAgentMessage message) =>
        _outbound.Writer.TryWrite(message);

    public void Complete() => _outbound.Writer.TryComplete();

    public bool MatchesWorkloadToken(string token) =>
        !string.IsNullOrWhiteSpace(token) &&
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(WorkloadTokenHash),
            Convert.FromHexString(HashToken(token)));

    public bool MatchesMcpAccessToken(string token) =>
        McpTokenExpiresAt > DateTimeOffset.UtcNow && !string.IsNullOrWhiteSpace(token) &&
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(McpAccessTokenHash),
            Convert.FromHexString(HashToken(token)));

    internal string ConsumeInitialMcpAccessToken()
    {
        var token = _initialMcpAccessToken ?? throw new InvalidOperationException("The initial MCP token was already consumed.");
        _initialMcpAccessToken = null;
        return token;
    }

    public bool TryBeginMcpCall(DateTimeOffset now, int maximumPerMinute = 60)
    {
        lock (_mcpRateLock)
        {
            while (_mcpCalls.TryPeek(out var occurredAt) && occurredAt <= now.AddMinutes(-1))
                _mcpCalls.Dequeue();
            if (_mcpCalls.Count >= maximumPerMinute) return false;
            _mcpCalls.Enqueue(now);
            return true;
        }
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));
}
