using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSweet.Communications.Abstractions;

namespace CSweet.Communications.Discord;

public static class DiscordConstants
{
    public const string Provider = "Discord";
    public const int MaxMessageLength = 2000;
    public const long RequiredPermissions =
        (1L << 4) |   // Manage Channels
        (1L << 28) |  // Manage Roles
        (1L << 29) |  // Manage Webhooks
        (1L << 10) |  // View Channel
        (1L << 11) |  // Send Messages
        (1L << 16) |  // Read Message History
        (1L << 34) |  // Manage Threads
        (1L << 38);   // Send Messages in Threads
}

public static class DiscordOAuthUrlBuilder
{
    public static Uri BuildInstallUri(ulong applicationId, Uri redirectUri, string state) => new(
        $"https://discord.com/oauth2/authorize?client_id={applicationId}&scope=bot%20applications.commands" +
        $"&permissions={DiscordConstants.RequiredPermissions}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri.ToString())}" +
        $"&state={Uri.EscapeDataString(state)}");
}

public static class DiscordMessageFormatter
{
    public static IReadOnlyList<string> Split(string content, int limit = DiscordConstants.MaxMessageLength)
    {
        if (string.IsNullOrEmpty(content)) return [string.Empty];
        var parts = new List<string>();
        var remaining = content;
        while (remaining.Length > limit)
        {
            var split = remaining.LastIndexOf('\n', limit - 1, limit);
            if (split < limit / 2) split = remaining.LastIndexOf(' ', limit - 1, limit);
            if (split < limit / 2) split = limit;
            parts.Add(remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart();
        }
        if (remaining.Length > 0) parts.Add(remaining);
        return parts;
    }

    public static string EscapeMentions(string content) => content
        .Replace("@everyone", "@\u200beveryone", StringComparison.OrdinalIgnoreCase)
        .Replace("@here", "@\u200bhere", StringComparison.OrdinalIgnoreCase);
}

public interface IDiscordWorkspaceClient
{
    Task<CommunicationResult> CreateAsync(string guildId, WorkspaceProvisioningChange change,
        string? parentExternalId, IReadOnlyDictionary<string, string> knownExternalIds, CancellationToken cancellationToken);
    Task<CommunicationResult> UpdateAsync(string guildId, WorkspaceProvisioningChange change, CancellationToken cancellationToken);
    Task<CommunicationResult> ArchiveAsync(string guildId, WorkspaceProvisioningChange change, string? archiveCategoryId, CancellationToken cancellationToken);
}

public sealed class DiscordWorkspaceProvisioner(IDiscordWorkspaceClient client) : IWorkspaceProvisioner, IWorkspaceReconciler
{
    public string Provider => DiscordConstants.Provider;

    public Task<WorkspaceProvisioningPlan> PlanAsync(Guid organizationId, string workspaceExternalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkspaceProvisioningPlan(organizationId, Provider, workspaceExternalId, [], DateTimeOffset.UtcNow));

    public async Task<WorkspaceProvisioningResult> ApplyAsync(WorkspaceProvisioningPlan plan, CancellationToken cancellationToken = default)
    {
        var resources = new List<ProviderResourceDescriptor>();
        var errors = new List<CommunicationError>();
        var externalIds = plan.Changes.Where(x => x.ExternalId is not null).ToDictionary(x => x.Purpose, x => x.ExternalId!, StringComparer.Ordinal);
        foreach (var change in plan.Changes.OrderBy(Priority))
        {
            if (change.Change == CommunicationChangeKind.NoChange)
            {
                if (change.ExternalId is not null) resources.Add(ToDescriptor(change, change.ExternalId));
                continue;
            }
            var parent = ResolveParent(change.Purpose, externalIds);
            var archive = externalIds.GetValueOrDefault("category:archive");
            var result = change.Change switch
            {
                CommunicationChangeKind.Create => await client.CreateAsync(plan.WorkspaceExternalId, change, parent, externalIds, cancellationToken),
                CommunicationChangeKind.Update => await client.UpdateAsync(plan.WorkspaceExternalId, change, cancellationToken),
                CommunicationChangeKind.Archive => await client.ArchiveAsync(plan.WorkspaceExternalId, change, archive, cancellationToken),
                _ => CommunicationResult.Failure("unsupported_change", $"{change.Change} is not supported.")
            };
            if (!result.Succeeded) { errors.Add(result.Error!); continue; }
            var id = result.ExternalId ?? change.ExternalId;
            if (id is null) { errors.Add(new("missing_external_id", $"Discord returned no ID for {change.Purpose}.")); continue; }
            externalIds[change.Purpose] = id;
            resources.Add(ToDescriptor(change, id, parent));
        }
        return new(errors.Count == 0, resources, errors);
    }

    public async Task<WorkspaceProvisioningResult> ReconcileAsync(Guid organizationId, string workspaceExternalId, CancellationToken cancellationToken = default) =>
        await ApplyAsync(await PlanAsync(organizationId, workspaceExternalId, cancellationToken), cancellationToken);

    private static int Priority(WorkspaceProvisioningChange x) => x.Kind switch
    {
        CommunicationResourceKind.Role => 0,
        CommunicationResourceKind.Category => 1,
        CommunicationResourceKind.Channel => 2,
        CommunicationResourceKind.Webhook => 3,
        _ => 4
    };

    private static string? ResolveParent(string purpose, IReadOnlyDictionary<string, string> ids)
    {
        if ((purpose.StartsWith("agent:", StringComparison.Ordinal) || purpose.StartsWith("team:", StringComparison.Ordinal) ||
             purpose.StartsWith("project:", StringComparison.Ordinal)) && purpose.EndsWith(":webhook", StringComparison.Ordinal))
            return ids.GetValueOrDefault(purpose.Replace(":webhook", ":channel", StringComparison.Ordinal));
        if (purpose.StartsWith("agent:", StringComparison.Ordinal)) return ids.GetValueOrDefault("category:agents");
        if (purpose.StartsWith("team:", StringComparison.Ordinal)) return ids.GetValueOrDefault("category:teams");
        if (purpose.StartsWith("project:", StringComparison.Ordinal)) return ids.GetValueOrDefault("category:projects");
        return purpose switch
        {
            "channel:welcome" or "channel:directory" or "channel:help" => ids.GetValueOrDefault("category:start"),
            "channel:inbox" or "channel:approvals" or "channel:decisions" => ids.GetValueOrDefault("category:executive"),
            "channel:activity" or "channel:incidents" => ids.GetValueOrDefault("category:operations"),
            _ => null
        };
    }

    private static ProviderResourceDescriptor ToDescriptor(WorkspaceProvisioningChange change, string id, string? parent = null) =>
        new(DiscordConstants.Provider, change.Kind, id, change.DesiredName, change.Purpose, parent);
}

public sealed class DiscordApiClient(HttpClient httpClient) : IDiscordWorkspaceClient, ICommunicationProvider, IExternalIdentityProvider
{
    public string Provider => DiscordConstants.Provider;

    public Task<CommunicationProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommunicationProviderHealth(Provider, httpClient.DefaultRequestHeaders.Authorization is not null,
            httpClient.DefaultRequestHeaders.Authorization is null ? "NotConfigured" : "Configured", DateTimeOffset.UtcNow));

    public async Task<CommunicationResult> AssignMemberAsync(string workspaceExternalId, string externalUserId,
        string memberRoleExternalId, CancellationToken cancellationToken = default) =>
        await ToResultAsync(await httpClient.PutAsync($"guilds/{workspaceExternalId}/members/{externalUserId}/roles/{memberRoleExternalId}",
            null, cancellationToken), cancellationToken, memberRoleExternalId);

    public async Task<CommunicationResult> SendAsync(OutboundCommunicationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        string? firstId = null;
        foreach (var part in DiscordMessageFormatter.Split(DiscordMessageFormatter.EscapeMentions(envelope.Content)))
        {
            var response = await httpClient.PostAsJsonAsync($"channels/{envelope.DestinationExternalId}/messages", new
            {
                content = part,
                message_reference = envelope.ReplyToExternalId is null ? null : new { message_id = envelope.ReplyToExternalId },
                allowed_mentions = new { parse = Array.Empty<string>() }
            }, cancellationToken);
            var result = await ToResultAsync(response, cancellationToken);
            if (!result.Succeeded) return result;
            firstId ??= result.ExternalId;
        }
        return CommunicationResult.Success(firstId);
    }

    public async Task<CommunicationResult> CreateAsync(string guildId, WorkspaceProvisioningChange change, string? parentExternalId,
        IReadOnlyDictionary<string, string> knownExternalIds, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        if (change.Kind == CommunicationResourceKind.Role)
            response = await httpClient.PostAsJsonAsync($"guilds/{guildId}/roles", new { name = change.DesiredName, mentionable = change.Purpose.StartsWith("agent:", StringComparison.Ordinal), permissions = "0" }, cancellationToken);
        else if (change.Kind == CommunicationResourceKind.Webhook)
        {
            if (parentExternalId is null) return CommunicationResult.Failure("missing_parent", "A Discord webhook requires a channel.");
            response = await httpClient.PostAsJsonAsync($"channels/{parentExternalId}/webhooks", new { name = $"C-Sweet - {change.DesiredName}" }, cancellationToken);
            return await ToWebhookResultAsync(response, cancellationToken);
        }
        else if (change.Kind == CommunicationResourceKind.Category)
        {
            var botId = await GetBotUserIdAsync(cancellationToken);
            if (botId is null) return CommunicationResult.Failure("bot_identity_unavailable", "Discord did not return the bot identity.", true);
            var overwrites = new List<object>
            {
                new { id = guildId, type = 0, allow = "0", deny = (1L << 10).ToString() },
                new { id = botId, type = 1, allow = DiscordConstants.RequiredPermissions.ToString(), deny = "0" }
            };
            var ownerRole = knownExternalIds.GetValueOrDefault("role:owner");
            var memberRole = knownExternalIds.GetValueOrDefault("role:member");
            if (ownerRole is not null) overwrites.Add(new { id = ownerRole, type = 0, allow = ((1L << 10) | (1L << 11) | (1L << 16)).ToString(), deny = "0" });
            if (memberRole is not null && change.Purpose != "category:executive")
                overwrites.Add(new { id = memberRole, type = 0, allow = ((1L << 10) | (1L << 11) | (1L << 16)).ToString(), deny = "0" });
            response = await httpClient.PostAsJsonAsync($"guilds/{guildId}/channels", new
            {
                name = change.DesiredName, type = 4, permission_overwrites = overwrites
            }, cancellationToken);
        }
        else
            response = await httpClient.PostAsJsonAsync($"guilds/{guildId}/channels", new
            {
                name = change.DesiredName,
                type = change.Kind == CommunicationResourceKind.Category ? 4 : 0,
                parent_id = change.Kind == CommunicationResourceKind.Channel ? parentExternalId : null,
                topic = change.Kind == CommunicationResourceKind.Channel ? $"Managed by C-Sweet ({change.Purpose})." : null
            }, cancellationToken);
        return await ToResultAsync(response, cancellationToken);
    }

    private async Task<string?> GetBotUserIdAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("users/@me", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    public async Task<CommunicationResult> SendWebhookAsync(string webhookId, string webhookToken, OutboundCommunicationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        string? firstId = null;
        foreach (var part in DiscordMessageFormatter.Split(DiscordMessageFormatter.EscapeMentions(envelope.Content)))
        {
            var response = await httpClient.PostAsJsonAsync($"webhooks/{webhookId}/{webhookToken}?wait=true", new
            {
                content = part, username = envelope.PersonaName, avatar_url = envelope.PersonaAvatarUrl,
                allowed_mentions = new { parse = Array.Empty<string>() }
            }, cancellationToken);
            var result = await ToResultAsync(response, cancellationToken);
            if (!result.Succeeded) return result;
            firstId ??= result.ExternalId;
        }
        return CommunicationResult.Success(firstId);
    }

    public async Task<CommunicationResult> UpdateAsync(string guildId, WorkspaceProvisioningChange change, CancellationToken cancellationToken)
    {
        if (change.ExternalId is null) return CommunicationResult.Failure("missing_external_id", "Cannot update a resource without an external ID.");
        var route = change.Kind == CommunicationResourceKind.Role ? $"guilds/{guildId}/roles/{change.ExternalId}" : $"channels/{change.ExternalId}";
        var response = await httpClient.PatchAsJsonAsync(route, new { name = change.DesiredName }, cancellationToken);
        return await ToResultAsync(response, cancellationToken);
    }

    public async Task<CommunicationResult> ArchiveAsync(string guildId, WorkspaceProvisioningChange change, string? archiveCategoryId, CancellationToken cancellationToken)
    {
        if (change.ExternalId is null) return CommunicationResult.Failure("missing_external_id", "Cannot archive a resource without an external ID.");
        if (change.Kind == CommunicationResourceKind.Webhook)
            return await ToResultAsync(await httpClient.DeleteAsync($"webhooks/{change.ExternalId}", cancellationToken), cancellationToken, change.ExternalId);
        var route = change.Kind == CommunicationResourceKind.Role ? $"guilds/{guildId}/roles/{change.ExternalId}" : $"channels/{change.ExternalId}";
        var response = await httpClient.PatchAsJsonAsync(route, new
        {
            name = change.DesiredName.StartsWith("archived-", StringComparison.Ordinal) ? change.DesiredName : $"archived-{change.DesiredName}",
            parent_id = change.Kind == CommunicationResourceKind.Channel ? archiveCategoryId : null
        }, cancellationToken);
        return await ToResultAsync(response, cancellationToken);
    }

    public static HttpClient CreateHttpClient(string botToken)
    {
        var client = new HttpClient { BaseAddress = new Uri("https://discord.com/api/v10/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordBot (https://csweet.ai, 1.0)");
        return client;
    }

    private static async Task<CommunicationResult> ToResultAsync(HttpResponseMessage response, CancellationToken cancellationToken, string? fallbackId = null)
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent) return CommunicationResult.Success(fallbackId);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return CommunicationResult.Success(json.RootElement.TryGetProperty("id", out var id) ? id.GetString() : fallbackId);
        }
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var retryAfter = response.Headers.RetryAfter?.Delta is { } delta ? DateTimeOffset.UtcNow.Add(delta) : (DateTimeOffset?)null;
        return CommunicationResult.Failure(response.StatusCode == HttpStatusCode.TooManyRequests ? "rate_limited" : "discord_api_error",
            $"Discord returned {(int)response.StatusCode}: {body}", response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500,
            retryAfter is null ? null : new CommunicationRateLimit(retryAfter));
    }

    private static async Task<CommunicationResult> ToWebhookResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode) return await ToResultAsync(response, cancellationToken);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var id = json.RootElement.GetProperty("id").GetString();
        var token = json.RootElement.GetProperty("token").GetString();
        return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(token)
            ? CommunicationResult.Failure("invalid_webhook", "Discord returned an incomplete webhook.")
            : CommunicationResult.Success($"{id}|{token}");
    }
}
