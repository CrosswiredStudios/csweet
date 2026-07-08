# Phase 10 - Observability, Security, and Operations

## Goal

Add the operational foundation needed for a reliable self-hosted product: logging, tracing, health checks, audit history, safe secret handling, and deployment notes.

## Why this phase matters

C-Sweet will call local and remote AI providers, run long task workflows, store sensitive business context, and eventually interact with third-party workers. Debuggability and safety must be part of the foundation.

## Deliverables

- Structured logging.
- OpenTelemetry tracing through Aspire ServiceDefaults.
- Health checks.
- Provider health status.
- Worker health status.
- Audit event viewer.
- Secret redaction.
- Prompt logging policy.
- Basic operations documentation.

## Logging requirements

Use structured logs.

Important event names:

```text
SetupStarted
SetupCompleted
ProviderProfileCreated
ProviderCapabilityTestStarted
ProviderCapabilityTestCompleted
OrganizationCreated
TaskCreated
TaskRunStarted
TaskRunCompleted
TaskRunFailed
ArtifactCreated
ArtifactApproved
ArtifactRejected
WorkerRegistered
WorkerExecutionStarted
WorkerExecutionCompleted
WorkerExecutionFailed
```

Include correlation IDs where possible.

## Tracing requirements

Trace spans should include:

- HTTP request.
- Application use case.
- Provider call.
- Agent run.
- Worker execution.
- Database operation where automatically available.

Do not include full prompts or API keys in trace attributes.

## Health checks

### API health

```http
GET /api/health
```

### Setup health

```http
GET /api/setup/status
```

### Provider health

```http
GET /api/llm-provider-profiles/{id}/health
```

### Worker host health

```http
GET /api/worker-host/health
```

Health should distinguish:

```text
Healthy
Degraded
Unhealthy
Unknown
```

## Provider status model

```csharp
public sealed record ProviderHealthDto(
    Guid ProviderProfileId,
    string Name,
    HealthState State,
    DateTimeOffset? LastSuccessfulConnectionAt,
    DateTimeOffset? LastTestedAt,
    string? Message);
```

## Secret handling

Rules:

- Never store raw API keys in provider profile rows.
- Never return API keys from API endpoints.
- Never log API keys.
- Redact values for fields named `apiKey`, `token`, `secret`, `password`, `authorization`.
- Use user secrets or environment variables for development.
- Add pluggable secret storage abstraction for production.

Interface:

```csharp
public interface ISecretStore
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
    Task<string> SaveSecretAsync(string scope, string value, CancellationToken cancellationToken);
    Task DeleteSecretAsync(string name, CancellationToken cancellationToken);
}
```

Initial implementation:

- Development: local encrypted or user-secret-backed implementation if practical.
- Fallback: environment variable references.

## Prompt logging policy

Default policy:

```text
Do not store full prompts.
Store prompt hash and short safe preview only.
```

Configuration options later:

```text
Disabled
MetadataOnly
SafePreview
FullPromptLocalOnly
FullPromptWithExplicitConsent
```

## Audit event viewer

Add a simple UI page:

```text
/settings/audit-events
```

Columns:

```text
Date/time
Event type
Entity type
Summary
```

Filters:

```text
Event type
Entity type
Date range
```

## Operations docs

Create docs for:

```text
Local development startup
LM Studio setup
Environment variables
Database migrations
Backup/restore placeholder
Provider troubleshooting
Worker troubleshooting
```

## Testing requirements

### Unit tests

- Secret redactor redacts expected fields.
- Prompt logging policy stores only allowed values.
- Provider health maps capability test results correctly.

### Integration tests

- Health endpoints return expected format.
- Audit events are created for major actions.
- API keys are not returned from provider profile endpoint.

## Manual QA

- Configure provider with fake API key.
- Confirm API response does not include key.
- Trigger provider failure.
- Confirm logs show failure but not secret.
- Confirm audit event appears.
- Confirm health endpoint reports degraded/unhealthy.

## Acceptance criteria

- [ ] Structured logs exist for major events.
- [ ] Health endpoints exist.
- [ ] Provider health can be viewed.
- [ ] Worker health can be viewed.
- [ ] Audit events can be viewed.
- [ ] Secrets are redacted.
- [ ] Prompt logging policy is implemented.
- [ ] Operations docs exist.
