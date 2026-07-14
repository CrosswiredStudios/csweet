using System.Text.RegularExpressions;

namespace CSweet.Infrastructure.Setup;

public static partial class DotNetAgentImageResolver
{
    public static string ResolveBuilderImage(string configuredImage, string? targetFramework) =>
        ResolveOfficialImage(configuredImage, targetFramework, "sdk");

    public static string ResolveRuntimeImage(string configuredImage, string? targetFramework) =>
        ResolveOfficialImage(configuredImage, targetFramework, "runtime");

    private static string ResolveOfficialImage(
        string configuredImage,
        string? targetFramework,
        string repository)
    {
        var frameworkVersion = ParseFrameworkVersion(targetFramework);
        if (frameworkVersion is null)
        {
            return configuredImage;
        }

        var match = OfficialImagePattern().Match(configuredImage);
        if (!match.Success ||
            !string.Equals(match.Groups["repository"].Value, repository, StringComparison.Ordinal))
        {
            return configuredImage;
        }

        return $"mcr.microsoft.com/dotnet/{repository}:{frameworkVersion}";
    }

    private static string? ParseFrameworkVersion(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return null;
        }

        var match = TargetFrameworkPattern().Match(targetFramework.Trim());
        return match.Success ? match.Groups["version"].Value : null;
    }

    [GeneratedRegex(@"^mcr\.microsoft\.com/dotnet/(?<repository>sdk|runtime):\d+\.\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex OfficialImagePattern();

    [GeneratedRegex(@"^net(?<version>\d+\.\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkPattern();
}
