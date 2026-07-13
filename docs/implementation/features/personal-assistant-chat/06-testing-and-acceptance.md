# Phase 6 — Testing and acceptance

## Goal

Bring the whole feature together with a consistent test story and a single acceptance checklist
you can run before calling the feature done. Most tests belong to their own phase; this
document collects the cross-cutting ones and the end-to-end verification.

## Test pyramid for this feature

```
        manual E2E (few)        Employees -> click agent -> stream a real reply
      integration (some)        API stream endpoint + persistence + fake agent
     unit tests (many)          service, router, agent streaming, mappers
```

## Unit tests (per phase, summarized)

- **Phase 1 — persistence** (`tests/CSweet.UnitTests`)
  - `ConversationService.StartAsync`: success for agent target; `not_an_agent` for "Self";
    `agent_not_found` for unknown id; `no_owner` when the org has no human.
  - `AppendMessageAsync`: persists, orders by `CreatedAt`, sets `Title` from first user message.
  - `ListMessagesAsync`: chronological order.
- **Phase 2 — agent streaming** (`tests/CSweet.UnitTests/PersonalAssistantAgentTests.cs`)
  - Fake `IChatClient` yields multiple updates → agent publishes ordered
    `assistant.response.chunk.v1` events, a final `IsFinal` chunk, then one
    `assistant.response.created.v1` with the concatenated text.
  - Malformed `UserMessageReceived` publishes nothing.
- **Phase 3 — gateway router**
  - `ChatStreamRouter`: subscribe returns a reader; publish delivers in order; final chunk
    completes the reader; publish to an unknown conversation is a no-op.

## Integration tests (`tests/CSweet.IntegrationTests`)

Use the existing `WebApplicationFactory`-based setup with a **fake agent/provider** so no real
model is called.

- **Happy path stream:** create a conversation (Phase 1 endpoint), POST to the stream endpoint,
  assert:
  - response `Content-Type` is `text/event-stream`;
  - at least one `data:` chunk arrives and the last has `isFinal:true`;
  - after completion, `GET .../messages` returns the user message followed by the assistant
    message.
- **Validation:** empty message → 400; unknown/foreign-org conversation → 404; no enabled LLM
  provider → 409 with a helpful error body.
- **Governance boundary:** assert the API never exposes a broker/gRPC endpoint to the browser —
  the only chat surface is the HTTP endpoints under `/api/core/organizations/.../conversations`.

> For the fake agent in integration tests, the simplest approach is to register a test double
> that plays the agent's role: on receiving `user.message.received`, publish a couple of
> `assistant.response.chunk.v1` events and a final event. Alternatively, run the real plugin
> logic against a fake `IChatClient`. Choose whichever your existing test harness supports and
> note it in the PR.

## Manual end-to-end QA (with a real provider, e.g. LM Studio)

1. Complete first-run setup so at least one **enabled** LLM provider profile exists.
2. Onboard a business so an org exists with a "Self" human and the Personal Assistant agent
   employee.
3. Run the full stack via Aspire (AppHost). Confirm in logs:
   - the Personal Assistant registered with the broker;
   - the API gateway registered as `com.csweet.api-gateway`.
4. Open the app → Organizations → open the org → **Employees**.
5. Verify the "Self" card is **not** clickable; the Personal Assistant card **is**.
6. Click the Personal Assistant → land on the chat page.
7. Send "Say hello in five words." → watch tokens stream into the assistant bubble.
8. Send a follow-up → confirm a second exchange works.
9. Refresh the page mid-stream → the app recovers without a crash.
10. Inspect the database: `Conversations` has a row, `ConversationMessages` has both the user
    and assistant turns with correct `Role`.

## Common failure modes and where to look

| Symptom | Likely cause | Where to check |
|---------|--------------|----------------|
| API logs "registration rejected" | gateway not in broker config, or business id mismatch | [src/CSweet.AgentHost/appsettings.json](../../../../src/CSweet.AgentHost/appsettings.json) step 2 of Phase 3 |
| No chunks ever arrive | agent can't publish chunk event (grant missing) | manifest + broker `Publications` in Phase 2 step 4 |
| Chunks arrive but UI shows nothing | SSE buffered by a proxy | `X-Accel-Buffering: no`, nginx config (Phase 4) |
| Endpoint 409 "no provider" | no enabled `LlmProviderProfile` | finish setup / provider profiles |
| API can't reach broker | missing AppHost reference | `.WithReference(agentHost)` on api (Phase 3 step 7) |
| Reply not persisted | stream broke before `isFinal` | timeout/error handling in Phase 4 step 4 |

## Final acceptance checklist (the whole feature)

Functional
- [ ] Agent employees (not "Self") are clickable on the Employees page.
- [ ] Clicking opens a chat page bound to that agent employee.
- [ ] Sending a message streams the assistant's reply token-by-token.
- [ ] The conversation and both message turns are persisted and survive a refresh of the app
      (a new conversation may be started per visit, but prior data remains in the DB).

Architecture / governance
- [ ] The agent's reply is generated by a MAF `ChatClientAgent` inside the
      `CSweet.Agents.PersonalAssistant` plugin (no MAF orchestration leaked into shared libs).
- [ ] The browser never talks to the broker; all agent traffic goes through the API gateway.
- [ ] The gateway is a broker principal (`com.csweet.api-gateway`) whose grants are defined by
      config (deny-by-default).
- [ ] Streaming uses broker events (no gRPC proto changes) and SSE (no new transport packages).

Quality
- [ ] Unit tests (persistence, router, agent streaming) pass.
- [ ] Integration tests (stream happy path + validation) pass.
- [ ] Manual E2E with a real provider works end-to-end.
- [ ] `dotnet build` and the existing test suite are green.

## Definition of done

Every box above is checked, the feature works against a real model, and the code respects the
four foundational decisions in the [README](README.md): MAF-in-plugin, API-as-gateway,
event-based streaming, and SSE to the browser.
