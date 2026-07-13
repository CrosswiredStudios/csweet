# Phase 4 — Chat API and SSE streaming

## Goal

Add the single endpoint the browser calls to send a message and receive a streamed reply. It
ties together Phase 1 (persistence), Phase 2 (the streaming agent), and Phase 3 (the governed
gateway):

1. Run governance checks.
2. Persist the user's message.
3. Publish `user.message.received` to the broker.
4. Stream the assistant's chunk events back to the browser as **Server-Sent Events (SSE)**.
5. Persist the assembled assistant message when the stream finishes.

## Why SSE

SSE is just an HTTP response with `Content-Type: text/event-stream` that stays open and pushes
`data:` lines as they are produced. It needs **no extra packages**, works through the existing
CORS setup, and is trivial for a Blazor WASM client to read. We do not need the bidirectional
complexity of SignalR for "type a message, watch the answer appear."

## Prerequisites

- Phase 1 merged: `IConversationService` with `AppendMessageAsync` / `GetAsync`.
- Phase 3 merged: `IAgentBrokerClient` (gateway connection), `IChatStreamRouter`,
  `ApiGatewayOptions`.
- The Personal Assistant from Phase 2 is publishing chunk + final events.

## The request/response shape

```
POST /api/core/organizations/{organizationId}/conversations/{conversationId}/messages/stream
Content-Type: application/json

{ "message": "What is on my plate today?" }

---> 200 OK
     Content-Type: text/event-stream

     data: {"sequence":0,"delta":"You ","isFinal":false}

     data: {"sequence":1,"delta":"have ","isFinal":false}

     data: {"sequence":2,"delta":"3 tasks.","isFinal":false}

     data: {"sequence":3,"delta":"","isFinal":true}
```

Each `data:` line is one JSON chunk. The client appends `delta`s until it sees `isFinal: true`.

## Step-by-step

### 1. Request DTO and the provider-selection helper

Create `src/CSweet.Contracts/Core/SendChatMessageRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record SendChatMessageRequest(
    [property: Required] string Message);
```

The agent needs a `ProviderProfileId` (which LLM to use). For now, pick the first **enabled**
`LlmProviderProfile`. Add a helper method to `IConversationService` (or a tiny dedicated
service) — the simplest is to query it inside the endpoint via the existing
`ILlmProviderProfileService` / `CSweetDbContext`. Expose a method that returns the default
provider id or `null`:

```csharp
// In IConversationService (Application) + ConversationService (Infrastructure)
Task<Guid?> GetDefaultProviderProfileIdAsync(CancellationToken cancellationToken = default);
```

Implementation (Infrastructure):

```csharp
public async Task<Guid?> GetDefaultProviderProfileIdAsync(CancellationToken cancellationToken = default)
{
    var profile = await _dbContext.LlmProviderProfiles
        .Where(x => x.IsEnabled)
        .OrderBy(x => x.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken);

    return profile?.Id;
}
```

> If no provider is configured we cannot chat. The endpoint returns a clear error so the UI can
> tell the user to finish setup.

### 2. Resolve the agent id for the target employee

The URL identifies the **agent employee** (`OrganizationUser`), but the broker speaks in **agent
ids** (`com.csweet.personal-assistant`). For this feature there is exactly one wired agent, so:

- Validate the target `OrganizationUser` exists in the org, is `EmployeeType.Agent`, and is not
  the "Self" user.
- Map it to the Personal Assistant agent id. A pragmatic first mapping: **treat every agent
  employee as the Personal Assistant** for now, and leave a `// TODO` to store the concrete
  agent id on the `OrganizationUser`/`Worker` when more agents exist.

Publishing an event does not name a target agent — the broker delivers it to every subscriber
in the business. Since the Personal Assistant is the only subscriber to
`user.message.received`, the message reaches it. Document this limitation (see "Known
limitations").

### 3. The streaming endpoint

Create `src/CSweet.Api/Chat/ChatMessageEndpoints.cs`:

```csharp
using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Agents.PersonalAssistant; // UserMessageReceived + event constants
using CSweet.Api.Chat;
using CSweet.Application.Core;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using Google.Protobuf;

namespace CSweet.Api.Chat;

public static class ChatMessageEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatMessageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/core/organizations/{organizationId:guid}/conversations/{conversationId:guid}/messages/stream",
            StreamAsync);

        return endpoints;
    }

    private static async Task StreamAsync(
        Guid organizationId,
        Guid conversationId,
        SendChatMessageRequest request,
        HttpContext http,
        IConversationService conversations,
        IAgentBrokerClient broker,
        IChatStreamRouter router,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error = "Message is required." }, cancellationToken);
            return;
        }

        // --- Governance / validation -------------------------------------------------
        var conversation = await conversations.GetAsync(conversationId, cancellationToken);
        if (conversation is null || conversation.OrganizationId != organizationId)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var providerId = await conversations.GetDefaultProviderProfileIdAsync(cancellationToken);
        if (providerId is null)
        {
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            await http.Response.WriteAsJsonAsync(
                new { error = "No enabled LLM provider is configured. Finish setup first." },
                cancellationToken);
            return;
        }

        // --- Persist the user's turn -------------------------------------------------
        await conversations.AppendMessageAsync(conversationId, ConversationRole.User, request.Message, cancellationToken);

        // --- Begin listening BEFORE publishing (avoid a race) ------------------------
        var reader = router.Subscribe(conversationId);

        // --- Publish the governed event to the broker --------------------------------
        var payload = new UserMessageReceived(
            providerId.Value,
            conversationId.ToString(),
            conversation.InitiatedByOrganizationUserId.ToString(),
            request.Message,
            Context: null);

        await broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = PersonalAssistantProfile.UserMessageReceivedEvent,
                SchemaVersion = "1",
                Subject = $"conversation/{conversationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions))
            },
            conversationId.ToString(),
            cancellationToken);

        // --- Stream chunks to the browser as SSE -------------------------------------
        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no"; // disable proxy buffering (nginx)

        var assembled = new System.Text.StringBuilder();

        try
        {
            await foreach (var chunk in reader.ReadAllAsync(cancellationToken))
            {
                if (!chunk.IsFinal && chunk.Delta.Length > 0)
                {
                    assembled.Append(chunk.Delta);
                }

                var json = JsonSerializer.Serialize(
                    new { sequence = chunk.Sequence, delta = chunk.Delta, isFinal = chunk.IsFinal },
                    SerializerOptions);

                await http.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await http.Response.Body.FlushAsync(cancellationToken);

                if (chunk.IsFinal)
                {
                    break;
                }
            }
        }
        finally
        {
            router.Complete(conversationId);
        }

        // --- Persist the assembled assistant turn ------------------------------------
        if (assembled.Length > 0)
        {
            await conversations.AppendMessageAsync(
                conversationId, ConversationRole.Assistant, assembled.ToString(), CancellationToken.None);
        }
    }
}
```

Register it in [src/CSweet.Api/Program.cs](../../../../src/CSweet.Api/Program.cs):

```csharp
app.MapChatMessageEndpoints();
```

> **Ordering matters.** We `Subscribe` to the router *before* publishing the event, so a fast
> agent cannot produce chunks before anyone is listening. The router uses a bounded channel, so
> early chunks are buffered until the SSE loop starts reading.

### 4. A timeout safety net

If the agent never responds (misconfigured provider, crash), the SSE loop would hang forever.
Wrap the read loop with a linked cancellation token that also trips after a sensible timeout
(e.g. 90 seconds), and on timeout write a final error `data:` line before closing:

```csharp
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));
// use timeoutCts.Token in ReadAllAsync; catch OperationCanceledException to emit a friendly error
```

Keep it simple — one timeout, one error chunk, then close.

### 5. CORS / streaming through the dev proxy

- The existing dev CORS policy (`DevelopmentBlazorApp`) allows the Blazor origin; SSE is a
  normal GET/POST response, so no CORS change is needed.
- If a reverse proxy sits in front (nginx in
  [docker/nginx.conf](../../../../docker/nginx.conf)), ensure it does not buffer the event
  stream. The `X-Accel-Buffering: no` header above handles nginx; for other proxies disable
  response buffering for this route.

## Known limitations (document these in your PR)

- **Targeting.** Publishing `user.message.received` reaches every subscriber in the business.
  With one agent that is correct, but it is not true point-to-point addressing. When a second
  agent is added, switch to a targeted mechanism (e.g. include the target agent id in the
  event subject and have agents filter, or use a capability invocation with a streaming
  contract). Leave a `// TODO(targeting)` at the publish site.
- **Concurrency per conversation.** The router keys by conversation id, so two overlapping
  sends on the *same* conversation would interleave. The UI (Phase 5) disables the input while
  a reply streams, which prevents this in practice.

## Testing

- **Integration test** (`tests/CSweet.IntegrationTests`, `WebApplicationFactory`): with a fake
  agent/provider, POST to the stream endpoint and assert the response content type is
  `text/event-stream`, that multiple `data:` lines arrive, and that the final line has
  `isFinal:true`. Afterwards, `GET .../messages` returns both the user and assistant messages.
- **Validation tests:** empty message → 400; unknown conversation → 404; no enabled provider →
  409 with a helpful error.
- **Manual (LM Studio / real provider):** run the app, create a conversation via Phase 1
  endpoint, then `curl -N` the stream endpoint and watch tokens arrive live.

```powershell
curl -N -X POST `
  "http://localhost:<api-port>/api/core/organizations/<orgId>/conversations/<convId>/messages/stream" `
  -H "Content-Type: application/json" `
  -d '{ "message": "Say hello in five words." }'
```

## Acceptance criteria

- [ ] The stream endpoint validates input, conversation ownership, and provider availability.
- [ ] The user message is persisted before the event is published.
- [ ] The endpoint subscribes to the router before publishing (no lost early chunks).
- [ ] Responses are `text/event-stream`, flushed per chunk, ending on `isFinal:true`.
- [ ] The assembled assistant message is persisted after the stream completes.
- [ ] A stalled agent trips the timeout and closes gracefully with an error chunk.
- [ ] Integration + validation tests pass.

## Definition of done

`curl -N` against the endpoint streams the assistant's reply token-by-token from a real (or
fake) model, and both turns are afterward retrievable via the Phase 1 messages endpoint.
