# Task checklist — Personal Assistant Chat

Copy these into GitHub issues. Each top-level checkbox is roughly one focused change; group
1–3 related boxes per pull request. Keep the order — later tasks depend on earlier ones.

Follow the same rules as
[docs/implementation/12-junior-developer-task-checklist.md](../../12-junior-developer-task-checklist.md):
read the referenced phase doc first, keep infra out of `CSweet.Domain`, add a migration when an
entity changes, and include tests + a short manual-verification note in every PR.

## Phase 1 — Conversation persistence
- [ ] Add `ConversationRole` enum and `Conversation` + `ConversationMessage` entities in `CSweet.Domain/Core`.
- [ ] Add `DbSet`s to `CSweetDbContext`.
- [ ] Add EF configuration methods in `CoreConfigurations` (enum-as-string, FKs, indexes).
- [ ] Add DTOs: `ConversationResponse`, `ConversationMessageResponse`, `StartConversationRequest`, `ConversationActionResponse`.
- [ ] Add `ToResponse()` mappers in `CoreMappers`.
- [ ] Add `IConversationService` (Application) and `ConversationService` (Infrastructure).
- [ ] Register `IConversationService` in `DependencyInjection`.
- [ ] Add `ConversationEndpoints` (POST create, GET conversation, GET messages) and map them in `Program.cs`.
- [ ] Create EF migration `ConversationPersistence`; verify it applies to a fresh DB.
- [ ] Unit tests for `ConversationService` (start/append/list, agent vs. Self guard).

## Phase 2 — MAF agent runtime and streaming (plugin)
- [ ] Add `Microsoft.Agents.AI` to `Directory.Packages.props`; reference it from the plugin csproj.
- [ ] Add `AssistantResponseChunkEvent` constant and `AssistantResponseChunk` record.
- [ ] Implement `StreamAssistantDeltasAsync` using `ChatClientAgent` + `RunStreamingAsync` (via `ILlmProviderFactory` seam).
- [ ] Rewrite `HandleEventAsync` to publish ordered chunk events + a terminal `IsFinal` chunk + the final response event.
- [ ] Add the chunk event to the manifest `requestedPublications` and broker `Publications`.
- [ ] Unit tests: streaming sequence, concatenation equals final text, malformed-input guard.

## Phase 3 — API gateway and broker governance
- [ ] Reference `CSweet.Agent.SDK` and `CSweet.Agent.Contracts` (and the plugin/shared contracts for `AssistantResponseChunk`) from `CSweet.Api`.
- [ ] Add the `com.csweet.api-gateway` entry to broker `appsettings.json` (publish user message; subscribe chunk + final).
- [ ] Add `ApiGatewayOptions` and bind `CSweet:ApiGateway`; add gateway config to API `appsettings.json`.
- [ ] Add `IChatStreamRouter` + `ChatStreamRouter` (bounded channels keyed by conversation id).
- [ ] Add `ApiGatewayBrokerWorker` hosted service (register, read loop, route chunk events); add connect/retry resilience.
- [ ] Add `AddChatGateway(...)` DI extension; call it in `Program.cs`.
- [ ] Add `.WithReference(agentHost)` to the API in AppHost.
- [ ] Unit tests for `ChatStreamRouter`; manual check that the gateway registers in logs.

## Phase 4 — Chat API and SSE streaming
- [ ] Add `SendChatMessageRequest` DTO and `GetDefaultProviderProfileIdAsync` on the conversation service.
- [ ] Add `ChatMessageEndpoints` with the SSE stream endpoint; map it in `Program.cs`.
- [ ] Subscribe to the router before publishing; persist user message before publish; persist assembled assistant message after.
- [ ] Add the stall timeout + friendly error chunk.
- [ ] Ensure proxy buffering is disabled for the stream route (`X-Accel-Buffering`, nginx).
- [ ] Integration tests: happy-path stream, 400/404/409 validation, messages persisted.

## Phase 5 — Employees entrypoint and chat UI
- [ ] Add `IsChattableAgent` + `OpenChat` and make agent (non-Self) cards clickable in `Employees.razor`.
- [ ] Add clickable-card styling.
- [ ] Add `IChatApiClient` + `ChatApiClient` (start conversation, get messages, SSE read); register in DI.
- [ ] Add `Chat.razor` page with transcript, streaming bubble, and input (Enter to send, disabled while streaming).
- [ ] Add chat/transcript styling.
- [ ] Manual UI verification (Self not clickable, streamed reply, refresh recovery).

## Phase 6 — Testing and acceptance
- [ ] Ensure per-phase unit tests are present and green.
- [ ] Add/confirm integration tests for the stream endpoint.
- [ ] Run the manual end-to-end QA script with a real provider.
- [ ] Walk the final acceptance checklist in [06-testing-and-acceptance.md](06-testing-and-acceptance.md).

## Suggested PR grouping

- **PR 1:** Phase 1 (all).
- **PR 2:** Phase 2 (all).
- **PR 3:** Phase 3 (all).
- **PR 4:** Phase 4 (all).
- **PR 5:** Phase 5 (all).
- **PR 6:** Phase 6 test hardening + acceptance.

Each PR should build, pass tests, and include a one-paragraph manual-verification note (plus a
screenshot for PR 5).
