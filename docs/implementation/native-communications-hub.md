# Native communications hub

`/organizations/{organizationId}/communications` is C-Sweet's authoritative communication workspace. External providers such as Discord are optional mirrors configured beneath `/communications/providers/{provider}`.

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

Dispatch is installation-targeted rather than broadcast. An organization-scoped plugin must belong to the event's organization. A system plugin must have a server-owned `PluginOrganizationGrant` for that organization. In both cases, its active installation grant must contain the exact subscription. This prevents one Discord installation from observing another organization's conversation data.
