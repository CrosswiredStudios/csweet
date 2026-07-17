using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSweet.Communications.Abstractions;
using Microsoft.Extensions.Configuration;

namespace CSweet.Infrastructure.Communications;

public sealed class HttpCommunicationRelayClient(HttpClient http, IConfiguration configuration) : ICommunicationRelayClient
{
    public async IAsyncEnumerable<NormalizedCommunicationEnvelope> ReadInboundAsync(Guid pairingId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = Create(HttpMethod.Get, $"api/v1/pairings/{pairingId:D}/inbound");
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var envelopes = await response.Content.ReadFromJsonAsync<List<NormalizedCommunicationEnvelope>>(cancellationToken: cancellationToken) ?? [];
        foreach (var envelope in envelopes) yield return envelope;
    }

    public async Task AcknowledgeAsync(Guid pairingId, Guid envelopeId, CancellationToken cancellationToken = default)
    {
        using var request = Create(HttpMethod.Post, $"api/v1/pairings/{pairingId:D}/inbound/{envelopeId:D}/ack");
        request.Content = JsonContent.Create(new { });
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CommunicationResult> SendAsync(Guid pairingId, OutboundCommunicationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        using var request = Create(HttpMethod.Post, $"api/v1/pairings/{pairingId:D}/outbound");
        request.Content = JsonContent.Create(envelope);
        using var response = await http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CommunicationResult>(cancellationToken: cancellationToken) ?? CommunicationResult.Failure("invalid_relay_response", "The relay returned no delivery result.")
            : CommunicationResult.Failure("relay_error", $"Relay returned {(int)response.StatusCode}.", (int)response.StatusCode >= 500);
    }

    public async Task<WorkspaceProvisioningResult> ApplyProvisioningAsync(Guid pairingId, WorkspaceProvisioningPlan plan, CancellationToken cancellationToken = default)
    {
        using var request = Create(HttpMethod.Post, $"api/v1/pairings/{pairingId:D}/provision");
        request.Content = JsonContent.Create(plan);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new(false, [], [new CommunicationError("relay_error", $"Relay returned {(int)response.StatusCode}.", (int)response.StatusCode >= 500)]);
        return await response.Content.ReadFromJsonAsync<WorkspaceProvisioningResult>(cancellationToken: cancellationToken)
            ?? new(false, [], [new CommunicationError("invalid_relay_response", "The relay returned no provisioning result.")]);
    }

    public async Task RegisterLinkCodeAsync(Guid pairingId, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        using var request = Create(HttpMethod.Post, $"api/v1/pairings/{pairingId:D}/link-codes");
        request.Content = JsonContent.Create(new { code, expiresAt });
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CommunicationResult> AssignMemberAsync(Guid pairingId, string workspaceExternalId, string externalUserId,
        string memberRoleExternalId, CancellationToken cancellationToken = default)
    {
        using var request = Create(HttpMethod.Post, $"api/v1/pairings/{pairingId:D}/members/assign");
        request.Content = JsonContent.Create(new { workspaceExternalId, externalUserId, memberRoleExternalId });
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunicationResult>(cancellationToken: cancellationToken)
            ?? CommunicationResult.Failure("invalid_relay_response", "Relay returned no identity assignment result.");
    }

    private HttpRequestMessage Create(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        var token = configuration["Communications:Relay:AccessToken"];
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
