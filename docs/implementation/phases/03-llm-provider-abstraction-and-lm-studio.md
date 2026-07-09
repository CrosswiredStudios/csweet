# Phase 3 - LLM Provider Abstraction and LM Studio

## Goal

Create the LLM provider abstraction layer and implement the first concrete provider path: LM Studio through an OpenAI-compatible endpoint.

## Why this phase matters

C-Sweet must support many local and hosted LLM configurations. LM Studio is the first default, but it should not be hard-coded into application logic.

## External context

LM Studio can serve local models through OpenAI-compatible endpoints. The common local base URL is:

```text
http://localhost:1234/v1
```

The implementation should use provider configuration so this can also point to another machine on the LAN, a Tailscale address, or a hosted OpenAI-compatible endpoint.

## Deliverables

- `CSweet.AI` interfaces for chat clients and provider testing.
- Provider profile DTOs.
- LM Studio preset.
- OpenAI-compatible provider factory.
- Connection test endpoint.
- Chat test endpoint.
- Capability test persistence.
- Fake provider for unit tests.

## Provider setup DTOs

### CreateLlmProviderProfileRequest

```csharp
public sealed record CreateLlmProviderProfileRequest(
    string Name,
    LlmProviderType ProviderType,
    string BaseUrl,
    string? ApiKey,
    string DefaultChatModel,
    string? DefaultEmbeddingModel,
    int? ContextWindowTokens,
    int? MaxOutputTokens,
    bool SupportsStreaming,
    bool SupportsToolCalling,
    bool SupportsStructuredOutput,
    bool SupportsVision);
```

Important:

- `ApiKey` should never be stored directly in the `LlmProviderProfile` table.
- Store the key in a secret store if available.
- Store only `ApiKeySecretName` in the database.
- For local LM Studio without auth, allow a placeholder key such as `lm-studio` if the client library requires a value.

### LmStudio preset

Add a preset method that returns defaults:

```csharp
public static LlmProviderPreset LmStudioLocalhost()
{
    return new LlmProviderPreset
    {
        Name = "Local LM Studio",
        ProviderType = LlmProviderType.LmStudio,
        BaseUrl = "http://localhost:1234/v1",
        ApiKeyPlaceholder = "lm-studio",
        SupportsStreaming = true,
        SupportsToolCalling = false,
        SupportsStructuredOutput = false,
        SupportsVision = false
    };
}
```

Do not assume tool calling, structured output, or vision. Detect and store capabilities.

## Interfaces

### ILlmProviderFactory

```csharp
public interface ILlmProviderFactory
{
    Task<IChatClient> CreateChatClientAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken);
}
```

If the current Microsoft.Extensions.AI package supports synchronous creation, a non-async method is acceptable. Async is recommended if secret retrieval or provider metadata lookup is required.

### ILlmConnectionTester

```csharp
public interface ILlmConnectionTester
{
    Task<ModelCapabilityTestResult> TestAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken);
}
```

### IModelCatalogClient

```csharp
public interface IModelCatalogClient
{
    Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken);
}
```

Not all providers expose a model list. If not supported, return a typed unsupported result instead of throwing raw exceptions.

## Capability test flow

The test should run in this order:

1. Validate provider profile exists.
2. Validate base URL is well-formed.
3. Try model list if provider supports `/models`.
4. Run simple chat completion.
5. Try streaming if requested.
6. Try structured JSON if requested.
7. Try tool calling if requested.
8. Save `ModelCapabilityTest`.
9. Update provider profile capability flags based on results.
10. Write audit event.

## Basic chat test

Prompt:

```text
Return the word READY and nothing else.
```

Expected acceptable result:

```text
READY
```

The test should be tolerant of whitespace and casing.

## Structured output test

Prompt:

```text
Return only a JSON object with this exact shape: {"status":"ready"}
```

Validation:

- Response must parse as JSON.
- `status` must equal `ready`.

If this fails, the provider can still be usable. Mark structured output unsupported.

## Tool calling test

Use a simple fake tool such as:

```text
get_current_time
```

The model should call the tool when asked:

```text
Use the provided tool to get the current time.
```

If tool calling fails, mark unsupported. This should not block setup unless the selected workflow requires tool calling.

## API endpoints

### Create provider profile

```http
POST /api/llm-provider-profiles
```

### List provider profiles

```http
GET /api/llm-provider-profiles
```

### Get provider profile

```http
GET /api/llm-provider-profiles/{id}
```

### Test provider

```http
POST /api/llm-provider-profiles/{id}/test
```

Response:

```json
{
  "providerProfileId": "...",
  "connectionSucceeded": true,
  "chatSucceeded": true,
  "streamingSucceeded": true,
  "structuredOutputSucceeded": false,
  "toolCallingSucceeded": false,
  "failureMessage": null
}
```

### Set default chat provider

```http
POST /api/setup/default-chat-provider
```

Request:

```json
{
  "providerProfileId": "..."
}
```

Rules:

- Provider must exist.
- Provider must be enabled.
- Provider must have successful chat test.

## Error handling

Return typed errors for:

- Invalid base URL.
- Provider unreachable.
- Model missing.
- Timeout.
- Auth failure.
- Chat completion failed.
- Unsupported capability.

Do not leak API keys in errors.

## Testing requirements

### Unit tests

- LM Studio preset returns expected defaults.
- Invalid base URL is rejected.
- Chat test marks success when response is `READY`.
- Chat test marks failure on timeout.
- Structured output test fails gracefully when response is not JSON.
- Tool calling unsupported does not fail the whole provider setup.

### Integration tests

Use a fake provider HTTP server, not real LM Studio, for automated integration tests.

Test cases:

- `/models` returns model list.
- `/chat/completions` returns `READY`.
- provider test persists `ModelCapabilityTest`.
- profile `LastSuccessfulConnectionAt` is updated.

## Manual LM Studio QA

1. Open LM Studio.
2. Load a chat-capable model.
3. Start the local server on port `1234`.
4. Confirm base URL: `http://localhost:1234/v1`.
5. In C-Sweet setup, choose LM Studio.
6. Enter or select the model ID.
7. Run connection test.
8. Confirm chat test succeeds.
9. Confirm capability results are shown honestly.

## Acceptance criteria

- [x] Provider profiles can be created.
- [x] LM Studio preset exists.
- [x] Provider profile can be tested.
- [x] Test result is persisted.
- [x] Default chat provider can be selected only after successful chat test.
- [x] The application does not hard-code LM Studio outside the preset/factory area.
- [x] No API keys appear in logs or responses.

## Implementation status

Completed in the phase 3 implementation pass.

Verified:

- `dotnet build CSweet.sln`
- `dotnet test CSweet.sln`

Notes:

- The OpenAI-compatible provider path is implemented with typed HTTP calls for `/models` and `/chat/completions`, plus a Microsoft.Extensions.AI `IChatClient` factory for runtime chat-client creation.
- API keys are accepted only on create, stored behind `ApiKeySecretName`, and omitted from provider responses. The current secret-store implementation is in-memory until a durable secret store is added.
- Automated integration tests use a fake OpenAI-compatible HTTP server path, not real LM Studio.
