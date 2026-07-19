# Native communications hub

`/organizations/{organizationId}/communications` is C-Sweet's authoritative communication workspace. External providers such as Discord are optional mirrors configured beneath `/communications/providers/{provider}`.

The canonical deep link for a conversation is `/organizations/{organizationId}/communications/{chatId}`. Employee and briefing entry points resolve the appropriate conversation and navigate to that route; there is no separate employee-chat page.

## Agent direct messages

- A human-agent relationship has one private, deletion-protected direct conversation.
- A human message in that direct conversation creates a durable chat turn. The turn worker starts the runtime, recalls memory, publishes execution trace events, persists the response, and captures memory.
- The Communications UI streams trace/output events and supports cancellation, retry, reconnect, and page-reload recovery.
- Human direct messages and group-channel messages are persisted normally. Merely adding an agent to an in-app channel does not invoke it.
- Turn list, trace, stream, retry, and cancel operations are scoped beneath the Communications chat API and require active membership in that conversation.

## Platform capabilities

Communication access is deny-by-default for agents. A plugin must declare a capability in its manifest and an administrator must grant it to the installed revision.

| Capability | Authority |
| --- | --- |
| `communication.chat.read.v1` | List chats visible to the agent employee, or list one chat's messages. |
| `communication.chat.create.v1` | Create direct or group chats. |
| `communication.chat.modify.v1` | Rename a group, change its description/privacy, and reconcile membership. |
| `communication.chat.delete.v1` | Archive a group while preserving its history. |
| `communication.message.send.v1` | Send a message to a chat in which the agent is an active member. |

Create and modify payloads accept explicit `participantOrganizationUserIds` plus `audienceRoleIds` and `audienceWorkstreamIds`. Audience IDs expand to active organization employees. Workstreams include active responsibility holders and the accountable manager.

Modify, archive, and send payloads include `chatId`. Send also includes `content`. Read accepts an empty payload to list the hub or `{ "chatId": "..." }` to list messages.

## Human authority

- Active organization members can create direct messages and participate in chats to which they belong.
- Managers and owners can create and maintain group chats.
- The creator is a coordinator and can maintain that group later.
- Archive is used instead of hard deletion so communication history and auditability are retained.
- Hiring an agent employee creates one private, deletion-protected direct conversation between that logical agent instance and the hiring human. Separate installations of the same agent therefore keep separate histories and memory contexts.
- Agent onboarding creates the protected conversation and a durable targeted lifecycle event; the agent decides whether to send an introduction through the communication capability.

## Unread state and live controls

Messages have monotonic sequences and each participant stores the last sequence they read. Self-authored messages never contribute to unread totals. The hub exposes `GET /hub/unread-summary` and `POST /hub/chats/{chatId}/read`; the read request includes the last sequence actually displayed so a racing message remains unread.

Authenticated browser sessions connect to `/hubs/app-events`. Communication changes and persisted `UserNotification` changes enter the transactional `ApplicationRealtimeOutbox`, then publish through a versioned `AppRealtimeEventEnvelope` only to server-resolved organization-user groups. The WebAssembly client deduplicates stable event IDs, reconnects automatically, and reconciles authoritative state through REST after reconnecting. This event store drives the navigation badge, conversation indicators, message refresh, and global notification toasts.

## Conversation events

Every replication-relevant mutation to a conversation, participant, or message is written to the transactional `CommunicationEventOutbox` in the same database save. The API dispatcher publishes events in global `sequence` order and retries transport failures. Each JSON payload uses a `CommunicationEventEnvelope` containing a stable `eventId` for idempotency, the organization, sequence, event type, canonical resource subject, occurrence time, and typed event data.

Plugins can declare and receive these subscriptions:

- `com.csweet.communication.chat.created.v1`
- `com.csweet.communication.chat.updated.v1`
- `com.csweet.communication.chat.archived.v1`
- `com.csweet.communication.chat.deleted.v1`
- `com.csweet.communication.participant.added.v1`
- `com.csweet.communication.participant.updated.v1`
- `com.csweet.communication.participant.removed.v1`
- `com.csweet.communication.message.created.v1`
- `com.csweet.communication.message.updated.v1`
- `com.csweet.communication.message.deleted.v1`
- `com.csweet.communication.read.updated.v1`

Dispatch is installation-targeted rather than broadcast. An organization-scoped plugin must belong to the event's organization. A system plugin must have a server-owned `PluginOrganizationGrant` for that organization. In both cases, its active installation grant must contain the exact subscription. This prevents one Discord installation from observing another organization's conversation data.
