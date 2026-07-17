using System.Globalization;
using System.Text.Json;
using CSweet.Application.Setup;
using Microsoft.Extensions.Logging;

namespace CSweet.Infrastructure.Setup;

public sealed class DockerAgentContainerRunner(
    IDockerCommandExecutor docker,
    ILogger<DockerAgentContainerRunner> logger) : IPluginContainerRunner
{
    private const int RuntimeUserId = 1654;

    public async Task<AgentContainerStatus> StartAsync(
        AgentContainerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateStartRequest(request);
        await EnsureNetworkAsync(request.NetworkName, cancellationToken);
        await ConnectBrokerGatewayAsync(request, cancellationToken);
        var cpus = (request.CpuPercent / 100m).ToString("0.##", CultureInfo.InvariantCulture);
        var args = new List<string>
        {
            "run", "--detach", "--init",
            "--name", request.ContainerName,
            "--network", request.NetworkName,
            "--read-only",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges=true",
            "--user", $"{RuntimeUserId}:{RuntimeUserId}",
            "--memory", $"{request.MemoryMb}m",
            "--cpus", cpus,
            "--pids-limit", request.PidsLimit.ToString(CultureInfo.InvariantCulture),
            "--tmpfs", $"/tmp:rw,nosuid,nodev,noexec,size=64m,uid={RuntimeUserId},gid={RuntimeUserId}",
            "--mount", CreatePackageMount(request.PackagePath),
            "--env", $"CSweet__Agent__RuntimeInstanceId={request.RuntimeInstanceId:D}",
            "--env", $"CSweet__Agent__TickId={request.TickId:D}",
            "--env", $"CSweet__Agent__InstallationId={request.InstallationId:D}",
            "--env", $"CSweet__Agent__BusinessId={request.BusinessId}",
            "--env", $"CSweet__Agent__BrokerEndpoint={request.BrokerEndpoint}",
            "--env", $"CSweet__Agent__WorkloadToken={request.WorkloadToken}",
            "--env", $"CSweet__Agent__ManifestPath={request.ManifestPath}",
            "--env", $"CSweet__Plugin__InstallationId={request.InstallationId:D}",
            "--env", $"CSweet__Plugin__BrokerEndpoint={request.BrokerEndpoint}",
            "--env", "DOTNET_CLI_HOME=/tmp/dotnet",
            "--env", "DOTNET_NOLOGO=1",
            request.RuntimeImage,
            "dotnet", $"/app/{request.EntryAssembly}"
        };

        if (!string.IsNullOrWhiteSpace(request.PersistentDataVolumeName))
        {
            ValidateVolumeName(request.PersistentDataVolumeName);
            var imageIndex = args.IndexOf(request.RuntimeImage);
            args.InsertRange(imageIndex, ["--mount", $"type=volume,source={request.PersistentDataVolumeName},target=/data"]);
        }

        logger.LogInformation(
            "Starting agent container {ContainerName} for runtime {RuntimeInstanceId}, installation {InstallationId}, image {RuntimeImage}, network {NetworkName}, and broker {BrokerEndpoint}",
            request.ContainerName,
            request.RuntimeInstanceId,
            request.InstallationId,
            request.RuntimeImage,
            request.NetworkName,
            request.BrokerEndpoint);
        var result = await docker.ExecuteAsync(args, cancellationToken);
        if (result.ExitCode != 0)
        {
            logger.LogError("Agent container {ContainerName} failed to start: {DockerError}", request.ContainerName, result.StandardError.Trim());
            throw new AgentContainerException($"Docker failed to start agent container: {SanitizeError(result.StandardError)}");
        }

        var id = result.StandardOutput.Trim();
        logger.LogInformation("Started agent container {ContainerId} for runtime {RuntimeInstanceId}", id, request.RuntimeInstanceId);
        return await InspectAsync(id, cancellationToken)
            ?? new AgentContainerStatus(id, request.ContainerName, AgentContainerState.Created, null, null, null, null);
    }

    public async Task StopAsync(string containerId, TimeSpan gracePeriod, CancellationToken cancellationToken = default)
    {
        ValidateContainerId(containerId);
        var seconds = Math.Max(0, (int)Math.Ceiling(gracePeriod.TotalSeconds));
        logger.LogInformation("Stopping agent container {ContainerId} with {GraceSeconds}s grace", containerId, seconds);
        await ExecuteRequiredAsync(["stop", "--time", seconds.ToString(CultureInfo.InvariantCulture), containerId], "stop", cancellationToken);
    }

    public async Task<AgentContainerStatus?> InspectAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ValidateContainerId(containerId);
        var result = await docker.ExecuteAsync(["inspect", "--format", "{{json .}}", containerId], cancellationToken);
        if (result.ExitCode != 0)
        {
            return result.StandardError.Contains("No such", StringComparison.OrdinalIgnoreCase) ? null
                : throw new AgentContainerException($"Docker failed to inspect agent container: {SanitizeError(result.StandardError)}");
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        var state = root.GetProperty("State");
        return new AgentContainerStatus(
            root.GetProperty("Id").GetString() ?? containerId,
            (root.GetProperty("Name").GetString() ?? string.Empty).TrimStart('/'),
            ParseState(state.GetProperty("Status").GetString()),
            state.TryGetProperty("ExitCode", out var exitCode) ? exitCode.GetInt32() : null,
            ParseTimestamp(state, "StartedAt"),
            ParseTimestamp(state, "FinishedAt"),
            state.TryGetProperty("Error", out var error) && !string.IsNullOrWhiteSpace(error.GetString()) ? error.GetString() : null);
    }

    public Task RemoveAsync(string containerId, bool force = false, CancellationToken cancellationToken = default)
    {
        ValidateContainerId(containerId);
        IReadOnlyList<string> args = force ? ["rm", "--force", containerId] : ["rm", containerId];
        logger.LogInformation("Removing agent container {ContainerId}; force={Force}", containerId, force);
        return ExecuteRequiredAsync(args, "remove", cancellationToken);
    }

    public async Task<string> GetLogsAsync(string containerId, int maximumBytes, CancellationToken cancellationToken = default)
    {
        ValidateContainerId(containerId);
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var result = await docker.ExecuteAsync(["logs", containerId], cancellationToken);
        if (result.ExitCode != 0) throw new AgentContainerException($"Docker failed to read agent container logs: {SanitizeError(result.StandardError)}");
        var combined = result.StandardOutput + result.StandardError;
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        return bytes.Length <= maximumBytes
            ? combined
            : System.Text.Encoding.UTF8.GetString(bytes.AsSpan(0, maximumBytes));
    }

    private async Task ExecuteRequiredAsync(IReadOnlyList<string> arguments, string operation, CancellationToken cancellationToken)
    {
        var result = await docker.ExecuteAsync(arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            logger.LogError("Docker failed to {Operation} agent container: {DockerError}", operation, result.StandardError.Trim());
            throw new AgentContainerException($"Docker failed to {operation} agent container: {SanitizeError(result.StandardError)}");
        }
    }

    private async Task EnsureNetworkAsync(string networkName, CancellationToken cancellationToken)
    {
        var inspect = await docker.ExecuteAsync(["network", "inspect", networkName], cancellationToken);
        if (inspect.ExitCode == 0)
        {
            logger.LogDebug("Using existing Docker network {NetworkName} for agent runtimes.", networkName);
            return;
        }

        if (!inspect.StandardError.Contains("not found", StringComparison.OrdinalIgnoreCase) &&
            !inspect.StandardError.Contains("No such network", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentContainerException(
                $"Docker failed to inspect agent runtime network '{networkName}': {SanitizeError(inspect.StandardError)}");
        }

        logger.LogWarning("Docker network {NetworkName} was missing; creating a bridge network for agent runtimes.", networkName);
        var create = await docker.ExecuteAsync(["network", "create", "--driver", "bridge", "--internal", networkName], cancellationToken);
        if (create.ExitCode != 0 && !create.StandardError.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentContainerException(
                $"Docker failed to create agent runtime network '{networkName}': {SanitizeError(create.StandardError)}");
        }
    }

    private async Task ConnectBrokerGatewayAsync(AgentContainerStartRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BrokerGatewayContainer) ||
            request.BrokerGatewayContainer.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new AgentContainerException("A valid broker gateway container is required for the isolated runtime network.");
        var brokerHost = new Uri(request.BrokerEndpoint).Host;
        var connect = await docker.ExecuteAsync([
            "network", "connect", "--alias", brokerHost, request.NetworkName, request.BrokerGatewayContainer
        ], cancellationToken);
        if (connect.ExitCode != 0 && !connect.StandardError.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            throw new AgentContainerException($"Docker could not attach the broker gateway to the isolated runtime network: {SanitizeError(connect.StandardError)}");
    }

    private static void ValidateStartRequest(AgentContainerStartRequest request)
    {
        if (request.MemoryMb <= 0 || request.CpuPercent <= 0 || request.PidsLimit <= 0 || request.MaxRuntimeSeconds <= 0)
            throw new AgentContainerException("Container resource and runtime limits must be positive.");
        if (string.IsNullOrWhiteSpace(request.AgentId) || string.IsNullOrWhiteSpace(request.BusinessId) || string.IsNullOrWhiteSpace(request.RuntimeImage) || string.IsNullOrWhiteSpace(request.NetworkName) || string.IsNullOrWhiteSpace(request.BrokerEndpoint) || string.IsNullOrWhiteSpace(request.WorkloadToken))
            throw new AgentContainerException("Runtime image, broker endpoint, workload token, and isolated network are required.");
        if (request.NetworkName.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new AgentContainerException("The runtime network name contains unsupported characters.");
        if (!Path.IsPathFullyQualified(request.PackagePath) || request.PackagePath.Contains(',', StringComparison.Ordinal))
            throw new AgentContainerException("The package path must be absolute and cannot contain Docker mount delimiters.");
        if (request.EntryAssembly != Path.GetFileName(request.EntryAssembly) || !request.EntryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new AgentContainerException("The entry assembly must be a package-root .dll file name.");
        if (!request.ManifestPath.StartsWith("/app/", StringComparison.Ordinal) || request.ManifestPath.Contains("..", StringComparison.Ordinal))
            throw new AgentContainerException("The manifest path must be inside the read-only package mount.");
        if (request.ContainerName.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new AgentContainerException("The container name contains unsupported characters.");
    }

    private static void ValidateVolumeName(string volumeName)
    {
        if (volumeName.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')))
            throw new AgentContainerException("The plugin data volume name contains unsupported characters.");
    }

    private static void ValidateContainerId(string containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId) || containerId.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new AgentContainerException("The container identifier is invalid.");
    }

    private static AgentContainerState ParseState(string? state) => state?.ToLowerInvariant() switch
    {
        "created" => AgentContainerState.Created, "running" => AgentContainerState.Running,
        "exited" => AgentContainerState.Exited, "dead" => AgentContainerState.Dead,
        "paused" => AgentContainerState.Paused, "restarting" => AgentContainerState.Restarting,
        _ => AgentContainerState.Unknown
    };

    private static DateTimeOffset? ParseTimestamp(JsonElement state, string property)
        => state.TryGetProperty(property, out var value) && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) && timestamp != default ? timestamp : null;

    private static string SanitizeError(string error)
        => string.IsNullOrWhiteSpace(error) ? "unknown Docker error" : error.Trim().Replace("\r", " ").Replace("\n", " ");

    private static string CreatePackageMount(string packagePath)
    {
        var volumeName = Environment.GetEnvironmentVariable("CSWEET_AGENT_PACKAGE_VOLUME");
        var packageRoot = Environment.GetEnvironmentVariable("CSWEET_AGENT_PACKAGE_CACHE");
        if (string.IsNullOrWhiteSpace(volumeName) || string.IsNullOrWhiteSpace(packageRoot))
            return $"type=bind,source={packagePath},target=/app,readonly";
        if (volumeName.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')))
            throw new AgentContainerException("The agent package volume name is invalid.");
        var root = Path.GetFullPath(packageRoot);
        var path = Path.GetFullPath(packagePath);
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal))
            throw new AgentContainerException("The runtime package is outside the approved package cache.");
        return $"type=volume,source={volumeName},target=/app,volume-subpath={relative},readonly";
    }
}
