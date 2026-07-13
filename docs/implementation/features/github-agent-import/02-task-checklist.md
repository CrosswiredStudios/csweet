# Task Checklist - GitHub Agent Import

Copy these into GitHub issues. Keep the order because the security model depends on the earlier
broker and sandbox work.

## Phase 0 - Compose parity

- [ ] Add `CSweet.AgentHost` to Docker Compose as `csweet-agenthost`.
- [ ] Add the official Personal Assistant to Docker Compose as a separate service.
- [ ] Add Dockerfiles or publish profiles for `CSweet.AgentHost` and `CSweet.Agents.PersonalAssistant`.
- [ ] Configure the API and Personal Assistant to resolve the broker in Compose.
- [ ] Add health checks for the broker and Personal Assistant.
- [ ] Update deployment docs to show the brokered agent runtime in Docker.

## Phase 1 - Import candidate preview

- [ ] Add `AgentPackageSource` and `AgentPackageVersion` entities.
- [ ] Add DTOs for submitting a GitHub repo URL and previewing an import candidate.
- [ ] Implement URL normalization and public GitHub repository validation.
- [ ] Resolve branch/tag/default ref to a commit SHA.
- [ ] Fetch only root `csweet-agent.json` for preview.
- [ ] Validate manifest ID, version, publisher, protocol range, runtime type, capabilities,
  publications, subscriptions, permissions, and network requests.
- [ ] Compute and persist manifest digest.
- [ ] Add audit events for import preview success/failure.
- [ ] Add unit tests for URL parsing, manifest validation, and digest stability.

## Phase 2 - Admin grant review

- [ ] Add `AgentInstallation` and `AgentGrant` entities.
- [ ] Add an admin UI review page for manifest requests and warnings.
- [ ] Persist approved grants as a narrowed intersection of manifest request, admin choice, and
  platform policy.
- [ ] Reject installs that request unsupported runtime types or forbidden permissions.
- [ ] Add broker policy loading from persisted install grants.
- [ ] Add audit events for approval, rejection, grant changes, enable, disable, and revocation.
- [ ] Add tests that a grant can never exceed the imported manifest request.

## Phase 3 - OCI sandbox runtime

- [ ] Extend `AgentManifest` to support OCI image digest runtime metadata.
- [ ] Require image digest pinning for community agents.
- [ ] Implement `IAgentSandboxRunner` for starting/stopping imported agent containers.
- [ ] Run containers as non-root with read-only root filesystem and a disposable workspace.
- [ ] Apply CPU, memory, process, runtime, and log limits.
- [ ] Deny Docker socket, host networking, privileged mode, and host filesystem mounts.
- [ ] Pass only broker endpoint, installation ID, business ID, and short-lived workload token.
- [ ] Record `AgentRuntimeInstance` status and exit details.
- [ ] Add integration tests or manual QA for start, stop, crash, timeout, and revocation.

## Phase 4 - Network and secret brokering

- [ ] Add per-install network allowlist policy.
- [ ] Route runtime egress through a controllable proxy or equivalent deny-by-default mechanism.
- [ ] Add workload token validation at the broker.
- [ ] Implement opaque connection IDs for external APIs and MCP servers.
- [ ] Ensure platform adapters, not agents, retrieve raw secrets.
- [ ] Add rate limits by installation, business, event type, and capability.
- [ ] Add audit events for tool calls, MCP calls, API calls, approvals, and policy denials.

## Phase 5 - MCP/API capability adapters

- [ ] Define capability naming conventions for MCP and external API actions.
- [ ] Add a platform MCP adapter that executes approved MCP calls on behalf of agents.
- [ ] Add request/response schema validation for brokered capabilities.
- [ ] Add approval gates for side-effecting capabilities.
- [ ] Add redaction/classification checks before returning tool results to agents.
- [ ] Add tests for denied side effects, missing approval, and allowed read-only calls.

## Phase 6 - Supply chain hardening

- [ ] Store package digest and image digest on every imported version.
- [ ] Add signature verification support for OCI images or `.csagent` packages.
- [ ] Add trust badges for official, reviewed, signed, community, modified, and revoked packages.
- [ ] Add update detection that creates a new import candidate instead of mutating an installed
  version.
- [ ] Add kill-switch/revocation support for compromised package versions.

## Definition of done

- [ ] Imported repository code never executes during preview.
- [ ] Community agents run only in the sandbox runtime.
- [ ] Agents communicate only through the broker.
- [ ] Grants are deny-by-default and no broader than manifest requests.
- [ ] Agents cannot access raw secrets, host files, Docker socket, database, or RabbitMQ.
- [ ] MCP and API calls are mediated by platform adapters.
- [ ] Network egress is denied by default and allowlisted per install.
- [ ] Import, approval, execution, tool use, denial, and revocation are audited.
