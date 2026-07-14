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

- [ ] Not started

## Phase 4 - Build Pipeline

- [ ] Skipped (requires Docker builder infrastructure)

## Phase 5 - Container Runner

- [ ] Not started

## Phase 6 - Scheduler & Runtime Manager

- [ ] Not started

## Phase 7 - Agents Page Management

- [ ] Not started

## Phase 8 - Cleanup & Observability

- [ ] Not started

## Notes

- Phase 1 is the foundation. All subsequent phases depend on the global settings.
- Phase 4 (build pipeline) requires Docker-in-Docker or a remote builder, which may need infrastructure setup first.
