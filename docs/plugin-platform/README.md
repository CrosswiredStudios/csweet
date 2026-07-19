# C-Sweet Plugin Platform v1

This is the normative contract for installing and operating C-Sweet plugins. The core is the authority for package identity, grants, tenant identity, secrets, events, storage, and external communication. A plugin is untrusted workload code.

## Package contract

Every package has exactly one `csweet-plugin.json` at its root. Version 1 is a clean break: `csweet-agent.json` is rejected. `manifestVersion` is `1.0`, and `kind` is `agent` or `service`.

The manifest declares typed provided and required capabilities, published and subscribed events, configuration fields, credential bindings, web rules, runtime requirements, and UI contributions. A declaration is a request, not authority. At install, an administrator acknowledges the complete requested grant set. Runtime calls are authorized against the immutable installed manifest and that revision's persisted grants.

Sources enter one content-addressed pipeline:

- a GitHub repository URL;
- a source ZIP uploaded to the API;
- a signed `.csplugin` bundle once a trusted signing policy is configured.

Archives are rejected for traversal, absolute paths, symlinks, duplicate or case-colliding names, excessive file count, excessive expanded size, suspicious compression ratios, missing root manifest, or oversized manifest. Source is built in a disposable builder and runtime output is mounted read-only.

## Grants and broker rules

Plugins have no direct route to the internet, LAN, metadata services, databases, queues, Docker, or other plugins. Each runtime receives a unique internal Docker network. Only the broker gateway is attached, under the broker hostname. The container runs non-root with a read-only root filesystem, all Linux capabilities dropped, `no-new-privileges`, bounded memory/CPU/PIDs/runtime, and a small `noexec` temporary filesystem.

Broker capabilities currently include:

| Capability | Purpose |
| --- | --- |
| `web.fetch.v1` | Bounded `GET` and `HEAD` requests |
| `web.request.v1` | Separately granted mutating HTTP methods |
| `web.render.v1` | Platform-managed browser rendering contract |
| `web.socket.v1` | Broker-managed WSS connections with bounded frames and revocation |

HTTP rules match protocol, scheme, exact host, optional port, path prefix, and method. `all-public` is a separate high-risk grant and requires an explicit acknowledgement. It never permits loopback, private, link-local, carrier-grade NAT, multicast, reserved, metadata, or local-service destinations.

The HTTP broker resolves and validates DNS before connecting, pins the connection to an approved resolved address, reauthorizes every redirect, rejects unauthorized protocol changes, bounds request and response sizes, and times out calls. Audit records contain the redacted destination, method, matched grant, result, status, and denial reason. Query strings, authorization values, and secret values are not logged.

Credential bindings name a core-owned secret and constrain its allowed origins. The proxy injects the credential only after the destination and grant match. There is no broker operation that returns secret material to a plugin.

## Immutable revision lifecycle

An installation version is an immutable revision with a stable installation key and increasing revision number.

1. Selecting an update creates a disabled `Staged` revision.
2. Its capability, event, credential, HTTP, and WebSocket grants are empty. Nothing is copied, intersected, suggested, or preselected from the active revision.
3. The old revision remains active while the package builds and awaits review.
4. An administrator acknowledges the staged revision's complete grant set, including unchanged grants and the distinct `all-public` warning when applicable.
5. After package verification, the core retires the old revision and activates the staged revision as one database transition.
6. Historical revisions retain their own historical grants and audit/run records for rollback and investigation.

A staged or retired revision cannot register, invoke the broker, receive credentials, receive events, or be enabled. Runtime tokens are short-lived and revision-bound.

## Agent employee onboarding lifecycle

When an agent installation is assigned to a new organization employee, the core atomically creates that employee's protected human-agent conversation and a durable `com.csweet.agent.onboarded.v1` event. The event payload contains the organization, agent employee, hiring employee, protected conversation, and occurrence timestamp. The core does not create an introductory message or prescribe what the agent must do.

The AgentHost offers the event only to the matching installation after it connects. This trusted targeted lifecycle event does not depend on an optional event-subscription grant. Its stable event ID is retried until the agent invokes `agent.onboarding.complete.v1`; agents must therefore handle it idempotently and acknowledge only after their chosen onboarding behavior succeeds. Agent-authored chat messages should use `communication.message.send.v1` with an idempotency key derived from the lifecycle event ID.

Delivery is bounded by `CSweet:AgentOnboardingDelivery:MaximumAttempts` (default `12`). Exhaustion marks the lifecycle event failed and creates an important, real-time notification for the hiring user with the agent employee, installation, event ID, and last delivery or acknowledgement problem.

## Plugin author checklist

- Use the C-Sweet Plugin SDK and load the canonical manifest at startup.
- Request the narrowest typed capabilities and exact event types.
- Use allowlisted origins and path prefixes; do not request `all-public` unless the plugin's purpose genuinely requires arbitrary public sites.
- Store credentials only through declared core bindings; never add a token configuration field.
- Store durable state through a declared broker capability rather than an unrestricted volume.
- Treat broker denials and grant revocation as normal lifecycle events.
- Stamp no tenant identity yourself. Trust only the organization and installation identity supplied by the broker.

## Administrator runbook

Before approval, verify publisher identity, package digest, provenance, manifest diff, every capability/event, credential origin, HTTP/WebSocket rule, resource limit, and the stated purpose. Test a staged update in a non-production organization. If behavior is suspicious, disable the revision, revoke its organization assignments and secrets, preserve package/run/audit evidence, rotate bound credentials, and investigate broker denial and destination-volume records.

Plugin-management APIs require the `SystemAdministrator` role. Installation is never implied by source preview or build completion.

## Enterprise readiness status

Implemented and release-tested in the current codebase:

- canonical v1 manifest and clean legacy rejection;
- GitHub and hardened source-ZIP imports;
- immutable staged revisions with empty update grants;
- brokered bounded HTTP with SSRF and DNS-rebinding defenses;
- brokered WSS with pinned DNS resolution, bounded frames, connection ownership, revocation, and destination-bound credential substitution;
- destination-bound opaque credentials;
- private broker-only runtime networks and container hardening;
- administrator-only management APIs;
- migrated ChiefOfStaff agent and Discord service manifests/SDK usage.

Required before claiming full enterprise production readiness:

- trusted `.csplugin` signatures, SBOM/provenance verification, malware/vulnerability policy, and controlled build dependency proxy;
- production `web.render.v1` implementation and adversarial WebSocket proxy/gateway test coverage;
- startup health-gated atomic update cutover with automatic runtime rollback;
- core-owned durable Discord resume/idempotency/provisioning state, complete workspace reconciliation, and deterministic fake-gateway round-trip gates;
- adversarial multi-tenant, forged-token, event-spoofing, replay, resource-exhaustion, and proxy-bypass suites;
- multi-repository CI that treats ChiefOfStaff chat and Discord message round trips as release gates;
- signed release key rotation, incident-response exercises, and platform-specific seccomp/AppArmor/SELinux enforcement validation.

Until those remaining gates are complete, the platform is a hardened enterprise-oriented foundation, not a finished enterprise-certified plugin system.
