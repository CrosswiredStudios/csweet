# Phase 4 - System Setup Wizard

## Goal

Build the first-run setup UI that guides a new user through system configuration before they create their first business.

## Why this phase matters

Without a guided setup, users will hit broken workflows because the app cannot call an LLM. The wizard should make the local/self-hosted experience feel polished and reliable.

## Deliverables

- `/setup` route in Blazor WASM.
- Setup status API client.
- Wizard layout and progress indicator.
- LLM provider step with LM Studio preset.
- Model capability test step.
- Storage status step.
- Worker runtime status step.
- Admin setup step.
- Finish step.
- Redirect guard for normal routes.

## Wizard steps

### Step 1 - Welcome

Purpose:

- Explain what C-Sweet is.
- Explain that setup configures local infrastructure before business onboarding.

Content:

```text
Welcome to C-Sweet.
Before creating your first business, we need to configure how this instance will run AI workers and store its data.
```

Actions:

- Continue.

### Step 2 - LLM Provider Setup

Purpose:

Create the default chat provider profile.

Fields:

```text
Provider Type
Provider Name
Base URL
API Key / Token
Default Chat Model
Default Embedding Model optional
Context Window Tokens optional
Max Output Tokens optional
```

Provider type options:

```text
LM Studio
OpenAI-compatible
OpenAI
Azure OpenAI
Anthropic
Ollama
Microsoft Foundry
Custom
```

LM Studio default values:

```text
Provider Type: LM Studio
Provider Name: Local LM Studio
Base URL: http://localhost:1234/v1
API Key: lm-studio
```

### Step 3 - Model Capability Test

Purpose:

Verify the configured provider works.

Show checks:

```text
Connection
Model list
Chat completion
Streaming
Structured JSON output
Tool calling
Embeddings optional
```

UI behavior:

- Use green check for success.
- Use yellow warning for optional unsupported capabilities.
- Use red error only for required capabilities.

Required for setup:

- Connection succeeds.
- Chat completion succeeds.

Optional for setup:

- Streaming.
- Structured output.
- Tool calling.
- Vision.
- Embeddings.

### Step 4 - Storage Setup

Purpose:

Show database/storage health.

Initial checks:

- API can reach database.
- Migrations are applied.
- System configuration row exists.

Future checks:

- File storage.
- Vector database.
- Object storage.
- Backup target.

### Step 5 - Worker Runtime Setup

Purpose:

Check local worker runtime availability.

Initial checks:

- `CSweet.WorkerHost` is reachable.
- Worker host reports version.
- Built-in local strategy worker is registered or available.

### Step 6 - Admin User Setup

Purpose:

Create the local owner/admin identity.

Initial implementation options:

- Simple local profile without full auth.
- Or ASP.NET Core Identity if authentication is part of the first milestone.

Recommended for first vertical slice:

- Create a minimal local owner profile first.
- Add full authentication in a dedicated later phase if needed.

Fields:

```text
Name
Email optional
Display name
```

### Step 7 - Finish

Purpose:

Complete setup and unlock business onboarding.

Rules:

- Required steps must be complete.
- Default chat provider must be set.
- Last capability test must have chat success.
- Storage check must pass.
- Worker runtime check should pass or show a warning if the first version can run without it.

On finish:

- Call `POST /api/setup/complete`.
- Redirect to `/business-onboarding`.

## Route guard behavior

When the app starts:

1. Call `GET /api/setup/status`.
2. If incomplete, redirect to `/setup`.
3. If complete and user is at `/setup`, redirect to dashboard or business onboarding depending on business state.

Allowed routes while setup incomplete:

```text
/setup
/setup/*
/error
```

## API client methods

Create a typed client in `CSweet.App`:

```csharp
public interface ISetupApiClient
{
    Task<SetupStatusDto> GetStatusAsync(CancellationToken cancellationToken);
    Task CompleteStepAsync(string key, CancellationToken cancellationToken);
    Task CompleteSetupAsync(CancellationToken cancellationToken);
}
```

Create provider client:

```csharp
public interface ILlmProviderApiClient
{
    Task<LlmProviderProfileDto> CreateAsync(CreateLlmProviderProfileRequest request, CancellationToken cancellationToken);
    Task<ModelCapabilityTestResultDto> TestAsync(Guid providerProfileId, CancellationToken cancellationToken);
    Task SetDefaultChatProviderAsync(Guid providerProfileId, CancellationToken cancellationToken);
}
```

## UI states

Each step should handle:

- Loading.
- Success.
- Validation error.
- API error.
- Provider timeout.
- Retry.

Do not leave the user on a blank screen during provider tests.

## Testing requirements

### Unit/component tests

- Wizard shows first incomplete step.
- LM Studio preset populates base URL.
- Finish button disabled until required steps complete.
- Provider test error appears clearly.

### Integration/manual tests

- Fresh database redirects to `/setup`.
- Completing setup redirects to `/business-onboarding`.
- Incomplete setup prevents dashboard access.

## Acceptance criteria

- [x] `/setup` exists.
- [x] Wizard reads setup status from API.
- [x] LM Studio preset is selectable.
- [x] User can create provider profile from wizard.
- [x] User can run provider capability test.
- [x] User can set default chat provider.
- [x] Required setup status blocks finish until satisfied.
- [x] Completing setup redirects to business onboarding.
- [x] UI clearly communicates unsupported optional capabilities.

## Implementation status

Completed in the phase 4 implementation pass.

Verified:

- `dotnet build CSweet.sln`
- `dotnet test CSweet.sln`
- `curl.exe http://localhost:5149/api/health`
- `curl.exe -I http://localhost:5097/setup`

Notes:

- The wizard uses typed Blazor API clients for setup status, step completion, provider profile creation, provider testing, and default chat provider selection.
- Admin user details are captured in the UI and mark the setup step complete; durable admin identity persistence remains future authentication/configuration work.
- Worker runtime checks are shown as optional warnings until a worker health endpoint exists.
- A `/business-onboarding` placeholder route was added as the post-setup redirect target.
