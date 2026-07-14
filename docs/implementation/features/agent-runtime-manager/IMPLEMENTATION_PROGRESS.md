# Agent Runtime Manager - Implementation Progress

Last updated: 2026-07-13

## Phase 1 - Global Agent Runtime Settings

- [x] Domain entity: `AgentRuntimeGlobalSettings`
- [x] Enums: `ActivationMode`, `OverlapPolicy`, `RestartPolicy`
- [x] EF configuration in `CSweetDbContext.cs`
- [x] DbContext DbSet registration
- [x] Migration: `AddAgentRuntimeGlobalSettings`
- [x] Seed defaults in `SetupService`
- [x] Contracts: `AgentRuntimeSettingsResponse`, `UpdateAgentRuntimeSettingsRequest`
- [x] Application: `IAgentRuntimeSettingsService`
- [x] Infrastructure: `AgentRuntimeSettingsService`
- [x] DependencyInjection registration
- [x] API: `AgentRuntimeSettingsEndpoints`
- [x] API: `Program.cs` endpoint registration
- [x] Audit event on settings changes
- [x] UI: API client methods
- [x] UI: Agent Runtime section in Configuration.razor
- [x] UI: runtime, container, network, storage, build, and retention settings
- [x] Unit tests for validation and partial updates
- [x] Integration tests for GET, PUT, invalid updates, persistence, and auditing

## Phase 2 - Import Preview

- [x] Manifest runtime fields for `dotnet-project`
- [x] Domain entities: `AgentPackageSource`, `AgentPackageVersion`
- [x] EF configuration and `AddAgentImportPreview` migration
- [x] Import preview request, response, and warning contracts
- [x] Public GitHub repository URL normalization
- [x] Default branch and immutable commit SHA resolution
- [x] Root-only `csweet-agent.json` fetch with 1 MB limit
- [x] Manifest validation without executing repository code
- [x] SHA-256 manifest digest and idempotent `Previewed` persistence
- [x] Audit event for new import previews
- [x] API: `POST /api/agents/imports/preview`
- [x] UI: Import Agent dialog and complete manifest summary
- [x] Unit tests for manifest fields, URLs, validation, persistence, and GitHub requests
- [x] Integration tests for preview endpoint and persistence

## Phase 3 - Install & Schedule

- [x] Domain entities: `AgentInstallation`, `AgentInstallationGrant`, `AgentSchedule`
- [x] EF configuration and `AddAgentInstallations` migration
- [x] Install and schedule request/response contracts
- [x] Grant intersection validation against the immutable manifest
- [x] Tick frequency, activation policy, runtime, memory, and CPU validation
- [x] Initial persisted next-tick calculation
- [x] Install, list, detail, schedule update, run-now, and disable endpoints
- [x] Audit events for approval and schedule management actions
- [x] AgentHost authorization from persisted installation grants
- [x] Import dialog approval and resource/schedule controls
- [x] Installed-agent schedule cards and management controls
- [x] Unit and integration tests for grants, schedules, persistence, endpoints, and broker policy

## Phase 4 - Build Pipeline

- [x] Domain entity and lifecycle validation: `AgentBuildJob`, `AgentBuildStatus`
- [x] EF configuration and `AddAgentBuildPipeline` migration
- [x] Installation approval queues one package build across business installations
- [x] Idempotent active-job queueing and numbered retry attempts
- [x] `IAgentBuildService` orchestration and persisted build-state transitions
- [x] `AgentBuildWorker` hosted by `CSweet.WorkerHost`
- [x] Exact approved commit materialization without repository code execution
- [x] Docker builder with read-only source, disposable workspace, and writable output only
- [x] Builder CPU, memory, PID, timeout, repository, and log limits
- [x] Builder capability drop, no-new-privileges, non-root user, and no network by default
- [x] Content-addressed immutable package directory and SHA-256 digest
- [x] Package version `Built`/`Failed` state and build audit events
- [x] Failed/cancelled builder-container cleanup and configurable workspace retention
- [x] Compose worker Docker CLI, socket access, and durable source/package volumes
- [x] Unit tests for lifecycle, success, failure, retry, and package-build reuse
- [x] Full test suite, Compose validation, worker image build, and isolated sample publish smoke test

## Phase 5 - Container Runner

- [x] `IAgentContainerRunner` abstraction with start, stop, inspect, remove, and bounded logs
- [x] Container start request and normalized status/state models
- [x] Docker CLI runner for local and Compose environments
- [x] Read-only package mount with enforced CPU, memory, and PID limits; approved max runtime carried for Phase 6 timeout enforcement
- [x] Non-root execution, dropped capabilities, no-new-privileges, read-only root filesystem, and isolated network
- [x] Fixed approved runtime environment; no arbitrary environment variables, database credentials, or host secrets
- [x] No privileged mode or host Docker socket mounts
- [x] Structured start, stop, remove, and failure logging
- [x] Fake runner and Docker argument/security unit tests

## Phase 6 - Scheduler & Runtime Manager

- [x] Domain entities and migration: `AgentRuntimeInstance`, `AgentRuntimeEvent`, and complete runtime status lifecycle
- [x] Durable transition history with timestamps, reasons, completion payloads, and terminal outcomes
- [x] `IAgentRuntimeManager` and `AgentScheduleWorker` hosted by `CSweet.WorkerHost`
- [x] Due periodic/manual schedule polling and optimistic row claiming through `NextTickAt`
- [x] Global, business, and installation container concurrency enforcement
- [x] `Skip`, `Queue`, and `CancelPrevious` overlap behavior
- [x] Built-package container launch through `IAgentContainerRunner`
- [x] Broker registration and max-runtime timeout reconciliation
- [x] Container exit, stop, removal, completion, and next-tick persistence
- [x] Runtime instance ID, tick ID, and bounded workload token in SDK/broker registration context
- [x] Workload-token hashing and fixed-time validation before broker registration
- [x] `com.csweet.runtime.completed.v1` identity validation and persisted completion signaling
- [x] Compose AgentHost service, fixed runtime network, and read-only package volume subpath mounting
- [x] Unit tests for due starts, overlap skip, concurrency blocking, completion, and timeout
- [x] Full unit and integration test suites

## Phase 7 - Agents Page Management

- [x] Unified Agent Fleet view for first-party agents and imported installations
- [x] Installed, build queued/building/failed/built, scheduled, running, and disabled status badges
- [x] Activation mode, tick frequency, next tick, resource limits, and latest run result summaries
- [x] Import Agent, Run Now, Enable/Disable, and Edit Schedule actions
- [x] Runtime history dialog with lifecycle events, timestamps, and outcome reasons
- [x] Retained build-log dialog with bounded API response and truncation indicator
- [x] Installation enable, runtime-history, and build-log API endpoints and UI client methods
- [x] Existing first-party per-agent configuration-schema editor preserved
- [x] Integration coverage for enable, run history, and build-log endpoints
- [x] UI and API builds plus full unit/integration verification

## Phase 8 - Cleanup & Observability

- [x] `AgentRuntimeCleanupWorker` hourly reconciliation with scoped cleanup service
- [x] Deferred terminal-container inspection/removal after runtime-manager or Docker failures
- [x] Safe source-workspace deletion constrained to the approved source root
- [x] Build-log deletion constrained to the approved package root and configured retention window
- [x] Separate completed and failed runtime-history retention windows with event cascade cleanup
- [x] Bounded container-log excerpts retained with runtime history and visible in View Runs
- [x] OpenTelemetry runtime counters and duration histogram for ticks, starts, stops, outcomes, and cleanup
- [x] Structured lifecycle, policy, timeout, failure, and cleanup logging
- [x] Audit events for schedule ticks, container start/stop, completion, timeout, policy denial, failure, and cleanup
- [x] Fixed-window per-client rate limits for import preview, build-triggering install approval, and Run Now
- [x] Bounded schedule claims and sequential build processing for internal worker backpressure
- [x] Always-on restart-policy reconciliation and active-runtime cancellation after disable
- [x] Final manual end-to-end QA script: `scripts/agent-runtime-e2e.ps1`
- [x] Cleanup, retention, audit, and API rate-limit tests

## Notes

- Phase 1 is the foundation. All subsequent phases depend on the global settings.
- Phase 4 uses the host Docker daemon from the trusted WorkerHost orchestrator. The untrusted builder
  container never receives the Docker socket and has no outbound network; projects that require
  external NuGet packages need those packages pre-seeded in a custom builder image until an
  approved-feed egress proxy is added.
- Run Now marks a persisted schedule as immediately due; Phase 6 adds the worker that claims and executes due schedules.
