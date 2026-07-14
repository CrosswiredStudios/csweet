using CSweet.Application.Setup;

namespace CSweet.Infrastructure.Setup;

public static class GitHubRepositoryUrlNormalizer
{
    public static NormalizedGitHubRepository Normalize(string repositoryUrl)
    {
        if (!Uri.TryCreate(repositoryUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new AgentImportPreviewException(
                "Repository URL must be a public HTTPS GitHub repository URL.");
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 2)
        {
            throw new AgentImportPreviewException(
                "Repository URL must have the form https://github.com/owner/repository.");
        }

        var owner = Uri.UnescapeDataString(segments[0]);
        var repositoryName = Uri.UnescapeDataString(segments[1]);
        if (repositoryName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repositoryName = repositoryName[..^4];
        }

        if (!IsValidSegment(owner) || !IsValidSegment(repositoryName))
        {
            throw new AgentImportPreviewException("GitHub repository owner or name is invalid.");
        }

        return new NormalizedGitHubRepository(
            owner,
            repositoryName,
            $"https://github.com/{owner}/{repositoryName}");
    }

    private static bool IsValidSegment(string value) =>
        value.Length is > 0 and <= 100 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
}

public sealed record NormalizedGitHubRepository(
    string Owner,
    string Name,
    string RepositoryUrl);