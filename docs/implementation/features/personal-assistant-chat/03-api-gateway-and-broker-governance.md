# Phase 3 — API gateway and broker governance

## Goal

Connect `CSweet.Api` to the agent broker as a **registered, governed principal** named
`com.csweet.api-gateway`. The gateway can **publish** the user's message event and **receive**
the assistant's chunk/final events. It holds one long-lived broker connection and fans received
events out to whichever HTTP request is waiting for them.

No user-facing endpoint yet — that is Phase 4. This phase builds the pipe and the governance
around it.

## Why this phase matters

This is the **security boundary** the product is being designed around. The browser must never
talk to an agent directly. Instead:

- The API registers with the broker and only gets the grants that **configuration** allows
  (deny-by-default; grant = manifest/registration **intersected** with config).
- Later, "which user may talk to which agent and use which capability" is enforced here and in
  the endpoint (Phase 4). Building the gateway now, for one agent, establishes that choke point.

If you skip this and let the UI hit the broker, you would have to retrofit governance across
every client. Doing it once, in the API, is the whole point.

## How the broker governs principals (context)

The broker authorizes every registration with
[src/CSweet.AgentHost/Broker/ConfiguredAgentAuthorizationPolicy.cs](../../../../src/CSweet.AgentHost/Broker/ConfiguredAgentAuthorizationPolicy.cs)
using the `CSweet:AgentBroker` section in
[src/CSweet.AgentHost/appsettings.json](../../../../src/CSweet.AgentHost/appsettings.json).
A principal that is not listed, not `Enabled`, or not permitted for the `BusinessId` is
rejected. Granted capabilities/subscriptions/publications are the **intersection** of what the
principal requests and what config lists.

So to let the API act as a gateway we must **add a config entry** for `com.csweet.api-gateway`.
That entry is the governance policy.

## Deliverables

- Project references so the API can use the broker client.
- A config entry authorizing `com.csweet.api-gateway` (publish user message; subscribe to
  chunk + final events).
- A hosted `ApiGatewayBrokerWorker` that connects, registers, and dispatches inbound events.
- An in-memory `IChatStreamRouter` that routes chunk events to the waiting HTTP request by
  conversation id.
- AppHost wiring so the API can discover the broker.

## Step-by-step

### 1. Reference the broker client from the API

The API does not currently reference the agent SDK. Add both references to
[src/CSweet.Api/CSweet.Api.csproj](../../../../src/CSweet.Api/CSweet.Api.csproj):

```xml
<ItemGroup>
  <ProjectReference Include="..\CSweet.Agent.SDK\CSweet.Agent.SDK.csproj" />
  <ProjectReference Include="..\CSweet.Agent.Contracts\CSweet.Agent.Contracts.csproj" />
</ItemGroup>
```

`CSweet.Agent.SDK` gives you `IAgentBrokerClient` / `GrpcAgentBrokerClient`;
`CSweet.Agent.Contracts` gives you the generated gRPC message types (`RegisterAgent`,
`PublishEvent`, `DeliveredEvent`, etc.).

### 2. Authorize the gateway principal in broker config

In [src/CSweet.AgentHost/appsettings.json](../../../../src/CSweet.AgentHost/appsettings.json),
add a second agent entry under `CSweet:AgentBroker:Agents`:

```json
"com.csweet.api-gateway": {
  "Enabled": true,
  "AllowedBusinessIds": [ "default" ],
  "Capabilities": [],
  "Subscriptions": [
    "com.csweet.assistant.response.chunk.v1",
    "com.csweet.assistant.response.created.v1"
  ],
  "Publications": [
    "com.csweet.user.message.received.v1"
  ]
}
```

This is the governance statement: *the gateway may publish user messages and may hear
assistant responses — nothing else.* It provides no capabilities and cannot receive other
agents' internal events.

> The Personal Assistant already subscribes to `com.csweet.user.message.received.v1` in its
> config, so it will receive what the gateway publishes.

### 3. Gateway options

Create `src/CSweet.Api/Chat/ApiGatewayOptions.cs`:

```csharp
namespace CSweet.Api.Chat;

public sealed class ApiGatewayOptions
{
    public const string SectionName = "CSweet:ApiGateway";

    public string AgentId { get; set; } = "com.csweet.api-gateway";
    public string Version { get; set; } = "0.1.0";
    public string BrokerEndpoint { get; set; } = "https+http://agenthost";
    public string InstallationId { get; set; } = $"api-{Environment.MachineName}";
    public string BusinessId { get; set; } = "default";
}
```

> `BusinessId = "default"` matches the `AllowedBusinessIds` in the broker config and the
> agent's own default. Multi-business identity is out of scope for this feature.

### 4. In-memory stream router

The gateway holds **one** broker connection but may serve **many** simultaneous chat requests.
When a chunk event arrives, we must hand it to the correct waiting HTTP request. Route by
`ConversationId` using bounded channels.

Create `src/CSweet.Api/Chat/IChatStreamRouter.cs`:

```csharp
namespace CSweet.Api.Chat;

/// <summary>A single streamed delta arriving from an agent.</summary>
public sealed record ChatStreamChunk(int Sequence, string Delta, bool IsFinal);

public interface IChatStreamRouter
{
    /// <summary>Called by an HTTP request to begin listening for a conversation's chunks.</summary>
    ChannelReader<ChatStreamChunk> Subscribe(Guid conversationId);

    /// <summary>Called by the broker worker when a chunk event arrives.</summary>
    void Publish(Guid conversationId, ChatStreamChunk chunk);

    /// <summary>Called by the HTTP request when it is done (final chunk or client disconnect).</summary>
    void Complete(Guid conversationId);
}
```

Create `src/CSweet.Api/Chat/ChatStreamRouter.cs`:

```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CSweet.Api.Chat;

public sealed class ChatStreamRouter : IChatStreamRouter
{
    private readonly ConcurrentDictionary<Guid, Channel<ChatStreamChunk>> _channels = new();

    public ChannelReader<ChatStreamChunk> Subscribe(Guid conversationId)
    {
        var channel = _channels.GetOrAdd(conversationId, _ =>
            Channel.CreateBounded<ChatStreamChunk>(new BoundedChannelOptions(256)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            }));

        return channel.Reader;
    }

    public void Publish(Guid conversationId, ChatStreamChunk chunk)
    {
        if (_channels.TryGetValue(conversationId, out var channel))
        {
            // Best-effort; drop if the reader is gone.
            channel.Writer.TryWrite(chunk);

            if (chunk.IsFinal)
            {
                channel.Writer.TryComplete();
            }
        }
    }

    public void Complete(Guid conversationId)
    {
        if (_channels.TryRemove(conversationId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}
```

Register it as a **singleton** (it is shared between the background worker and request handlers).

### 5. The hosted gateway worker

This background service owns the broker connection: it registers as the gateway principal,
loops over inbound broker messages, and forwards chunk events into the router. It also exposes
the `IAgentBrokerClient` so endpoints can publish user messages on the same connection.

Create `src/CSweet.Api/Chat/ApiGatewayBrokerWorker.cs`:

```csharp
using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Agents.PersonalAssistant; // for AssistantResponseChunk + event-type constants
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CSweet.Api.Chat;

public sealed class ApiGatewayBrokerWorker : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAgentBrokerClient _broker;
    private readonly IChatStreamRouter _router;
    private readonly ApiGatewayOptions _options;
    private readonly ILogger<ApiGatewayBrokerWorker> _logger;

    public ApiGatewayBrokerWorker(
        IAgentBrokerClient broker,
        IChatStreamRouter router,
        IOptions<ApiGatewayOptions> options,
        ILogger<ApiGatewayBrokerWorker> logger)
    {
        _broker = broker;
        _router = router;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var registration = new RegisterAgent
        {
            AgentId = _options.AgentId,
            AgentVersion = _options.Version,
            InstallationId = _options.InstallationId,
            BusinessId = _options.BusinessId
        };
        registration.RequestedPublications.Add(PersonalAssistantProfile.UserMessageReceivedEvent);
        registration.RequestedSubscriptions.Add(PersonalAssistantProfile.AssistantResponseChunkEvent);
        registration.RequestedSubscriptions.Add(PersonalAssistantProfile.AssistantResponseCreatedEvent);

        await _broker.StartAsync(registration, stoppingToken);
        _logger.LogInformation("API gateway registered with broker as {AgentId}.", _options.AgentId);

        await foreach (var message in _broker.ReadAllAsync(stoppingToken))
        {
            if (message.PayloadCase != BrokerToAgentMessage.PayloadOneofCase.Event)
            {
                continue;
            }

            var evt = message.Event;
            if (evt.EventType != PersonalAssistantProfile.AssistantResponseChunkEvent)
            {
                continue;
            }

            var chunk = JsonSerializer.Deserialize<AssistantResponseChunk>(
                evt.Payload.ToByteArray(), SerializerOptions);

            if (chunk is null || !Guid.TryParse(chunk.ConversationId, out var conversationId))
            {
                continue;
            }

            _router.Publish(conversationId, new ChatStreamChunk(chunk.Sequence, chunk.Delta, chunk.IsFinal));
        }
    }
}
```

> **Why reference the plugin's contracts?** `AssistantResponseChunk` and the event-type
> constants live in `CSweet.Agents.PersonalAssistant`. Add a project reference from the API to
> the plugin **for these shared contracts only**. If you prefer not to couple the API to the
> plugin executable, extract `AssistantResponseChunk` + the event-type constants into a small
> shared contracts project (e.g. `CSweet.Agent.Contracts` or a new `CSweet.Agents.Shared`) and
> reference that from both. Pick one and note it in your PR. The simplest first step is a direct
> project reference; the cleaner long-term move is a shared contracts library.

### 6. Register gateway services

Create `src/CSweet.Api/Chat/ChatGatewayServiceCollectionExtensions.cs`:

```csharp
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using Microsoft.Extensions.DependencyInjection;

namespace CSweet.Api.Chat;

public static class ChatGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddChatGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiGatewayOptions>()
            .Bind(configuration.GetSection(ApiGatewayOptions.SectionName))
            .ValidateOnStart();

        var brokerEndpoint = configuration[$"{ApiGatewayOptions.SectionName}:BrokerEndpoint"]
            ?? "https+http://agenthost";

        services.AddGrpcClient<AgentBroker.AgentBrokerClient>(options =>
        {
            options.Address = DependencyInjection.CreateGrpcAddress(brokerEndpoint);
        });

        services.AddSingleton<IAgentBrokerClient, GrpcAgentBrokerClient>();
        services.AddSingleton<IChatStreamRouter, ChatStreamRouter>();
        services.AddHostedService<ApiGatewayBrokerWorker>();

        return services;
    }
}
```

`DependencyInjection.CreateGrpcAddress(...)` is the existing helper in
[src/CSweet.Agent.SDK/DependencyInjection.cs](../../../../src/CSweet.Agent.SDK/DependencyInjection.cs)
that resolves Aspire composite schemes like `https+http://` (it is `internal static`; either
call it via `[InternalsVisibleTo]`, or copy the tiny scheme-splitting logic locally). Copying
the few lines is acceptable and avoids widening visibility.

Wire it up in [src/CSweet.Api/Program.cs](../../../../src/CSweet.Api/Program.cs):

```csharp
builder.Services.AddChatGateway(builder.Configuration);
```

Add the gateway config to
[src/CSweet.Api/appsettings.json](../../../../src/CSweet.Api/appsettings.json):

```json
"CSweet": {
  "ApiGateway": {
    "AgentId": "com.csweet.api-gateway",
    "BrokerEndpoint": "https+http://agenthost",
    "BusinessId": "default"
  }
}
```

### 7. Let the API discover the broker (AppHost)

The API currently has no reference to the broker resource, so Aspire cannot resolve
`https+http://agenthost` from the API. In
[src/CSweet.AppHost/Program.cs](../../../../src/CSweet.AppHost/Program.cs) add
`.WithReference(agentHost)` (and wait for it):

```csharp
var api = builder.AddProject<Projects.CSweet_Api>("api")
    .WithReference(postgres)
    .WithReference(agentHost)
    .WaitFor(postgres)
    .WaitFor(agentHost)
    .WaitForCompletion(migrator);
```

## Resilience notes (keep it simple, but do these)

- If `StartAsync` throws (broker not up yet), the hosted service will crash the host. Wrap the
  connect/loop in a try/catch with a short delay-and-retry loop so the API tolerates the broker
  starting slightly later. Do not build anything fancy — a `while (!stoppingToken.IsCancellationRequested)`
  around connect + read, with a `Task.Delay` on failure, is enough.
- The router drops chunks for conversations nobody is listening to. That is fine: if the HTTP
  request already finished or never started, there is no one to receive them.

## Testing

- **Unit-test the router** (`ChatStreamRouter`): subscribing returns a reader; publishing
  delivers chunks in order; a final chunk completes the reader; publishing to an unknown
  conversation is a no-op.
- **Integration smoke test** (manual, via Aspire): start the app, watch logs for
  "API gateway registered with broker as com.csweet.api-gateway". If the broker rejects it,
  the log will show the rejection reason — usually a missing/mismatched config entry from step 2.

## Acceptance criteria

- [ ] `CSweet.Api` references the agent SDK + contracts and builds.
- [ ] Broker config authorizes `com.csweet.api-gateway` with exactly the intended grants.
- [ ] `ApiGatewayBrokerWorker` registers on startup and logs success.
- [ ] `IChatStreamRouter` is a singleton, unit-tested, and routes chunks by conversation id.
- [ ] AppHost gives the API a reference to `agenthost`; the API resolves the broker endpoint.
- [ ] The gateway tolerates the broker starting after the API (retry, no crash loop).

## Definition of done

With the whole app running under Aspire, the API establishes and keeps a broker connection as
the governed gateway principal, and inbound assistant chunk events are decoded and routed to
the in-memory router — ready for Phase 4 to consume.
