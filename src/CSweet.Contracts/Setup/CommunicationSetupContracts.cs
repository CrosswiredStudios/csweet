namespace CSweet.Contracts.Setup;

public sealed record CommunicationSetupOptionsResponse(
    string? DiscordInstallUrl,
    bool DiscordIsConfigured,
    IReadOnlyList<FirstPartyCommunicationPluginResponse> FirstPartyPlugins);

public sealed record FirstPartyCommunicationPluginResponse(
    string Key,
    string PluginId,
    string DisplayName,
    string Description,
    string? RepositoryUrl,
    string? CommitSha,
    string DocumentationUrl,
    string ServicePortalUrl)
{
    public bool IsSourceConfigured =>
        Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(CommitSha) &&
        CommitSha.Length == 40 &&
        CommitSha.All(Uri.IsHexDigit);
}
