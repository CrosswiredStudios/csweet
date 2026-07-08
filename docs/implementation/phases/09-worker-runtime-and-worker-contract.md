# Phase 9 - Worker Runtime and Worker Contract

## Goal

Define and implement the first version of the C-Sweet worker contract and local worker runtime, so task execution can be routed through a consistent worker model.

## Why this phase matters

C-Sweet needs to support built-in local AI workers now and marketplace workers later. A clear worker contract prevents marketplace integration from requiring a rewrite.

## Deliverables

- Worker manifest contract.
- Task execution request contract.
- Task execution response contract.
- Local worker registry.
- Built-in local strategy worker.
- Worker host health endpoint.
- Task execution routed through worker abstraction.
- Worker logs persisted.

## Worker manifest

```json
{
  "workerId": "local-strategy-agent",
  "name": "Local Strategy Agent",
  "description": "Creates practical business plans and task breakdowns using the configured local LLM provider.",
  "workerType": "LocalAgent",
  "executionMode": "LocalWorkerHost",
  "capabilities": [
    "business-planning",
    "operating-plan",
    "task-breakdown",
    "risk-identification"
  ],
  "inputSchema": {},
  "outputSchema": {},
  "pricing": {
    "type": "free-local"
  },
  "requiresApproval": true
}
```

## Task execution request

```json
{
  "taskId": "task_123",
  "taskRunId": "run_123",
  "organizationContext": {
    "name": "Example Co",
    "industry": "Software",
    "stage": "Pre-revenue",
    "primaryGoal": "Launch MVP in 30 days"
  },
  "roleContext": {
    "roleName": "CEO",
    "responsibilities": []
  },
  "task": {
    "title": "Create 30-day launch plan",
    "description": "Create a practical operating plan."
  },
  "constraints": [],
  "availableArtifacts": []
}
```

## Task execution response

```json
{
  "status": "completed",
  "artifactType": "Plan",
  "title": "30-Day Operating Plan",
  "content": "# 30-Day Operating Plan\n...",
  "summary": "A week-by-week launch plan.",
  "recommendedNextTasks": [
    {
      "title": "Define target customer",
      "description": "Clarify the customer segment most likely to buy first."
    }
  ],
  "logs": []
}
```

## Worker host endpoints

### Health

```http
GET /api/worker-host/health
```

Response:

```json
{
  "status": "ok",
  "service": "CSweet.WorkerHost",
  "version": "0.1.0"
}
```

### List workers

```http
GET /api/worker-host/workers
```

### Execute worker

```http
POST /api/worker-host/workers/{workerId}/execute
```

For the first version, the worker host may be internal-only and called by API/application code. Do not expose it publicly without auth.

## Local worker registry

Create a registry:

```csharp
public interface IWorkerRegistry
{
    Task<IReadOnlyList<WorkerManifest>> ListAsync(CancellationToken cancellationToken);
    Task<WorkerManifest?> FindAsync(string workerId, CancellationToken cancellationToken);
}
```

Create executor:

```csharp
public interface IWorkerExecutor
{
    Task<WorkerExecutionResponse> ExecuteAsync(
        WorkerExecutionRequest request,
        CancellationToken cancellationToken);
}
```

## Built-in Local Strategy Agent

The local strategy worker should wrap the `IAgentRunner` from Phase 5.

Flow:

```text
WorkerExecutionRequest
  → validate capability
  → assemble prompt
  → call IAgentRunner
  → map result to WorkerExecutionResponse
```

## Capability matching

When assigning a task to a worker, check that at least one required task capability matches worker capabilities.

Initial task capability field:

```text
RequiredCapability: business-planning
```

Later this can become a list.

## Worker logs

Persist logs at task run level.

Recommended fields:

```text
WorkerRunLog
  Id
  TaskRunId
  WorkerId
  Level
  Message
  MetadataJson
  CreatedAt
```

Do not log secrets.

## Testing requirements

### Unit tests

- Worker registry returns local strategy worker.
- Capability matching succeeds for `business-planning`.
- Capability matching fails for unsupported capability.
- Local strategy worker maps agent result to artifact response.
- Worker failure maps to failed execution response.

### Integration tests

- Worker host health endpoint returns ok.
- List workers includes local strategy agent.
- Execute worker with fake agent runner returns completed response.

## Acceptance criteria

- [ ] Worker manifest contract exists.
- [ ] Task execution request/response contracts exist.
- [ ] Local strategy worker is registered.
- [ ] Worker host reports health.
- [ ] Tasks can execute through worker abstraction.
- [ ] Worker logs are persisted.
- [ ] Design supports future marketplace workers.
