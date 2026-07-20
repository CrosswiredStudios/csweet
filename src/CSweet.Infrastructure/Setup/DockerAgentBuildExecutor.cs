using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CSweet.Application.Setup;

namespace CSweet.Infrastructure.Setup;

public sealed class DockerAgentBuildExecutor : IPluginBuildExecutor
{
    private const int BuilderUserId = 1654;

    public async Task<AgentBuildWorkspace> CloneAsync(
        AgentBuildExecutionRequest request,
        IAgentBuildProgressReporter progress,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var sourceRoot = ResolveStorageRoot(request.SourceRootPath, "sources");
        var packageRoot = ResolveStorageRoot(request.PackageCachePath, "packages");
        var sourcePath = Path.Combine(sourceRoot, request.BuildJobId.ToString("N"));
        var stagingPath = Path.Combine(packageRoot, ".staging", request.BuildJobId.ToString("N"));
        var logPath = Path.Combine(packageRoot, "logs", $"{request.BuildJobId:N}.log");

        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(stagingPath);

        var log = new CappedBuildLog(logPath, MegabytesToBytes(request.MaximumBuildLogMb));
        if (!string.IsNullOrWhiteSpace(request.SourceArchivePath))
        {
            await log.AppendAsync($"Materializing approved source archive {request.CommitSha}.{Environment.NewLine}", cancellationToken);
            ExtractApprovedArchive(request.SourceArchivePath, sourcePath, request.MaximumRepositorySizeMb);
        }
        else
        {
            await log.AppendAsync(
                $"Materializing commit {request.CommitSha} from {request.RepositoryUrl}.{Environment.NewLine}",
                cancellationToken);
            await RunRequiredAsync("git", ["init", "--template=", sourcePath], null, log, cancellationToken);
            await RunRequiredAsync("git", ["-C", sourcePath, "remote", "add", "origin", request.RepositoryUrl], null, log, cancellationToken);
            await RunRequiredAsync("git", ["-C", sourcePath, "fetch", "--depth", "1", "--no-tags", "origin", request.CommitSha], null, log, cancellationToken);
            await RunRequiredAsync("git", ["-C", sourcePath, "checkout", "--detach", "FETCH_HEAD"], null, log, cancellationToken);
        }

        EnsureRepositorySize(sourcePath, request.MaximumRepositorySizeMb);
        EnsureProjectExistsInsideWorkspace(sourcePath, request.ProjectPath);
        return new AgentBuildWorkspace(sourcePath, stagingPath, logPath);
    }

    public async Task<AgentBuildExecutionResult> BuildAsync(
        AgentBuildExecutionRequest request,
        AgentBuildWorkspace workspace,
        IAgentBuildProgressReporter progress,
        CancellationToken cancellationToken = default)
    {
        var log = new CappedBuildLog(workspace.LogPath, MegabytesToBytes(request.MaximumBuildLogMb));
        await log.AppendAsync(
            $"Building {request.ProjectPath} in isolated image {request.BuilderImage}.{Environment.NewLine}",
            cancellationToken);

        var temporaryWorkspaceMb = Math.Max(
            request.MaximumRepositorySizeMb * 3,
            request.MemoryMb);
        var cpuCount = Math.Max(0.01m, request.CpuPercent / 100m)
            .ToString("0.##", CultureInfo.InvariantCulture);
        var containerName = $"csweet-agent-build-{request.BuildJobId:N}";
        var arguments = new List<string>
        {
            "run", "--rm",
            "--name", containerName,
            "--network", "bridge",
            "--read-only",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges=true",
            "--user", $"{BuilderUserId}:{BuilderUserId}",
            "--memory", $"{request.MemoryMb}m",
            "--cpus", cpuCount,
            "--pids-limit", request.PidsLimit.ToString(CultureInfo.InvariantCulture),
            "--tmpfs", "/tmp:rw,nosuid,nodev,size=512m,uid=1654,gid=1654",
            "--tmpfs", $"/work:rw,exec,nosuid,nodev,size={temporaryWorkspaceMb}m,uid=1654,gid=1654",
            "--mount", CreateSourceMount(request, workspace),
            "--mount", CreatePackageMount(request, workspace),
            "--env", $"PROJECT_PATH={NormalizeContainerPath(request.ProjectPath)}",
            "--env", "DOTNET_CLI_HOME=/tmp/dotnet",
            "--env", "NUGET_PACKAGES=/tmp/nuget",
            "--env", "DOTNET_NOLOGO=1",
            request.BuilderImage,
            "/bin/sh", "-c",
            "set -eu; echo '__CSWEET_BUILD_STEP__:isolate:started'; " +
            "mkdir -p /work/source; cp -a /source/. /work/source/; " +
            "echo '__CSWEET_BUILD_STEP__:isolate:succeeded'; " +
            "echo '__CSWEET_BUILD_STEP__:restore:started'; " +
            "dotnet restore \"/work/source/$PROJECT_PATH\" --nologo --source https://api.nuget.org/v3/index.json; " +
            "echo '__CSWEET_BUILD_STEP__:restore:succeeded'; " +
            "echo '__CSWEET_BUILD_STEP__:publish:started'; " +
            "dotnet publish \"/work/source/$PROJECT_PATH\" --configuration Release --no-restore --nologo --output /output; " +
            "cp /source/csweet-plugin.json /output/csweet-plugin.json; " +
            "echo '__CSWEET_BUILD_STEP__:publish:succeeded'"
        };

        var tracker = new BuildStepTracker(progress);
        try
        {
            await RunRequiredAsync("docker", arguments, null, log, cancellationToken, progress, tracker);
        }
        catch
        {
            await RemoveBuilderContainerAsync(containerName, log);
            throw;
        }
        await progress.ReportAsync(
            new AgentBuildProgressUpdate(
                AgentBuildStepKeys.Package,
                AgentBuildStepStatuses.InProgress,
                "Validating package contents and computing its digest."),
            cancellationToken);
        try
        {
            var packageFiles = EnumerateRegularFiles(workspace.StagingPackagePath, "build package");
            if (packageFiles.Count == 0)
            {
                throw new AgentBuildException("The isolated builder completed without producing package files.");
            }
            EnsureTotalSize(packageFiles, request.MaximumRepositorySizeMb, "The build package");

            var digest = await ComputePackageDigestAsync(
                workspace.StagingPackagePath,
                packageFiles,
                cancellationToken);
            var packageRoot = ResolveStorageRoot(request.PackageCachePath, "packages");
            var finalPath = Path.Combine(packageRoot, request.PackageVersionId.ToString("N"), digest);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            if (Directory.Exists(finalPath))
            {
                Directory.Delete(workspace.StagingPackagePath, recursive: true);
            }
            else
            {
                Directory.Move(workspace.StagingPackagePath, finalPath);
            }

            await log.AppendAsync($"Package digest: sha256:{digest}{Environment.NewLine}", cancellationToken);
            await progress.ReportAsync(
                new AgentBuildProgressUpdate(
                    AgentBuildStepKeys.Package,
                    AgentBuildStepStatuses.Succeeded,
                    $"Package verified (sha256:{digest[..12]}…)."),
                cancellationToken);
            return new AgentBuildExecutionResult(finalPath, digest, workspace.LogPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var message = $"Package verification failed. {exception.Message}";
            await progress.ReportAsync(
                new AgentBuildProgressUpdate(
                    AgentBuildStepKeys.Package,
                    AgentBuildStepStatuses.Failed,
                    Error: message),
                CancellationToken.None);
            throw new AgentBuildException(message, AgentBuildStepKeys.Package, exception);
        }
    }

    public Task CleanupWorkspaceAsync(
        AgentBuildWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(workspace.SourcePath))
        {
            Directory.Delete(workspace.SourcePath, recursive: true);
        }
        if (Directory.Exists(workspace.StagingPackagePath))
        {
            Directory.Delete(workspace.StagingPackagePath, recursive: true);
        }
        return Task.CompletedTask;
    }

    private static async Task RunRequiredAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CappedBuildLog log,
        CancellationToken cancellationToken,
        IAgentBuildProgressReporter? progress = null,
        BuildStepTracker? stepTracker = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        await log.AppendAsync($"> {fileName} {FormatArgumentsForLog(arguments)}{Environment.NewLine}", cancellationToken);
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new AgentBuildException($"Could not start required command '{fileName}'.");
            }
        }
        catch (Exception exception) when (exception is not AgentBuildException)
        {
            throw new AgentBuildException(
                $"Could not start required command '{fileName}'. Ensure it is installed for the build worker.",
                exception);
        }

        var diagnosticTail = new DiagnosticTailBuffer(16 * 1024);
        var standardOutput = CopyProcessOutputAsync(
            process.StandardOutput,
            log,
            diagnosticTail,
            stepTracker,
            cancellationToken);
        var standardError = CopyProcessOutputAsync(
            process.StandardError,
            log,
            diagnosticTail,
            null,
            cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(standardOutput, standardError);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            var message = CreateFailureMessage(fileName, process.ExitCode, stepTracker, diagnosticTail);
            if (progress is not null && stepTracker?.CurrentStepKey is not null)
            {
                await progress.ReportAsync(
                    new AgentBuildProgressUpdate(
                        stepTracker.CurrentStepKey,
                        AgentBuildStepStatuses.Failed,
                        Error: message),
                    CancellationToken.None);
            }
            throw stepTracker?.CurrentStepKey is { } stepKey
                ? new AgentBuildException(message, stepKey)
                : new AgentBuildException(message);
        }
    }

    private static async Task CopyProcessOutputAsync(
        StreamReader reader,
        CappedBuildLog log,
        DiagnosticTailBuffer diagnosticTail,
        BuildStepTracker? stepTracker,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (stepTracker is not null && await stepTracker.TryHandleMarkerAsync(line, cancellationToken))
            {
                continue;
            }
            diagnosticTail.Append(line);
            await log.AppendAsync(line + Environment.NewLine, cancellationToken);
        }
    }

    private static string CreateFailureMessage(
        string fileName,
        int exitCode,
        BuildStepTracker? stepTracker,
        DiagnosticTailBuffer diagnosticTail)
    {
        var lines = diagnosticTail.Lines;
        var errorLines = lines
            .Where(line => line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            .TakeLast(4)
            .ToList();
        var selected = errorLines.Count > 0 ? errorLines : lines.TakeLast(3).ToList();
        var phase = stepTracker?.CurrentStepKey switch
        {
            AgentBuildStepKeys.Isolate => "Isolated build environment preparation",
            AgentBuildStepKeys.Restore => "Dependency restore",
            AgentBuildStepKeys.Publish => "Compilation and publish",
            _ => $"Required command '{fileName}'"
        };
        var header = $"{phase} failed (exit code {exitCode}).";
        if (selected.Count == 0)
        {
            return header;
        }
        var message = header + Environment.NewLine + string.Join(Environment.NewLine, selected);
        return message.Length <= 1900 ? message : message[..1900];
    }

    private static async Task RemoveBuilderContainerAsync(string containerName, CappedBuildLog log)
    {
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await RunRequiredAsync(
                "docker",
                ["rm", "--force", containerName],
                null,
                log,
                cleanupTimeout.Token);
        }
        catch (Exception exception)
        {
            await log.AppendAsync(
                $"Builder container cleanup note: {exception.Message}{Environment.NewLine}",
                CancellationToken.None);
        }
    }

    private static async Task<string> ComputePackageDigestAsync(
        string packagePath,
        IReadOnlyList<string> packageFiles,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var filePath in packageFiles
                     .OrderBy(path => Path.GetRelativePath(packagePath, path), StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(packagePath, filePath).Replace('\\', '/');
            hash.AppendData(Encoding.UTF8.GetBytes(relativePath));
            hash.AppendData([0]);
            await using var stream = File.OpenRead(filePath);
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, bytesRead));
            }
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void ValidateRequest(AgentBuildExecutionRequest request)
    {
        if (request.CommitSha.Length != 40 || request.CommitSha.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new AgentBuildException("The approved commit SHA is invalid.");
        }
        if (string.IsNullOrWhiteSpace(request.BuilderImage) || request.BuilderImage.Contains(char.MinValue))
        {
            throw new AgentBuildException("A .NET builder image is required.");
        }
        if (request.TimeoutSeconds <= 0 || request.MemoryMb <= 0 || request.CpuPercent <= 0 ||
            request.PidsLimit <= 0 || request.MaximumRepositorySizeMb <= 0 || request.MaximumBuildLogMb <= 0)
        {
            throw new AgentBuildException("Build limits must all be positive.");
        }
    }

    private static void EnsureProjectExistsInsideWorkspace(string sourcePath, string projectPath)
    {
        var normalizedProjectPath = projectPath.Replace('/', Path.DirectorySeparatorChar);
        var fullSourcePath = Path.GetFullPath(sourcePath) + Path.DirectorySeparatorChar;
        var fullProjectPath = Path.GetFullPath(Path.Combine(sourcePath, normalizedProjectPath));
        if (!fullProjectPath.StartsWith(fullSourcePath, PathComparison) ||
            !fullProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentBuildException("The approved project path escapes the cloned source workspace.");
        }
        if (!File.Exists(fullProjectPath))
        {
            throw new AgentBuildException($"The approved project path '{projectPath}' was not found at the recorded commit.");
        }
    }

    private static void ExtractApprovedArchive(string archivePath, string destination, int maximumSizeMb)
    {
        if (!Path.IsPathFullyQualified(archivePath) || !File.Exists(archivePath))
            throw new AgentBuildException("The approved source archive is unavailable.");
        using var archive = System.IO.Compression.ZipFile.OpenRead(archivePath);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (relative.StartsWith('/') || Path.IsPathRooted(relative) || segments.Contains("..", StringComparer.Ordinal))
                throw new AgentBuildException("The source archive contains a path traversal entry.");
            if (string.IsNullOrEmpty(entry.Name)) continue;
            total = checked(total + entry.Length);
            if (total > MegabytesToBytes(maximumSizeMb)) throw new AgentBuildException("The source archive exceeds the approved size limit.");
            var target = Path.GetFullPath(Path.Combine(destination, Path.Combine(segments)));
            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, PathComparison)) throw new AgentBuildException("The source archive escapes its workspace.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
    }

    private static void EnsureRepositorySize(string sourcePath, int maximumSizeMb)
    {
        var files = EnumerateRegularFiles(sourcePath, "cloned repository");
        EnsureTotalSize(files, maximumSizeMb, "The cloned repository");
    }

    private static void EnsureTotalSize(
        IReadOnlyList<string> files,
        int maximumSizeMb,
        string subject)
    {
        var maximumBytes = MegabytesToBytes(maximumSizeMb);
        long totalBytes = 0;
        foreach (var file in files)
        {
            totalBytes = checked(totalBytes + new FileInfo(file).Length);
            if (totalBytes > maximumBytes)
            {
                throw new AgentBuildException($"{subject} exceeds the {maximumSizeMb} MB limit.");
            }
        }
    }

    private static IReadOnlyList<string> EnumerateRegularFiles(string rootPath, string subject)
    {
        var files = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);
        while (pendingDirectories.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new AgentBuildException($"The {subject} contains a symbolic link, which is not allowed.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pendingDirectories.Push(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }
        }
        return files;
    }

    private static string ResolveStorageRoot(string configuredPath, string childName)
    {
        var environmentVariable = childName == "sources"
            ? "CSWEET_AGENT_SOURCE_ROOT"
            : "CSWEET_AGENT_PACKAGE_CACHE";
        var environmentPath = Environment.GetEnvironmentVariable(environmentVariable);
        var path = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : !string.IsNullOrWhiteSpace(environmentPath)
                ? environmentPath
                : Path.Combine(GetLocalStateDirectory(), "agents", childName);
        var fullPath = Path.GetFullPath(path);
        if (fullPath.Contains(','))
        {
            throw new AgentBuildException("Agent build storage paths cannot contain commas because they are Docker mount delimiters.");
        }
        return fullPath;
    }

    private static string GetLocalStateDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, ".csweet")
            : Path.Combine(localAppData, "CSweet");
    }

    private static string CreateSourceMount(
        AgentBuildExecutionRequest request,
        AgentBuildWorkspace workspace)
    {
        var volumeName = Environment.GetEnvironmentVariable("CSWEET_AGENT_SOURCE_VOLUME");
        return string.IsNullOrWhiteSpace(volumeName)
            ? CreateBindMount(workspace.SourcePath, "/source", readOnly: true)
            : CreateVolumeMount(volumeName, request.BuildJobId.ToString("N"), "/source", readOnly: true);
    }

    private static string CreatePackageMount(
        AgentBuildExecutionRequest request,
        AgentBuildWorkspace workspace)
    {
        var volumeName = Environment.GetEnvironmentVariable("CSWEET_AGENT_PACKAGE_VOLUME");
        return string.IsNullOrWhiteSpace(volumeName)
            ? CreateBindMount(workspace.StagingPackagePath, "/output", readOnly: false)
            : CreateVolumeMount(volumeName, $".staging/{request.BuildJobId:N}", "/output", readOnly: false);
    }

    private static string CreateBindMount(string source, string target, bool readOnly) =>
        $"type=bind,source={Path.GetFullPath(source)},target={target}{(readOnly ? ",readonly" : string.Empty)}";

    private static string CreateVolumeMount(string volumeName, string subpath, string target, bool readOnly)
    {
        if (volumeName.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')))
        {
            throw new AgentBuildException("Agent build Docker volume names contain unsupported characters.");
        }
        return $"type=volume,source={volumeName},target={target},volume-subpath={subpath}" +
            (readOnly ? ",readonly" : string.Empty);
    }

    private static string NormalizeContainerPath(string projectPath) => projectPath.Replace('\\', '/');

    private static long MegabytesToBytes(int value) => checked(value * 1024L * 1024L);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string FormatArgumentsForLog(IReadOnlyList<string> arguments) =>
        string.Join(' ', arguments.Select(argument => argument.Any(char.IsWhiteSpace) ? "\"...\"" : argument));

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed class BuildStepTracker(IAgentBuildProgressReporter progress)
    {
        private const string MarkerPrefix = "__CSWEET_BUILD_STEP__:";

        public string? CurrentStepKey { get; private set; }

        public async Task<bool> TryHandleMarkerAsync(string line, CancellationToken cancellationToken)
        {
            if (!line.StartsWith(MarkerPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var marker = line[MarkerPrefix.Length..].Split(':', 2, StringSplitOptions.TrimEntries);
            if (marker.Length != 2 || marker[0] is not (
                    AgentBuildStepKeys.Isolate or
                    AgentBuildStepKeys.Restore or
                    AgentBuildStepKeys.Publish))
            {
                return true;
            }

            var stepKey = marker[0];
            if (string.Equals(marker[1], "started", StringComparison.Ordinal))
            {
                CurrentStepKey = stepKey;
                var detail = stepKey switch
                {
                    AgentBuildStepKeys.Isolate => "Copying approved source into the restricted build container.",
                    AgentBuildStepKeys.Restore => "Resolving packages from the approved package feed.",
                    _ => "Compiling the agent and producing release output."
                };
                await progress.ReportAsync(
                    new AgentBuildProgressUpdate(
                        stepKey,
                        AgentBuildStepStatuses.InProgress,
                        detail),
                    cancellationToken);
            }
            else if (string.Equals(marker[1], "succeeded", StringComparison.Ordinal))
            {
                var detail = stepKey switch
                {
                    AgentBuildStepKeys.Isolate => "The isolated build environment is ready.",
                    AgentBuildStepKeys.Restore => "All dependencies were restored.",
                    _ => "The agent compiled and publish output was created."
                };
                await progress.ReportAsync(
                    new AgentBuildProgressUpdate(
                        stepKey,
                        AgentBuildStepStatuses.Succeeded,
                        detail),
                    cancellationToken);
                CurrentStepKey = null;
            }
            return true;
        }
    }

    private sealed class DiagnosticTailBuffer(int maximumCharacters)
    {
        private readonly Queue<string> _lines = new();
        private readonly object _lock = new();
        private int _characterCount;

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lock)
                {
                    return _lines.ToList();
                }
            }
        }

        public void Append(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }
            lock (_lock)
            {
                _lines.Enqueue(line);
                _characterCount += line.Length;
                while (_characterCount > maximumCharacters && _lines.Count > 1)
                {
                    _characterCount -= _lines.Dequeue().Length;
                }
            }
        }
    }

    private sealed class CappedBuildLog
    {
        private readonly string _path;
        private readonly long _maximumBytes;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public CappedBuildLog(string path, long maximumBytes)
        {
            _path = path;
            _maximumBytes = maximumBytes;
        }

        public async Task CopyFromAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var buffer = new char[4096];
            int charactersRead;
            while ((charactersRead = await reader.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await AppendAsync(new string(buffer, 0, charactersRead), cancellationToken);
            }
        }

        public async Task AppendAsync(string value, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await using var stream = new FileStream(
                    _path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);
                var remaining = _maximumBytes - stream.Length;
                if (remaining <= 0)
                {
                    return;
                }
                await stream.WriteAsync(bytes.AsMemory(0, (int)Math.Min(bytes.Length, remaining)), cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
