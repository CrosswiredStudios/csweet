namespace CSweet.Infrastructure.Setup;

public interface IDockerCommandExecutor
{
    Task<DockerCommandResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

public sealed record DockerCommandResult(int ExitCode, string StandardOutput, string StandardError);
