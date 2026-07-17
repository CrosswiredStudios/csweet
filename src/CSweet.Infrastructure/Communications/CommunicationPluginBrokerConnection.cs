using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Communications.Abstractions;
using Google.Protobuf;

namespace CSweet.Infrastructure.Communications;

public sealed class CommunicationPluginBrokerConnection : ICommunicationPluginClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _sync = new();
    private IAgentBrokerClient? _client;

    internal void Attach(IAgentBrokerClient client) { lock (_sync) _client = client; }
    internal void Detach(IAgentBrokerClient client) { lock (_sync) if (ReferenceEquals(_client, client)) _client = null; }

    public Task<CommunicationResult> SendAsync(Guid pluginInstallationId, OutboundCommunicationEnvelope envelope,
        CancellationToken cancellationToken = default) =>
        InvokeAsync<OutboundCommunicationEnvelope, CommunicationResult>(
            pluginInstallationId, CommunicationPluginCapabilities.SendMessage, envelope, cancellationToken);

    public Task<WorkspaceProvisioningResult> ApplyProvisioningAsync(Guid pluginInstallationId, WorkspaceProvisioningPlan plan,
        CancellationToken cancellationToken = default) =>
        InvokeAsync<WorkspaceProvisioningPlan, WorkspaceProvisioningResult>(
            pluginInstallationId, CommunicationPluginCapabilities.ApplyWorkspace, plan, cancellationToken);

    public async Task RegisterLinkCodeAsync(Guid pluginInstallationId, string code, DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default) =>
        _ = await InvokeAsync<CommunicationPluginLinkCodeRequest, CommunicationResult>(pluginInstallationId,
            CommunicationPluginCapabilities.RegisterLinkCode, new(code, expiresAt), cancellationToken);

    public Task<CommunicationResult> AssignMemberAsync(Guid pluginInstallationId, string workspaceExternalId,
        string externalUserId, string memberRoleExternalId, CancellationToken cancellationToken = default) =>
        InvokeAsync<CommunicationPluginIdentityRequest, CommunicationResult>(pluginInstallationId,
            CommunicationPluginCapabilities.AssignIdentity,
            new(workspaceExternalId, externalUserId, memberRoleExternalId), cancellationToken);

    private async Task<TResponse> InvokeAsync<TRequest, TResponse>(Guid installationId, string capability,
        TRequest payload, CancellationToken cancellationToken)
    {
        var request = new RequestCapability
        {
            RequestId = Guid.NewGuid().ToString("N"),
            TargetAgentId = $"installation:{installationId:D}",
            Capability = capability,
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))
        };
        var result = await Current.InvokeCapabilityAsync(request, request.RequestId, cancellationToken);
        if (!result.Succeeded) throw new InvalidOperationException(result.Error ?? $"Plugin capability '{capability}' failed.");
        return JsonSerializer.Deserialize<TResponse>(result.Payload.Span, JsonOptions)
            ?? throw new InvalidOperationException($"Plugin capability '{capability}' returned an empty result.");
    }

    private IAgentBrokerClient Current
    {
        get { lock (_sync) return _client ?? throw new InvalidOperationException("The communication plugin broker is not connected."); }
    }
}
