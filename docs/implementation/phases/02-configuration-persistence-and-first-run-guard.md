# Phase 2 - Configuration Persistence and First-Run Guard

## Goal

Add database persistence for system configuration, LLM provider profiles, onboarding state, capability tests, and audit events. Then add the first-run guard that blocks normal app usage until system setup is complete.

## Why this phase matters

C-Sweet needs to know how the system is configured before it can run agents or create useful business workflows. This phase creates the system setup foundation.

## Deliverables

- EF Core installed and configured.
- Postgres configured in Aspire for local development.
- `CSweetDbContext` created.
- Initial system setup entities created.
- Initial migration created.
- Dedicated migrator project created.
- First-run seed logic added.
- API endpoint to get setup status.
- API guard/middleware or endpoint-level checks for setup requirements.
- Audit event writer.

## Entities

### SystemConfiguration

```csharp
public sealed class SystemConfiguration
{
    public Guid Id { get; set; }
    public bool IsFirstRunComplete { get; set; }
    public Guid? DefaultChatProviderId { get; set; }
    public Guid? DefaultEmbeddingProviderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Rules:

- There should be exactly one active system configuration row.
- On first app start, create a row with `IsFirstRunComplete = false`.
- Do not mark first run complete until required setup steps are complete.

### LlmProviderProfile

```csharp
public sealed class LlmProviderProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public LlmProviderType ProviderType { get; set; }

    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKeySecretName { get; set; }

    public string DefaultChatModel { get; set; } = string.Empty;
    public string? DefaultEmbeddingModel { get; set; }

    public int? ContextWindowTokens { get; set; }
    public int? MaxOutputTokens { get; set; }

    public bool SupportsStreaming { get; set; }
    public bool SupportsToolCalling { get; set; }
    public bool SupportsStructuredOutput { get; set; }
    public bool SupportsVision { get; set; }

    public bool IsEnabled { get; set; }
    public DateTimeOffset? LastSuccessfulConnectionAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### LlmProviderType

```csharp
public enum LlmProviderType
{
    LmStudio,
    OpenAiCompatible,
    OpenAi,
    AzureOpenAi,
    Anthropic,
    Ollama,
    MicrosoftFoundry,
    Custom
}
```

### ModelCapabilityTest

```csharp
public sealed class ModelCapabilityTest
{
    public Guid Id { get; set; }
    public Guid ProviderProfileId { get; set; }

    public bool ConnectionSucceeded { get; set; }
    public bool ChatSucceeded { get; set; }
    public bool StreamingSucceeded { get; set; }
    public bool ToolCallingSucceeded { get; set; }
    public bool StructuredOutputSucceeded { get; set; }

    public string? FailureMessage { get; set; }
    public string? RawResult { get; set; }

    public DateTimeOffset TestedAt { get; set; }
}
```

### OnboardingStep

```csharp
public sealed class OnboardingStep
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsComplete { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Initial step keys:

```text
welcome
llm-provider
model-capability-test
storage
worker-runtime
admin-user
finish
```

### AuditEvent

```csharp
public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Summary { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

## DbContext

Create `CSweetDbContext` in `CSweet.Infrastructure`.

```csharp
public sealed class CSweetDbContext : DbContext
{
    public CSweetDbContext(DbContextOptions<CSweetDbContext> options)
        : base(options)
    {
    }

    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<LlmProviderProfile> LlmProviderProfiles => Set<LlmProviderProfile>();
    public DbSet<ModelCapabilityTest> ModelCapabilityTests => Set<ModelCapabilityTest>();
    public DbSet<OnboardingStep> OnboardingSteps => Set<OnboardingStep>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
}
```

## API endpoints

### Get setup status

```http
GET /api/setup/status
```

Response:

```json
{
  "isFirstRunComplete": false,
  "defaultChatProviderId": null,
  "defaultEmbeddingProviderId": null,
  "steps": [
    {
      "key": "llm-provider",
      "displayName": "LLM Provider",
      "isRequired": true,
      "isComplete": false
    }
  ]
}
```

### Complete setup step

```http
POST /api/setup/steps/{key}/complete
```

Rules:

- Only known step keys are accepted.
- Completing a step writes an audit event.
- Completing `finish` is only allowed when all required prior steps are complete.

### Finish first-run setup

```http
POST /api/setup/complete
```

Rules:

- Requires at least one enabled provider profile.
- Requires default chat provider.
- Requires successful chat capability test.
- Requires admin setup completion.
- Sets `IsFirstRunComplete = true`.
- Writes audit event.

## First-run guard

The API and UI should both understand setup state.

### UI behavior

- If `IsFirstRunComplete = false`, redirect to `/setup`.
- Allow `/setup` and setup-related API calls.
- Block business routes until setup is complete.

### API behavior

Protect normal endpoints later with setup guard. Do not block:

```text
/api/health
/api/setup/*
/api/llm-provider-profiles/* while setup is incomplete
/api/model-capability-tests/* while setup is incomplete
```

## Seeding requirements

On startup or migration:

- Ensure one `SystemConfiguration` row exists.
- Ensure required onboarding steps exist.
- Do not overwrite completed steps.

## Testing requirements

### Unit tests

- Creating initial setup state returns incomplete setup.
- Completing a required step sets `IsComplete = true` and `CompletedAt`.
- Finish setup fails if no default chat provider exists.
- Finish setup fails if no successful chat test exists.

### Integration tests

- `GET /api/setup/status` returns 200.
- A fresh database returns `isFirstRunComplete = false`.
- `POST /api/setup/complete` fails when prerequisites are missing.

## Manual QA

- Start app with empty database.
- Confirm setup status is incomplete.
- Confirm Blazor app redirects to `/setup`.
- Confirm `/api/health` still works.

## Acceptance criteria

- [x] EF Core is configured.
- [x] Postgres runs in local Aspire environment.
- [x] Initial migration exists.
- [x] Dedicated migrator project exists.
- [x] Setup status endpoint works.
- [x] Required onboarding steps are seeded.
- [x] First-run setup cannot be completed without provider prerequisites.
- [x] Audit events are created for setup actions.
- [x] Tests cover happy and failure paths.

## Implementation status

Completed in the phase 2 implementation pass.

Verified:

- `dotnet build CSweet.sln`
- `dotnet test CSweet.sln --no-build`

Notes:

- `dotnet ef` was not installed in the local environment, so the initial migration was added directly and validated by build.
- `CSweet.Migrator` applies migrations and seeds setup state; the API and worker do not apply migrations at startup.
- Development runs without a configured Postgres connection string use a durable local SQLite file at `%LOCALAPPDATA%\CSweet\csweet-dev.db` by default; tests override the context with EF Core InMemory; production requires `ConnectionStrings:Postgres`.
- Local provider API keys/tokens use a durable development file at `%LOCALAPPDATA%\CSweet\provider-secrets.json` by default until an external secret store is introduced.
- Aspire Postgres credentials are driven by `src/CSweet.AppHost/appsettings.Development.json` via `CSweet:Postgres:*`, not generated secrets, so the local data volume keeps matching credentials across runs.
- If the `csweet-aspire-postgres` Docker volume was initialized before credentials became config-driven, delete that volume once so Postgres can initialize with the configured `csweet` user/password.
