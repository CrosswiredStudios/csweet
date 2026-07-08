# Phase 5 - Microsoft Agent Framework Integration

## Goal

Add a thin adapter over Microsoft Agent Framework so C-Sweet can run role-based agents and workflows without coupling the entire codebase to framework-specific types.

## Why this phase matters

C-Sweet needs agentic behavior, but it also needs clean enterprise architecture. Agent Framework should power specialized agent behavior and workflows; it should not replace the domain model or normal application services.

## What to use Agent Framework for

Use Agent Framework for:

- Role-based agents.
- Multi-step workflows.
- Business planning.
- Artifact drafting.
- Tool/MCP use.
- Human-in-the-loop review flows.
- Workflow checkpointing when available.

Do not use Agent Framework for:

- CRUD.
- Data validation.
- Auth.
- Database persistence.
- Billing.
- Basic status transitions.
- Simple deterministic mapping.

## Deliverables

- Agent abstraction interfaces in `CSweet.Application` or `CSweet.AI`.
- Agent Framework adapter implementation in `CSweet.AI`.
- Basic agent runner.
- Basic workflow runner placeholder.
- First `BusinessStrategistAgent` profile.
- Agent run logging.
- Fake agent runner for tests.

## Interfaces

### IAgentRunner

```csharp
public interface IAgentRunner
{
    Task<AgentRunResult> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken);
}
```

### AgentRunRequest

```csharp
public sealed record AgentRunRequest(
    Guid ProviderProfileId,
    string AgentKey,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyDictionary<string, string> Context,
    AgentRunOptions Options);
```

### AgentRunOptions

```csharp
public sealed record AgentRunOptions(
    double? Temperature,
    int? MaxOutputTokens,
    bool RequireStructuredOutput,
    string? OutputSchemaJson);
```

### AgentRunResult

```csharp
public sealed record AgentRunResult(
    bool Succeeded,
    string? Content,
    string? StructuredJson,
    string? FailureMessage,
    IReadOnlyList<AgentRunLogEntry> Logs);
```

## Adapter design

Create an implementation similar to:

```text
CSweet.AI
  /AgentFramework
    AgentFrameworkAgentRunner.cs
    AgentFrameworkWorkflowRunner.cs
    AgentFrameworkToolRegistry.cs
    AgentFrameworkAgentFactory.cs
```

The rest of the app should call `IAgentRunner`, not direct Agent Framework types.

## First agent profile

### BusinessStrategistAgent

Purpose:

Produce practical, early-stage business planning artifacts from organization context.

Initial system prompt:

```text
You are a practical business strategy agent inside C-Sweet.
Your job is to produce clear, actionable plans for a small business owner.
Avoid hype. Make assumptions explicit. Prefer concrete next actions.
When the user provides incomplete context, proceed with reasonable assumptions and list them.
```

Capabilities:

```text
business-planning
operating-plan
task-breakdown
risk-identification
```

## Agent run logging

Persist enough information to debug runs without leaking sensitive data by default.

Recommended fields:

```text
AgentRunLog
  Id
  TaskRunId nullable
  AgentKey
  ProviderProfileId
  StartedAt
  CompletedAt
  Status
  PromptHash
  PromptPreview optional
  OutputPreview optional
  FailureMessage
  TokenInputCount nullable
  TokenOutputCount nullable
  DurationMs
```

Do not store full prompts by default until a prompt logging policy exists.

## Workflow placeholder

Do not build complex workflows yet. Add a placeholder for future use:

```csharp
public interface IAgentWorkflowRunner
{
    Task<AgentWorkflowRunResult> RunAsync(
        AgentWorkflowRunRequest request,
        CancellationToken cancellationToken);
}
```

First implementation can internally call one agent.

## Human-in-the-loop alignment

Agent Framework supports human-in-the-loop workflow patterns. C-Sweet should align this with its own approval model:

```text
Agent drafts artifact
  → C-Sweet creates artifact
  → Artifact waits for user approval
  → User approves/rejects
  → Workflow can continue or revise later
```

Do not invent a separate approval model only for Agent Framework.

## Testing requirements

### Unit tests

Use a fake `IAgentRunner`.

Test:

- Successful agent result maps to artifact content.
- Failed agent result marks task run failed.
- Required structured output failure is handled gracefully.
- Agent run logs redact secrets.

### Integration tests

Use a fake `IChatClient` where possible.

Test:

- Agent runner can call provider factory.
- Agent result is returned.
- Failure is captured in logs.

## Manual QA

With LM Studio running:

- Configure provider.
- Run a simple `BusinessStrategistAgent` prompt.
- Confirm output appears.
- Confirm run logs are saved.
- Confirm failures are shown clearly when LM Studio is stopped.

## Acceptance criteria

- [ ] `IAgentRunner` exists.
- [ ] Agent Framework adapter exists.
- [ ] Application code does not depend directly on Agent Framework types.
- [ ] First business strategy agent can run against configured provider.
- [ ] Agent failures are handled and logged.
- [ ] Tests can run with fake agent runner.
