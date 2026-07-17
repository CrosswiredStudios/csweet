using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Application.Setup;
using Google.Protobuf;

namespace CSweet.AgentHost.Broker;

public sealed class PlatformPluginSecretCapabilityHandler(
    IPluginSecretStore secrets,
    IAuditEventWriter audit)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CapabilityResult> HandleAsync(
        AgentSession session,
        RequestCapability request,
        CancellationToken cancellationToken)
    {
        if (request.Capability != PluginPlatformCapabilities.ReadSecret)
            return Failure(request.RequestId, "Unsupported plugin secret capability.");
        if (session.Grant.RequestedCapabilities?.Contains(PluginPlatformCapabilities.ReadSecret) != true)
            return Failure(request.RequestId, "The installation is not granted plugin secret reads.");
        if (!Guid.TryParse(session.InstallationId, out var installationId))
            return Failure(request.RequestId, "The plugin installation identity is invalid.");

        try
        {
            var command = JsonSerializer.Deserialize<PluginSecretReadRequest>(request.Payload.Span, JsonOptions)
                ?? throw new JsonException("The secret request is empty.");
            if (!session.Grant.Permissions.Contains($"secret.read:{command.Key}"))
                return Failure(request.RequestId, $"The installation is not granted access to secret '{command.Key}'.");
            var value = await secrets.GetAsync(installationId, command.Key, cancellationToken);
            await audit.WriteAsync("plugin.secret.read", "PluginInstallation", installationId,
                $"Plugin read installation-scoped secret '{command.Key}'.",
                JsonSerializer.Serialize(new { installationId, key = command.Key, session.AgentId }), cancellationToken);
            return value is null
                ? Failure(request.RequestId, $"Secret '{command.Key}' is not configured.")
                : Success(request.RequestId, JsonSerializer.SerializeToUtf8Bytes(new PluginSecretReadResponse(value), JsonOptions));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return Failure(request.RequestId, exception.Message);
        }
    }

    private static CapabilityResult Success(string requestId, byte[] payload) => new()
    {
        RequestId = requestId,
        Succeeded = true,
        ContentType = "application/json",
        Payload = ByteString.CopyFrom(payload)
    };

    private static CapabilityResult Failure(string requestId, string error) => new()
    {
        RequestId = requestId,
        Succeeded = false,
        ContentType = "application/json",
        Error = error
    };
}
