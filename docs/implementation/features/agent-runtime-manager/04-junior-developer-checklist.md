# Agent Runtime Manager - Junior Developer Checklist

Use this as the issue checklist. Keep PRs small. A good PR usually completes one phase or a
coherent subset of one phase.

## Phase 0 - Compose baseline

- [ ] Add broker service to Docker Compose.
- [ ] Add Personal Assistant service to Docker Compose.
- [ ] Add required Dockerfiles or publish profiles.
- [ ] Configure API broker endpoint for Compose.
- [ ] Configure Personal Assistant broker endpoint for Compose.
- [ ] Add broker health check.
- [ ] Add manual verification note for Compose startup.

## Phase 1 - Global settings

- [ ] Add `AgentRuntimeGlobalSettings` domain entity.
- [ ] Add EF configuration.
- [ ] Add migration.
- [ ] Seed default settings.
- [ ] Add settings request/response DTOs.
- [ ] Add application service interface.
- [ ] Add infrastructure implementation.
- [ ] Add API endpoints.
- [ ] Add UI API client methods.
- [ ] Add Agent Runtime section to Configuration page.
- [ ] Add validation tests.
- [ ] Add endpoint integration tests.

## Phase 2 - Import preview

- [ ] Extend manifest model for `dotnet-project`.
- [ ] Add package source/version entities.
- [ ] Add import preview DTOs.
- [ ] Add GitHub URL normalization.
- [ ] Add commit SHA resolution.
- [ ] Fetch root `csweet-agent.json`.
- [ ] Validate manifest without running source code.
- [ ] Persist preview record.
- [ ] Add Import Agent button to Agents page.
- [ ] Add import preview dialog.
- [ ] Add URL/manifest tests.

## Phase 3 - Install and schedule

- [ ] Add `AgentInstallation`.
- [ ] Add `AgentInstallationGrant`.
- [ ] Add `AgentSchedule`.
- [ ] Add activation mode enum.
- [ ] Add overlap policy enum.
- [ ] Add install/schedule DTOs.
- [ ] Add install endpoint.
- [ ] Add schedule update endpoint.
- [ ] Add run-now endpoint.
- [ ] Validate grant is no broader than manifest.
- [ ] Validate tick frequency against global minimum.
- [ ] Update broker policy to load persisted grants.
- [ ] Add install step to import dialog.
- [ ] Add schedule controls to Agents page.

## Phase 4 - Build pipeline

- [ ] Add `AgentBuildJob`.
- [ ] Add build status enum.
- [ ] Add build service interface.
- [ ] Add build worker hosted service.
- [ ] Clone approved commit into application-owned workspace.
- [ ] Run build in isolated builder container.
- [ ] Enforce build limits.
- [ ] Capture build logs.
- [ ] Store package/image digest.
- [ ] Mark package version built.
- [ ] Add tests for build state transitions.

## Phase 5 - Container runner

- [x] Add `IAgentContainerRunner`.
- [x] Add container start request model.
- [x] Add container status model.
- [x] Add Docker runner implementation.
- [x] Apply CPU/memory limits and carry the approved runtime limit for manager enforcement.
- [x] Pass approved environment variables only.
- [x] Deny privileged mode.
- [x] Deny host Docker socket mount.
- [x] Add fake runner for tests.

## Phase 6 - Scheduler and runtime manager

- [x] Add `AgentRuntimeInstance`.
- [x] Add `AgentRuntimeEvent`.
- [x] Add runtime status enum.
- [x] Add runtime manager interface.
- [x] Add runtime manager implementation.
- [x] Add schedule worker hosted service.
- [x] Claim due schedules safely.
- [x] Enforce concurrency limits.
- [x] Enforce overlap policy.
- [x] Start container for due tick.
- [x] Wait for broker registration.
- [x] Handle completion event.
- [x] Handle runtime timeout.
- [x] Compute next tick.
- [x] Add scheduler/runtime tests.

## Phase 7 - Agents page management

- [ ] Show imported installations in agent list.
- [ ] Show build/schedule/runtime status badges.
- [ ] Show tick frequency and next run.
- [ ] Add Run Now action.
- [ ] Add Enable/Disable action.
- [ ] Add Edit Schedule action.
- [ ] Add View Runs action.
- [ ] Add View Build Log action.
- [ ] Preserve existing configurable-agent editor behavior.

## Phase 8 - Cleanup and observability

- [ ] Add cleanup worker.
- [ ] Remove completed containers when configured.
- [ ] Remove old workspaces when configured.
- [ ] Retain failed logs according to settings.
- [ ] Add audit events.
- [ ] Add metrics/logging for starts, stops, failures, and timeouts.
- [ ] Add rate limits for import/build/run-now.
- [ ] Add final manual end-to-end QA script.

## Final end-to-end acceptance

- [ ] Admin can edit global agent/container settings from Configuration.
- [ ] Admin can import a public GitHub URL from Agents.
- [ ] App previews root `csweet-agent.json` without executing code.
- [ ] Admin can approve grants and configure tick frequency.
- [ ] App clones the approved commit.
- [ ] App builds the .NET project in an isolated builder.
- [ ] A due tick starts a container.
- [ ] Agent registers with the broker.
- [ ] Agent completes work and reports completion.
- [ ] Runtime manager stops/removes the container.
- [ ] Next tick is scheduled.
- [ ] Runtime history is visible from the Agents page.
