# Feature: Personal Assistant Chat

## What we are building

Today the **Employees** page shows a reporting graph and a directory of employees
(both human teammates and agent employees), but the cards do nothing when clicked.

This feature lets a user **click any agent employee (except "Self")** and open a
**chat page** where they can have a live, streaming conversation with that agent. The
first (and currently only) agent wired end-to-end is the **Personal Assistant**.

A user types a message, and the assistant's reply **streams back token-by-token**. The
conversation is **persisted** in the database so it survives a page refresh.

> This is the first end-user-facing agent interaction in C-Sweet. Treat it as the
> reference implementation for how the product talks to agents safely.

## Who this is for

These documents are written for a **junior developer**. Each phase gives you context
(the "why"), then concrete step-by-step instructions (the "how") with real file paths and
code you can adapt. You do **not** need to have designed any of this yourself — follow the
phases in order and you will end with a working feature.

Before you start, skim these existing docs so the vocabulary makes sense:

- [docs/implementation/phases/09a-agent-broker-and-personal-assistant.md](../../phases/09a-agent-broker-and-personal-assistant.md) — how the agent broker and the Personal Assistant work today.
- [docs/implementation/00-architecture-baseline.md](../../00-architecture-baseline.md) — the project layout and layering rules.
- [docs/implementation/12-junior-developer-task-checklist.md](../../12-junior-developer-task-checklist.md) — how we break work into small PRs.

## The four big decisions (already made for you)

You do not need to re-litigate these. They are the foundation of every phase below.

1. **The agent is a self-contained plugin.** All Microsoft Agent Framework (MAF) code —
   the `ChatClientAgent`, the streaming loop, the system prompt — lives **inside the
   `CSweet.Agents.PersonalAssistant` project**. The plugin talks to the platform only
   through the broker gRPC contract and its manifest. It must **not** push agent
   orchestration into shared libraries.
2. **The API is the governed gateway.** The browser never talks to an agent directly. The
   `CSweet.Api` app connects to the broker as a registered principal
   (`com.csweet.api-gateway`) and mediates every message. This is where we will later
   enforce "who is allowed to talk to which agent and use which capabilities." Building it
   now, even for one agent, sets up that governance boundary.
3. **Streaming uses the broker's existing event pub/sub.** We do **not** change the gRPC
   proto. The agent publishes small "chunk" events as it generates text; the gateway
   relays them. This reuses infrastructure that already exists.
4. **The browser receives the stream over Server-Sent Events (SSE).** Plain HTTP, no new
   packages, no SignalR. The Blazor page reads the response stream line-by-line.

## End-to-end architecture

```
 Browser (Blazor WASM)                CSweet.Api  (governed gateway)         AgentHost (broker)        CSweet.Agents.PersonalAssistant (MAF plugin)
 ---------------------                -----------------------------         ------------------        --------------------------------------------
 Chat.razor                                                                                              PersonalAssistantAgent
   |  1. POST message (HTTP)                                                                                     |
   | ----------------------------->  ChatEndpoints                                                               |
   |                                    |  2. governance checks                                                  |
   |                                    |  3. persist user message (DB)                                          |
   |                                    |  4. publish "user.message.received"                                    |
   |                                    | -----------------------------------> AgentBroker --- deliver event --> HandleEventAsync
   |                                    |                                          ^                             |  5. MAF ChatClientAgent
   |  6. SSE stream (text/event-stream) |                                          |                             |     .RunStreamingAsync(...)
   | <===============================   |  <----- "assistant.response.chunk" -----|<--- publish chunk events ---|  (one event per delta)
   |     tokens appended live           |  <----- "assistant.response.created" ---|<--- publish final event ----|
   |                                    |  7. persist assistant message (DB)                                     |
```

The important idea: **the browser only ever speaks HTTP to our own API.** Everything on the
agent side of the API is governed by the broker.

## Phases (do them in this order)

Each phase is an independently reviewable PR (or a small group of PRs). Each ends with a
"Definition of done" you can verify before moving on.

| # | Phase | What you build | Depends on |
|---|-------|----------------|------------|
| 1 | [01-conversation-persistence.md](01-conversation-persistence.md) | `Conversation` + `ConversationMessage` entities, EF config, migration, service, DTOs, CRUD endpoints | — |
| 2 | [02-maf-agent-runtime-and-streaming.md](02-maf-agent-runtime-and-streaming.md) | Personal Assistant uses MAF `ChatClientAgent` and streams chunk events (plugin-only) | — |
| 3 | [03-api-gateway-and-broker-governance.md](03-api-gateway-and-broker-governance.md) | API connects to broker as `com.csweet.api-gateway`, governance config, AppHost wiring | 2 |
| 4 | [04-chat-api-and-sse-streaming.md](04-chat-api-and-sse-streaming.md) | The streamed chat endpoint (SSE) that ties persistence + gateway together | 1, 3 |
| 5 | [05-employees-entrypoint-and-chat-ui.md](05-employees-entrypoint-and-chat-ui.md) | Clickable agent cards + the `Chat.razor` page + API client | 4 |
| 6 | [06-testing-and-acceptance.md](06-testing-and-acceptance.md) | Unit/integration/manual tests and the final acceptance checklist | 1–5 |

There is also a flat task checklist you can copy into GitHub issues:
[07-task-checklist.md](07-task-checklist.md).

## Scope

**In scope**

- Click an agent employee (not "Self") → open a chat page for that agent.
- Send a message and receive a streamed reply from the Personal Assistant.
- Persist conversations and messages; reload them on revisit.
- Enforce the governance boundary (API-as-gateway) for one agent.

**Out of scope (deliberately, for now)**

- Multiple concurrently addressable agents (only the Personal Assistant is wired; the UI is
  written generically so more agents can be added later).
- Editing, deleting, or searching conversation history.
- Approvals / proposed actions surfaced in the UI (the agent may still propose them in text).
- Authentication and multi-tenant identity (we use the org's "Self" user as the initiator).

## Glossary

- **Agent employee** — an `OrganizationUser` row with `EmployeeType == Agent` (`1`).
- **Self** — the human owner row, `DisplayName == "Self"` and `EmployeeType == Human` (`0`).
- **Broker** — the gRPC service in `CSweet.AgentHost` that routes events/capabilities between
  registered principals and enforces the authorization policy.
- **Principal / grant** — anything registered with the broker (an agent, or our API gateway).
  A grant is the intersection of what a principal *requests* and what config *allows*.
- **Capability** — a named skill an agent provides, e.g. `assistant.converse.v1`.
- **Event** — a pub/sub message with an `event_type` and a `subject` (routing key), e.g.
  `com.csweet.user.message.received.v1` on subject `conversation/{id}`.
- **Chunk event** — a new event type we add, `com.csweet.assistant.response.chunk.v1`, carrying
  one streamed delta of the assistant's reply.
- **SSE** — Server-Sent Events; an HTTP response with `Content-Type: text/event-stream` that
  pushes `data:` lines to the browser as they are produced.

## Conventions to respect (from the existing codebase)

- **Domain entities** are plain sealed classes with a `Guid Id` and `DateTimeOffset` timestamps.
  No EF or infrastructure references in `CSweet.Domain`.
- **Persistence**: services inject `CSweetDbContext` directly and query with LINQ. There is
  **no repository pattern** — do not introduce one.
- **Mapping** entity → DTO uses `ToResponse()` extension methods in `CSweet.Infrastructure`.
- **DTOs** are `record` types in `CSweet.Contracts`.
- **API endpoints** are minimal APIs grouped with `MapGroup(...)` and registered in
  [src/CSweet.Api/Program.cs](../../../../src/CSweet.Api/Program.cs) via a `MapXxxEndpoints()`
  extension method.
- **Every mutation writes an audit event** via `IAuditEventWriter`.
- Package versions are centralized in
  [Directory.Packages.props](../../../../Directory.Packages.props). Add new packages there
  (no inline versions).
```
