# Remote Workforce Provider Protocol

## Purpose

Commercial workers may be proprietary services that are unavailable when the self-hosted application cannot reach the provider.

CSweet therefore needs a standard protocol for discovering, quoting, invoking, monitoring, cancelling, and auditing remote work.

The protocol should be transport-neutral enough to support:

- HTTP APIs
- A2A-compatible services
- Provider-specific adapters
- Long-running asynchronous work
- Streaming or polling

## Provider responsibilities

The provider owns:

- Proprietary models, prompts, software, data, and MCP servers
- Service authentication
- Licensing and subscription enforcement
- Usage metering and authoritative billing
- Provider-side workspace state
- Availability and support
- Data retention and deletion behavior
- Internal human review where offered
- Provider-side professional credential management

CSweet owns:

- Company hierarchy
- Task definition
- Context authorization
- Budget approval and reservation
- Local audit history
- Result normalization
- Company artifacts and decisions
- Reassignment when the provider is unavailable

## Provider descriptor

A provider descriptor should include:

- Provider ID and name
- Protocol and endpoint
- Authentication methods
- Supported worker offerings
- Health and compatibility endpoints
- Data-processing disclosures
- Billing and quote capabilities
- Support and legal links
- Version information
- Provider workspace capabilities

## Worker offering descriptor

A remote offering should declare:

- Stable worker ID
- Role and capability claims
- Input and output schema versions
- Supported task types
- Required data scopes
- Optional provider workspace requirements
- Expected latency and long-running support
- Supported billing meters
- Credential claims
- Geographic or jurisdiction restrictions
- Cancellation and refund behavior
- Human-review availability

Descriptors installed into the self-hosted application are read-only representations of provider-owned offerings.

## Provider workspace

A provider may maintain shared state for several workers.

Example:

```text
Provider Workspace
├── Connected accounting company
├── Chart of accounts
├── Transactions and receipts
├── Payroll records
├── Tax profile
└── Filing history
```

CSweet stores only the connection metadata and references necessary for orchestration.

Raw credentials must not be placed into prompts or task messages.

## Suggested operations

- Discover provider metadata
- List worker offerings
- Retrieve current pricing
- Validate license or subscription
- Create a quote
- Start execution
- Stream or poll execution events
- Submit additional information
- Submit approval
- Cancel execution
- Retrieve final result
- Retrieve usage receipt
- Request provider-side data deletion
- Check health and compatibility

## Quote contract

Before billable work, CSweet may send:

```json
{
  "workerId": "com.example.tax-reviewer",
  "capability": "finance.quarterly-tax-review",
  "taskSummary": "Review Q2 records and estimate payments",
  "estimatedInputTokens": 18000,
  "requestedOutputTokens": 6000,
  "requestedMeters": {
    "professional.review": 1
  }
}
```

The provider should return:

```json
{
  "quoteId": "quote_83724",
  "currency": "USD",
  "estimatedCost": 120.00,
  "maximumCost": 150.00,
  "expiresAt": "2026-06-18T00:00:00Z",
  "pricingDescription": "Automated review plus one professional review"
}
```

The maximum amount is used for budget reservation.

Execution must not exceed it without a new authorization.

## Execution request

Recommended logical contract:

```csharp
public sealed record RemoteWorkerRequest(
    Guid ExecutionId,
    string WorkerId,
    string WorkerVersion,
    string Capability,
    WorkerTask Task,
    IReadOnlyList<AuthorizedContextItem> Context,
    IReadOnlyList<AuthorizedToolGrant> ToolGrants,
    SpendingAuthorization SpendingAuthorization,
    CallbackConfiguration? Callback);
```

The context list should contain explicit, scoped items rather than a broad project dump.

## Execution events

Long-running providers should publish normalized events:

- Accepted
- Queued
- Running
- ProgressUpdated
- InformationRequested
- ApprovalRequested
- ArtifactProduced
- MeterUpdated
- Blocked
- Completed
- Failed
- Cancelled

Provider-specific details can be retained in metadata while CSweet maps events into its company activity model.

## Result contract

```csharp
public sealed record RemoteWorkerResult(
    Guid ExecutionId,
    WorkerExecutionStatus Status,
    string Summary,
    decimal? Confidence,
    IReadOnlyList<ArtifactReference> Artifacts,
    IReadOnlyList<IssueReport> Issues,
    IReadOnlyList<DecisionRequest> Decisions,
    IReadOnlyList<TaskProposal> ProposedTasks,
    IReadOnlyList<HireRequest> HireRequests,
    UsageReceipt Usage);
```

## Usage receipt

A provider should return an immutable receipt containing:

- Execution and quote IDs
- Meter names and quantities
- Final cost and currency
- Provider receipt ID
- Timestamp
- Optional signature or verification data
- Adjustments or credits

The provider invoice remains authoritative, but CSweet records receipts for:

- Local budgeting
- Reconciliation
- Forecasting
- Dispute evidence
- Executive reporting

## Authentication

Potential modes:

- API key
- OAuth 2.0
- Signed request
- Mutual TLS
- Enterprise private network

Secrets should be stored through a dedicated secret provider and referenced by connection ID.

They should not be readable by ordinary agents.

## Data authorization

Every request must include only authorized data.

Allowed example:

- Project brief
- Approved requirements
- Selected artifact IDs
- Public brand guide

Denied example:

- Unrelated projects
- CEO private notes
- Credentials
- Payroll or financial records outside scope
- Other workers’ conversations

High-sensitivity policies may require approval for every remote transmission.

## Availability and failure states

Remote staff may enter:

- Available
- Degraded
- RateLimited
- AuthenticationRequired
- LicenseExpired
- BudgetBlocked
- ProviderUnavailable
- VersionUnsupported
- Disabled

When unavailable, assigned work should pause without losing state.

The manager should receive:

- Impact
- Blocked tasks
- Expected delay
- Suggested alternatives
- Replacement recommendations

## Retries

Do not retry paid or side-effecting remote work indefinitely.

Retry behavior must consider:

- Whether the provider accepted the execution
- Idempotency key
- Whether a quote expired
- Whether usage was already incurred
- Cancellation state
- Provider retry guidance

## Provider SDK

Potential packages:

```text
CSweet.RemoteWorkers.Contracts
CSweet.RemoteWorkers.AspNetCore
CSweet.RemoteWorkers.Client
CSweet.RemoteWorkers.TestKit
CSweet.RemoteWorkers.Certification
```

The SDK should make it easy for a provider to implement:

- Discovery
- Quotes
- Execution
- Events
- Cancellation
- Receipts
- Health
- Data deletion

without adopting CSweet’s internal architecture.

## Compatibility and evolution

All descriptors and messages need explicit schema versions.

Providers and hosts should negotiate supported versions and degrade gracefully when optional capabilities are missing.
