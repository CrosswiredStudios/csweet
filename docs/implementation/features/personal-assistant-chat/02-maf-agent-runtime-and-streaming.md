# Phase 2 — MAF agent runtime and streaming (plugin only)

## Goal

Make the Personal Assistant generate its reply with the **Microsoft Agent Framework (MAF)**
and **stream** that reply as a series of small "chunk" events, instead of producing one big
response at the end.

All of this happens **inside the `CSweet.Agents.PersonalAssistant` project**. This is a hard
rule: the agent is a plugin, and its brain lives in the plugin.

## Why this phase matters

Two things the product needs come together here:

1. **MAF as the agent runtime.** Today the assistant calls a shared `IAgentRunner` that does a
   single `IChatClient.GetResponseAsync(...)`. That is not the Agent Framework — it is a raw
   chat call. We want the assistant to be a real MAF agent (`ChatClientAgent`) so it can later
   grow threads, tools, and middleware without re-plumbing.
2. **Streaming.** A chat that only shows text after the whole reply is generated feels broken.
   MAF agents expose `RunStreamingAsync(...)`, which yields incremental updates. We forward
   each update to the broker as a chunk event so the gateway (Phase 3/4) can relay it to the
   browser.

## Plugin boundary — read this before you code

- **Do put in `CSweet.Agents.PersonalAssistant`:** the MAF `ChatClientAgent` with `AgentSession`
  management, the streaming loop, prompt assembly, and publishing chunk/final events.
- **Do reuse (thin seam only):** `ILlmProviderFactory.CreateChatClientAsync(providerProfileId)`
  to obtain an `IChatClient` for the configured LLM provider. The plugin already references
  `CSweet.Infrastructure` and calls `builder.AddCSweetInfrastructure()` in
  [src/CSweet.Agents.PersonalAssistant/Program.cs](../../../../src/CSweet.Agents.PersonalAssistant/Program.cs),
  so this factory is resolvable.
- **Do NOT:** move MAF orchestration into `CSweet.AI` / `IAgentRunner`, or make the plugin
  depend on the shared runner for conversation. The shared `AgentFrameworkAgentRunner` stays
  as-is for other in-process uses (planning). Leaving it untouched is correct.

## Deliverables

- `Microsoft.Agents.AI` core package added centrally and referenced by the plugin.
  This package provides `ChatClientAgent`, `AIAgent`, `AgentSession`, and all agent runtime
  primitives. No provider-specific sub-package (like `Microsoft.Agents.AI.Foundry`) is needed
  because we obtain `IChatClient` from our own `ILlmProviderFactory`.
- A new streaming chunk event type + a message contract for it.
- The Personal Assistant builds a `ChatClientAgent` and streams via `RunStreamingAsync`.
- Manifest and broker config updated to allow publishing the new chunk event.

## Background: what the plugin looks like today

- Entry point registers the agent:
  [src/CSweet.Agents.PersonalAssistant/Program.cs](../../../../src/CSweet.Agents.PersonalAssistant/Program.cs)
  → `builder.AddCSweetAgent<PersonalAssistantAgent>();`
- The agent handles the user message event and publishes one final response:
  [src/CSweet.Agents.PersonalAssistant/PersonalAssistantAgent.cs](../../../../src/CSweet.Agents.PersonalAssistant/PersonalAssistantAgent.cs)
  → `HandleEventAsync` deserializes `UserMessageReceived`, calls `GenerateResponseAsync`,
  then `PublishEventAsync(AssistantResponseCreated)`.
- Message contracts and profile constants:
  [src/CSweet.Agents.PersonalAssistant/Messages.cs](../../../../src/CSweet.Agents.PersonalAssistant/Messages.cs),
  [src/CSweet.Agents.PersonalAssistant/PersonalAssistantProfile.cs](../../../../src/CSweet.Agents.PersonalAssistant/PersonalAssistantProfile.cs)

We will change `GenerateResponseAsync` from "call the shared runner once" to "build a MAF
agent and stream", and add chunk publishing to `HandleEventAsync`.

## Step-by-step

### 1. Add the MAF package

In [Directory.Packages.props](../../../../Directory.Packages.props), add the Microsoft Agent
Framework core package (pin the latest version — confirm on nuget.org):

```xml
<PackageVersion Include="Microsoft.Agents.AI" Version="<latest-stable>" />
```

Reference it from the plugin
[src/CSweet.Agents.PersonalAssistant/CSweet.Agents.PersonalAssistant.csproj](../../../../src/CSweet.Agents.PersonalAssistant/CSweet.Agents.PersonalAssistant.csproj):

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" />
  <PackageReference Include="Microsoft.Agents.AI" />
</ItemGroup>
```

> `Microsoft.Agents.AI` is the **core** MAF package. It provides `ChatClientAgent`, `AIAgent`,
> `AgentSession`, and all agent runtime primitives. We do NOT need provider-specific packages
> like `Microsoft.Agents.AI.Foundry` because we obtain `IChatClient` from our own
> `ILlmProviderFactory`. The core package is sufficient for wrapping any `IChatClient` as a
> first-class MAF agent.

### 2. Add the streaming chunk event + contract

In [src/CSweet.Agents.PersonalAssistant/PersonalAssistantProfile.cs](../../../../src/CSweet.Agents.PersonalAssistant/PersonalAssistantProfile.cs),
add the new event-type constant next to the existing ones:

```csharp
public const string AssistantResponseChunkEvent = "com.csweet.assistant.response.chunk.v1";
```

In [src/CSweet.Agents.PersonalAssistant/Messages.cs](../../../../src/CSweet.Agents.PersonalAssistant/Messages.cs),
add a record for a streamed delta:

```csharp
public sealed record AssistantResponseChunk(
    string ConversationId,
    int Sequence,       // 0-based order of this chunk within the reply
    string Delta,       // the incremental text produced since the previous chunk
    bool IsFinal);      // true on the terminal chunk (Delta may be empty)
```

> The gateway will use `Sequence`/`IsFinal` to order chunks and to know when to stop and
> persist the final assistant message. Keep chunks small; do not resend the whole text.

### 3. Build and stream with a MAF `ChatClientAgent`

Replace the body of `GenerateResponseAsync` in
[src/CSweet.Agents.PersonalAssistant/PersonalAssistantAgent.cs](../../../../src/CSweet.Agents.PersonalAssistant/PersonalAssistantAgent.cs)
so it constructs a MAF agent from an `IChatClient` and streams. Also change `HandleEventAsync`
to publish a chunk event per delta and a final event at the end.

Add the required usings at the top of the file:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CSweet.Application.Llm; // ILlmProviderFactory
```

Add a helper that streams deltas from the MAF agent. It resolves the `IChatClient` through the
thin seam, wraps it as a `ChatClientAgent`, creates an `AgentSession` for conversation state,
and yields incremental text:

```csharp
private async IAsyncEnumerable<string> StreamAssistantDeltasAsync(
    AssistantCapabilityInput input,
    string capability,
    AgentRuntimeContext runtimeContext,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
{
    using var scope = _scopeFactory.CreateScope();

    // Thin seam: reuse the platform's provider factory to get an IChatClient.
    var providerFactory = scope.ServiceProvider.GetRequiredService<ILlmProviderFactory>();
    var chatClient = await providerFactory.CreateChatClientAsync(input.ProviderProfileId, cancellationToken);

    // Build the MAF agent inside the plugin. The system prompt and behavior are ours.
    AIAgent agent = new ChatClientAgent(
        chatClient,
        instructions: PersonalAssistantProfile.SystemPrompt);

    var prompt = capability switch
    {
        PersonalAssistantProfile.SummarizeActivityCapability =>
            $"Summarize the relevant company activity for the executive.\n\n{input.Prompt}",
        PersonalAssistantProfile.PlanWorkCapability =>
            $"Create a practical work plan. Identify required capabilities, risks, approvals, and next steps.\n\n{input.Prompt}",
        _ => input.Prompt
    };

    // Use AgentSession for conversation state management (official MAF pattern).
    // For single-turn streaming this session is ephemeral.
    // Future: serialize/deserialize sessions for cross-request conversation continuity.
    AgentSession session = await agent.CreateSessionAsync(cancellationToken);

    await foreach (var update in agent.RunStreamingAsync(prompt, session, cancellationToken))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            yield return update.Text;
        }
    }
}
```

> **Official MAF pattern reference:** This follows the documented guidance at
> https://learn.microsoft.com/en-us/agent-framework/agents/?pivots=programming-language-csharp
>
> Key patterns used:
> - `ChatClientAgent(IChatClient, instructions)` — wraps any IChatClient as a first-class agent
> - `CreateSessionAsync()` — creates an AgentSession for conversation state (not GetNewThread)
> - `RunStreamingAsync(prompt, session, cancellationToken)` — streams AgentResponseUpdate objects
> - `update.Text` — extracts incremental text from each update
>
> If your pinned package version has different method signatures, verify against the official
> docs. The core contract is: agent creation via ChatClientAgent, state via AgentSession,
> streaming via RunStreamingAsync.

Now rewrite `HandleEventAsync` to publish chunk events while streaming, then a final event.
Replace the section that currently calls `GenerateResponseAsync` and publishes a single event:

```csharp
var conversationId = incoming.ConversationId;
var builder = new System.Text.StringBuilder();
var sequence = 0;

await foreach (var delta in StreamAssistantDeltasAsync(
    new AssistantCapabilityInput(
        incoming.ProviderProfileId,
        conversationId,
        incoming.Message,
        incoming.Context),
    PersonalAssistantProfile.ConverseCapability,
    context,
    cancellationToken))
{
    builder.Append(delta);

    await PublishChunkAsync(context, message.EventId, new AssistantResponseChunk(
        conversationId,
        sequence++,
        delta,
        IsFinal: false), cancellationToken);
}

// Terminal chunk so the gateway knows the stream is complete.
await PublishChunkAsync(context, message.EventId, new AssistantResponseChunk(
    conversationId, sequence, Delta: string.Empty, IsFinal: true), cancellationToken);

// Keep the existing final "response created" event for anything that consumes the whole reply.
await context.Broker.PublishEventAsync(
    new PublishEvent
    {
        EventType = PersonalAssistantProfile.AssistantResponseCreatedEvent,
        SchemaVersion = "1",
        Subject = $"conversation/{conversationId}",
        ContentType = "application/json",
        Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(
            new AssistantResponseCreated(conversationId, builder.ToString(), ProposedActions: [], DateTimeOffset.UtcNow),
            SerializerOptions))
    },
    message.EventId,
    cancellationToken);
```

Add the small publish helper:

```csharp
private static Task PublishChunkAsync(
    AgentRuntimeContext context,
    string correlationId,
    AssistantResponseChunk chunk,
    CancellationToken cancellationToken)
{
    return context.Broker.PublishEventAsync(
        new PublishEvent
        {
            EventType = PersonalAssistantProfile.AssistantResponseChunkEvent,
            SchemaVersion = "1",
            Subject = $"conversation/{chunk.ConversationId}",
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(chunk, SerializerOptions))
        },
        correlationId,
        cancellationToken);
}
```

> Keep `ExecuteCapabilityAsync` (the non-streaming capability path) working — it can continue
> to return the assembled text. You can refactor it to reuse `StreamAssistantDeltasAsync` and
> concatenate, or leave its existing single-shot behavior. Chat uses the event path, not the
> capability path.

### 4. Let the plugin publish the chunk event (manifest + broker policy)

A plugin can only publish events it is granted. Add the new event in **both** places.

Manifest
[src/CSweet.Agents.PersonalAssistant/csweet-agent.json](../../../../src/CSweet.Agents.PersonalAssistant/csweet-agent.json)
— add to `requestedPublications`:

```json
"requestedPublications": [
  "com.csweet.assistant.response.created.v1",
  "com.csweet.assistant.response.chunk.v1",
  "com.csweet.assistant.progress.updated.v1",
  "com.csweet.action.proposed.v1"
]
```

Broker policy
[src/CSweet.AgentHost/appsettings.json](../../../../src/CSweet.AgentHost/appsettings.json)
— add to the personal assistant's `Publications` list:

```json
"Publications": [
  "com.csweet.assistant.response.created.v1",
  "com.csweet.assistant.response.chunk.v1",
  "com.csweet.assistant.progress.updated.v1",
  "com.csweet.action.proposed.v1"
]
```

> Remember the grant is the **intersection** of manifest and config. If you add the event to
> only one, the plugin will silently not be allowed to publish chunks.

## Testing

In `tests/CSweet.UnitTests` (there is already a `PersonalAssistantAgentTests.cs` using a fake
chat client / provider):

- Feed a fake `IChatClient` that yields several `ChatResponseUpdate`s and assert the agent
  publishes: N `assistant.response.chunk.v1` events with increasing `Sequence`, then a final
  chunk with `IsFinal == true`, then one `assistant.response.created.v1` whose text equals the
  concatenation of all deltas.
- Assert every published chunk uses subject `conversation/{conversationId}`.
- Assert a malformed `UserMessageReceived` (empty message / empty provider id) publishes
  nothing (existing guard).

> Use the existing fake provider used by the current tests so you do not call a real model.

## Acceptance criteria

- [ ] `Microsoft.Agents.AI` core package is referenced centrally and by the plugin only.
- [ ] The assistant produces its reply via a MAF `ChatClientAgent` + `AgentSession` + `RunStreamingAsync`.
- [ ] Session management uses `CreateSessionAsync()` (official MAF pattern), not `GetNewThread()`.
- [ ] No MAF orchestration was added to `CSweet.AI` / `IAgentRunner`.
- [ ] Streaming produces ordered `assistant.response.chunk.v1` events ending with `IsFinal`.
- [ ] A final `assistant.response.created.v1` event still carries the full text.
- [ ] Manifest and broker policy both grant the new chunk publication.
- [ ] Unit tests cover the streaming sequence and the malformed-input guard.

## Definition of done

Running the agent (via Aspire) and publishing a test `user.message.received` event results in
a stream of chunk events on the broker followed by a final response event — verifiable from
the agent logs and unit tests. No browser or API changes are required to complete this phase.
