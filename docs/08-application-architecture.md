# Application Architecture

## Architectural style

Start as a modular monolith with two executable applications:

1. `CSweet.Web` — Blazor UI, APIs, authentication, and real-time client updates
2. `CSweet.Worker` — background orchestration, agent execution, provider calls, schedules, and long-running work

Both use shared domain and application modules and initially share PostgreSQL.

Split services only when scaling, trust isolation, or deployment constraints justify it.

## Logical layers

```text
Blazor CEO and Professional Interfaces
                ↓
Application Commands, Queries, and Events
                ↓
Company / Workforce / Marketplace Domain
                ↓
Orchestration and Runtime Adapters
                ↓
Models, MCP, Providers, Humans, and Tools
                ↓
Persistence, Artifacts, Telemetry, and Secrets
```

## Suggested solution structure

```text
src/
  CSweet.Domain/
  CSweet.Application/
  CSweet.Contracts/
  CSweet.Infrastructure/

  CSweet.Companies/
  CSweet.Workforce/
  CSweet.Orchestration/
  CSweet.Budgeting/
  CSweet.Approvals/
  CSweet.Artifacts/

  CSweet.AgentRuntime.Abstractions/
  CSweet.AgentRuntime.Maf/
  CSweet.ModelProviders/
  CSweet.Tools/
  CSweet.Mcp/

  CSweet.RemoteWorkers.Contracts/
  CSweet.RemoteWorkers.Client/

  CSweet.Marketplace.Contracts/
  CSweet.Engagements/
  CSweet.ProfessionalProfiles/
  CSweet.TimeTracking/

  CSweet.Web/
  CSweet.Worker/

sdk/
  CSweet.RemoteWorkers.AspNetCore/
  CSweet.RemoteWorkers.TestKit/
  CSweet.WorkerManifest/

tests/
  CSweet.Domain.Tests/
  CSweet.Application.Tests/
  CSweet.AgentRuntime.Tests/
  CSweet.IntegrationTests/
  CSweet.ScenarioTests/

samples/
  LocalSoftwareCompany/
  RemoteWorkerProvider/
  HumanCollaboration/
```

This may begin with fewer projects and split as boundaries stabilize.

## Major modules

### Companies

- Company setup
- Organizational units
- Membership and roles
- Company policies
- Knowledge and preferences

### Workforce

- Staff members
- Capabilities
- Role templates
- Teams and reporting relationships
- Responsibilities
- Availability and performance

### Orchestration

- Goals and projects
- Task graph
- Work routing
- Execution coordination
- Checkpoints and retries
- Events and escalation

### Budgeting

- Allocations
- Reservations
- Quotes
- Usage
- Charges
- Forecasts

### Approvals

- Authority policies
- Approval requests
- Decisions
- Expiration and escalation

### Artifacts

- Metadata
- Versioning
- Storage
- Validation
- Access control
- Retrieval indexing

### Agent runtime

- Microsoft Agent Framework adapter
- Agent and workflow factories
- Session management
- Context assembly
- Structured-output validation
- Runtime-event mapping

### Remote workers

- Provider descriptors
- Connections and authentication
- Quotes
- Remote execution
- Events and receipts
- Health and compatibility

### Human engagements

- Professional profiles
- Invitations and proposals
- Contracts
- Time and milestones
- Expenses
- Deliverables
- Payout-facing events

## Persistence

Initial recommendation:

- PostgreSQL: authoritative domain state, tasks, events, policies, budgets, sessions, and audit references
- S3-compatible object storage: artifacts
- pgvector or Qdrant: optional semantic retrieval
- Redis: optional cache, locking, and coordination; never authoritative business state
- Secret store: provider credentials and encryption keys

## Event model

Use application events to decouple modules while remaining in-process initially.

Examples:

- GoalCreated
- ProjectApproved
- TaskReady
- WorkerRequested
- WorkerAssigned
- ExecutionStarted
- ArtifactSubmitted
- ReviewRejected
- BudgetThresholdReached
- ApprovalRequested
- ProviderUnavailable
- EngagementAccepted

Persist important events into an activity and audit stream.

Not every domain event must become public integration infrastructure.

## Background processing

The worker process should handle:

- Ready-task scheduling
- Agent and provider execution
- Recurring responsibilities
- Workflow resumption
- Timeouts and retries
- Provider health checks
- Notifications
- Cost reconciliation
- Artifact processing

Use database-backed claims or leases so multiple worker instances do not execute the same task concurrently.

## Blazor applications

### CEO console

- Personal Assistant chat
- Executive inbox
- Company and org chart
- Staff and marketplace
- Projects and task graph
- Activity feed
- Artifacts
- Budgets
- Approvals
- Operations and traces

### Professional portal

May initially be part of the same Blazor application with a separate route area:

- Engagement invitations
- Assigned work
- Messages
- Files
- Time and expenses
- Deliverables
- Earnings status

## Real-time updates

Use SignalR initially for:

- Company activity
- Execution progress
- Approvals
- Messages
- Provider status
- Human task updates

Keep the event contract transport-neutral so SSE or AG-UI can be evaluated later.

## Model-provider abstraction

Users should configure named model profiles:

```text
Local Qwen Reasoning
Local Fast Utility
Hosted Premium Reasoning
Hosted Vision
```

A profile includes:

- Endpoint
- Model name
- Capabilities
- Context limits
- Pricing estimates
- Secret references
- Availability
- Supported structured output
- Tool-calling support

Workers reference profiles or requirements rather than hard-coded providers.

## Tool architecture

Suggested contracts:

```csharp
public interface IToolProvider;
public interface IToolExecutor;
public interface IToolAuthorizationService;
public interface IToolReceiptStore;
```

Tools must expose:

- Permission requirements
- Side-effect classification
- Idempotency behavior
- Cost estimation
- Result schemas
- Data scopes
- Cancellation behavior

## Plugin approach

Initial extension types:

- Data-driven role packs
- Workflow templates
- Local tool adapters
- MCP connections
- Remote workforce providers
- Model providers
- Artifact handlers
- Evaluation providers

Do not load arbitrary community DLLs into the web or worker processes.

Trusted in-process extensions can be considered later with signing, review, and explicit administrator installation.

## Testing strategy

### Unit tests

- Domain invariants
- Budget rules
- Approval policies
- Routing scoring
- State transitions

### Contract tests

- Worker descriptors
- Structured results
- Provider protocol
- Tool schemas

### Integration tests

- PostgreSQL persistence
- Artifact storage
- Model endpoint adapters
- MCP servers
- Provider mocks

### Scenario tests

Run complete company scenarios with deterministic or recorded model responses:

- Start a project
- Hire workers
- Delegate tasks
- Fail and retry
- Request approval
- Exceed budget
- Use a human professional
- Recover from provider outage
- Replace a worker
- Preserve artifacts

## Deployment

Initial self-hosted deployment should support Docker Compose with:

- Web
- Worker
- PostgreSQL
- Object storage
- Optional Redis
- Optional vector database

Model servers and MCP services may run locally or remotely and are configured through the application.
