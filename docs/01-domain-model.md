# Core Domain Model

## Purpose

The domain model must describe a persistent company, not a collection of temporary agent chats. Microsoft Agent Framework and other runtimes execute work, but the CSweet domain remains authoritative.

## Top-level aggregates

### Company

The tenant and operating boundary.

Suggested fields:

- `CompanyId`
- Name and description
- Mission and operating preferences
- Default workforce-routing policy
- Default approval policy
- Default model/provider policy
- Company budget
- Data-classification policy
- Status and lifecycle dates
- Owner and administrators

A user may own or participate in multiple companies with isolated staff, knowledge, credentials, artifacts, and budgets.

### Organization Unit

Represents departments, divisions, teams, and temporary project groups.

- `OrganizationUnitId`
- Parent unit
- Type: Department, Division, Team, ProjectTeam
- Manager or accountable owner
- Budget
- Policies inherited from parent
- Members
- Active responsibilities

### Staff Member

A company-specific instance of a worker offering or included worker template.

- Identity and display name
- Worker execution type
- Role and capabilities
- Department and team memberships
- Supervisor
- Employment or engagement status
- Model profile or provider connection
- Permission grants
- Autonomy levels
- Worker and usage budgets
- Availability
- Performance metrics
- Marketplace or provider references

The staff member is not the same as a marketplace listing, human account, provider service, or agent session.

### Capability

A normalized unit of work a resource can perform.

Examples:

- `software.csharp`
- `software.code-review`
- `research.market-analysis`
- `finance.expense-categorization`
- `creative.product-photography`
- `professional.tax-review.us.ca`

Capabilities should support:

- Version
- Proficiency claim
- Verification status
- Jurisdiction
- Prerequisites
- Tool requirements
- Risk classification
- Expected output contracts

### Role Template

A reusable bundle of:

- Name
- Description
- Default instructions
- Capabilities
- Responsibilities
- Tool requirements
- Model requirements
- Permissions
- Delegation rules
- Evaluation criteria
- Recommended supervisor

Examples:

- Personal Assistant
- Project Manager
- Researcher
- Developer
- QA Engineer
- Editor
- Accountant
- Tax Professional
- Artist
- Product Manager

### Goal and Project

A goal captures an executive outcome. A project organizes the work needed to achieve it.

Goal fields:

- Desired outcome
- Background and motivation
- Constraints
- Success measures
- Owner
- Target date
- Budget envelope

Project fields:

- Goal reference
- Scope
- Status
- Project manager
- Task graph
- Team
- Risks
- Decisions
- Budget
- Artifacts
- Forecast

### Task

A durable, structured unit of work.

Recommended fields:

- Objective
- Background
- Inputs and authorized context
- Constraints
- Acceptance criteria
- Expected deliverables
- Required capabilities
- Assigned execution owner
- Review, decision, and accountable owners
- Parent task and dependencies
- Priority
- Due date
- Allowed tools and data scopes
- Budget and cost estimate
- Approval policy
- Attempt count
- Status

Suggested statuses:

- Proposed
- Ready
- Offered
- Assigned
- Accepted
- Running
- Blocked
- AwaitingInformation
- AwaitingDecision
- AwaitingApproval
- Reviewing
- RevisionRequested
- Completed
- Failed
- Cancelled

### Responsibility

A recurring obligation assigned to a staff member or team.

Examples:

- Categorize transactions daily
- Reconcile accounts monthly
- Review pull requests
- Produce weekly project status
- Monitor customer-support issues
- Review security alerts
- Prepare quarterly tax estimates

Responsibilities generate tasks or workflow runs according to schedules, triggers, or detected conditions.

### Artifact

A durable work product, not merely an agent response.

- Artifact type
- Name and version
- Storage location
- Content type
- Creator
- Related task and project
- Validation status
- Source references
- Metadata
- Access policy
- Ownership and license metadata

Examples:

- Documents
- Source code
- Git commits
- Test results
- Images
- Spreadsheets
- Reports
- Invoices
- Signed professional reviews
- Build packages

### Decision Request

A structured escalation to an authorized decision owner.

- Question
- Context
- Available options
- Recommendation
- Expected consequences
- Urgency
- Blocking tasks
- Requested by
- Resolution
- Rationale

### Issue and Risk

An issue is current. A risk is potential.

Both should capture:

- Severity
- Probability where appropriate
- Impact
- Owner
- Mitigation
- Affected tasks
- Escalation status
- Resolution

### Approval Request

Represents an action that may not proceed without authority.

Common approval classes:

- Reversible write
- External communication
- Financial commitment
- Data sharing
- Destructive operation
- Production deployment
- Hiring or engagement
- Professional filing or certification

### Work Execution

A durable execution record for any local agent, remote worker, human task, or hybrid service.

- Execution ID
- Task ID
- Worker and staff member
- Runtime type
- Started and completed timestamps
- Status and checkpoints
- Input-context manifest
- Tool calls or provider events
- Cost reservations and receipts
- Result envelope
- Failure and retry history
- Cancellation history

### Worker Result Envelope

Every execution type should normalize its outcome into a common envelope:

- Summary
- Status recommendation
- Confidence
- Assumptions
- Artifacts created
- Issues raised
- Decisions requested
- Tasks proposed
- Hire requests
- Usage receipt or time entry
- Recommended next action

The application validates the envelope and applies authorized state transitions.

## Marketplace concepts

These must remain distinct:

- **Publisher / Provider** — organization or person publishing offerings
- **Worker Offering** — marketplace product describing a role or service
- **Worker Version** — immutable version of a machine-executed offering
- **Listing** — public marketplace presentation
- **Entitlement** — right to use a commercial offering
- **Installation / Connection** — company configuration for the offering
- **Engagement** — mutual agreement to use a human professional or managed service
- **Staff Member** — company instance placed into the organization

## Human engagement entities

- `ProfessionalProfile`
- `ServiceOffering`
- `Proposal`
- `Engagement`
- `Contract`
- `Milestone`
- `TimeEntry`
- `ExpenseClaim`
- `DeliverableSubmission`
- `Dispute`
- `PayoutRecord`

## Budget entities

- `Budget`
- `BudgetAllocation`
- `BudgetReservation`
- `Quote`
- `SpendingAuthorization`
- `UsageEvent`
- `Charge`
- `Adjustment`
- `Refund`
- `Forecast`

## Memory boundaries

Separate memory into:

- Company knowledge
- Project knowledge
- Agent/session continuity
- Artifact retrieval index
- Immutable audit history

A universal company chat transcript should not become the memory system.

## Core invariants

1. Every executable task has an accountable owner.
2. A worker cannot exceed the most restrictive applicable permission or budget.
3. Marketplace removal never deletes company-owned history or artifacts.
4. Worker updates cannot silently add permissions.
5. Human professionals must accept engagements before receiving active staff status.
6. Remote providers receive only explicitly authorized task context.
7. Task completion requires acceptance criteria and/or review, not merely a successful model response.
8. Financial usage is recorded against both a reservation and an actual receipt or approved time entry.
9. A provider outage pauses remote work without corrupting task state.
10. Side-effecting retries must be idempotent or require manual review.
11. A role title alone does not imply verified professional credentials.
12. Data access is scoped to company, project, task, artifact, and operation.
