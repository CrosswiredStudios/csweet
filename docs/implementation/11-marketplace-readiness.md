# 11 - Marketplace Readiness Plan

## Purpose

The marketplace should not be built first, but the core system should be designed so marketplace support can be added without rewriting the task, worker, and artifact models.

The marketplace is expected to support:

- AI worker plugins from third-party companies.
- Proprietary remote workers backed by vendor-owned models and MCP servers.
- Human workers who register, set rates, and complete tasks.
- Marketplace discovery, pricing, ratings, capability matching, and fulfillment tracking.

## Key assumption

The self-hosted core must work without the marketplace. Third-party workers may not function if the self-hosted application cannot reach the third party's server, but built-in local workers should still work.

## Marketplace boundary

### Core C-Sweet owns

- Organizations.
- Roles.
- Tasks.
- Task runs.
- Approvals.
- Artifacts.
- Worker registrations.
- Worker assignment logic.
- Worker capability matching.
- Audit history.
- Local execution status.

### Marketplace owns

- Public worker profiles.
- Human worker identity and payments.
- Vendor worker subscriptions.
- Reviews and ratings.
- Marketplace search/discovery.
- Escrow or payment authorization.
- Worker availability.
- Vendor-hosted endpoints.
- Marketplace terms and dispute process.

## Design for marketplace compatibility now

### Worker entity fields to include early

```text
Worker
  Id
  OrganizationId nullable
  MarketplaceWorkerId nullable
  Name
  Description
  WorkerType
  ExecutionMode
  CapabilitiesJson
  CostModelJson
  EndpointConfigurationJson
  IsEnabled
  RequiresHumanApproval
  CreatedAt
  UpdatedAt
```

### Worker types

```text
LocalAgent
RemoteAgent
Human
McpServer
OpenAiCompatibleService
MarketplaceProxy
BuiltInSystem
```

### Execution modes

```text
InProcess
LocalWorkerHost
HttpRemote
McpRemote
MarketplaceManaged
HumanFulfillment
```

## Marketplace worker contract

The marketplace should eventually provide a signed worker manifest:

```json
{
  "marketplaceWorkerId": "worker_tax_pro_001",
  "name": "Tax Professional",
  "publisher": "Example Accounting Co",
  "workerType": "RemoteAgent",
  "executionMode": "HttpRemote",
  "capabilities": [
    "tax-planning",
    "expense-review",
    "quarterly-estimates"
  ],
  "inputSchemaUrl": "https://marketplace.example/workers/worker_tax_pro_001/input.schema.json",
  "outputSchemaUrl": "https://marketplace.example/workers/worker_tax_pro_001/output.schema.json",
  "pricing": {
    "type": "per-task",
    "amount": 25,
    "currency": "USD"
  },
  "requiresUserConsent": true,
  "dataHandling": {
    "storesData": true,
    "retentionDays": 30,
    "humanReviewPossible": true
  }
}
```

## Core APIs that should be marketplace-ready

### Register worker

```http
POST /api/workers
```

Required behavior:

- Accept local and remote workers.
- Validate capabilities.
- Store worker source.
- Mark marketplace workers as untrusted until user approval.

### Assign task to worker

```http
POST /api/tasks/{taskId}/assign-worker
```

Required behavior:

- Validate that worker supports the task capability.
- Validate that worker is enabled.
- Validate cost/approval requirements.
- Create audit event.

### Execute task

```http
POST /api/tasks/{taskId}/run
```

Required behavior:

- Create `TaskRun`.
- Resolve worker execution mode.
- Execute local or remote flow.
- Persist logs and output.
- Create artifact if successful.
- Mark task run failed if worker fails.

## Marketplace-specific phases for later

### Marketplace Phase A - Hosted marketplace web app

Deliverables:

- Separate web app for marketplace discovery.
- Worker registration.
- Worker profile editing.
- Capability tagging.
- Admin review/approval.
- Public worker search.

### Marketplace Phase B - Marketplace API

Deliverables:

- Worker manifest API.
- Worker search API.
- Worker detail API.
- Pricing API.
- Install worker into self-hosted C-Sweet instance.
- Signed manifest verification.

### Marketplace Phase C - Human worker fulfillment

Deliverables:

- Human worker accounts.
- Availability and rate settings.
- Task offer flow.
- Accept/decline task.
- Deliver artifact.
- User approval and revision requests.

### Marketplace Phase D - Billing and payment

Deliverables:

- Payment provider integration.
- Escrow or preauthorization.
- Platform fee.
- Refund/dispute process.
- Invoices and receipts.

## Rules for current implementation

Do not build marketplace screens yet.

Do build:

- Worker type abstraction.
- Capability model.
- Cost model placeholder.
- Endpoint configuration placeholder.
- Task assignment to a worker.
- Task run history.
- Artifact approval.

That is enough to avoid a rewrite later.
