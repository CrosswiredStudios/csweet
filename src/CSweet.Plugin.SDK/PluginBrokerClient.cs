using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using Google.Protobuf;

namespace CSweet.Plugin.SDK;

public sealed record PluginRegistration(
    string PluginId,
    string Version,
    Guid InstallationId,
    string InstallationScope,
    IReadOnlyList<string> ProvidedCapabilities,
    IReadOnlyList<string> RequestedCapabilities,
    IReadOnlyList<string> RequestedPublications,
    IReadOnlyList<string> RequestedSubscriptions,
    IReadOnlyList<string> RequestedPermissions);

/// <summary>
/// Provider-neutral facade over the legacy agent transport. Plugins depend on this SDK while
/// CSweet.Agent.SDK remains the wire-compatible transport during the additive protocol migration.
/// </summary>
public sealed class PluginBrokerClient(IAgentBrokerClient transport) : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task StartAsync(PluginRegistration registration, CancellationToken cancellationToken = default)
    {
        var request = new RegisterAgent
        {
            AgentId = registration.PluginId,
            AgentVersion = registration.Version,
            InstallationId = registration.InstallationId.ToString("D"),
            BusinessId = registration.InstallationScope
        };
        request.DeclaredCapabilities.Add(registration.ProvidedCapabilities);
        request.RequestedSubscriptions.Add(registration.RequestedSubscriptions);
        request.RequestedPublications.Add(registration.RequestedPublications);
        request.RequestedPermissions.Add(registration.RequestedPermissions);
        return transport.StartAsync(request, cancellationToken);
    }

    public async Task<TResponse> InvokeAsync<TRequest, TResponse>(
        string capability,
        TRequest payload,
        string? targetInstallationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await transport.InvokeCapabilityAsync(new RequestCapability
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Capability = capability,
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions)),
            TargetAgentId = targetInstallationId is null ? string.Empty : $"installation:{targetInstallationId}"
        }, cancellationToken: cancellationToken);
        if (!result.Succeeded) throw new PluginCapabilityException(capability, result.Error);
        return JsonSerializer.Deserialize<TResponse>(result.Payload.Span, JsonOptions)
            ?? throw new PluginCapabilityException(capability, "The capability response was empty.");
    }

    public IAsyncEnumerable<BrokerToAgentMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
        transport.ReadAllAsync(cancellationToken);

    public Task CompleteAsync(CapabilityResult result, string? correlationId = null, CancellationToken cancellationToken = default) =>
        transport.SendCapabilityResultAsync(result, correlationId, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => transport.StopAsync(cancellationToken);
    public ValueTask DisposeAsync() => transport.DisposeAsync();
}

public sealed class PluginCapabilityException(string capability, string? message)
    : Exception($"Plugin capability '{capability}' failed: {message}");
