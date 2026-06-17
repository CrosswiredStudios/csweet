# Open Questions and Decision Log

## Purpose

This document tracks decisions that must be resolved through prototypes, research, legal review, or product testing.

It should be updated as decisions are made rather than allowing important assumptions to remain buried in conversations.

## P0: Core product and execution

### Autonomy boundary

- Which actions may included workers perform automatically?
- Which actions always require approval?
- Can the CEO delegate approval authority to managers by amount, capability, and risk class?
- How does authority expire or get revoked?

### Task contract

- What minimum fields are required before a task becomes ready?
- How are acceptance criteria represented and evaluated?
- Who may declare completion?
- How are partial success and abandoned work represented?

### Responsibility hierarchy

- When does responsibility remain with the delegating manager?
- When is a true handoff allowed?
- Can an accountable owner be a remote provider, or must it always be a company staff member?

### Durable execution

- Which MAF durability features are mature enough for the first implementation?
- What application state must be persisted independently?
- How are model calls and side effects resumed safely?
- What is the lease and concurrency model for multiple worker processes?

### Workforce routing

- What scoring model chooses local agent, remote worker, human, or hybrid service?
- How should quality and confidence be estimated before execution?
- How does the router learn from company-specific history without creating opaque decisions?

### Artifact validation

- What proves that a task was actually completed?
- Which artifact types can be machine-validated?
- When is independent review mandatory?

## P0: Security and trust

### Tool authorization

- What permission vocabulary is needed initially?
- How are MCP tools wrapped with application-enforced authorization?
- How are side-effect classes and idempotency declared?

### Data classification

- Which default classifications ship with the application?
- How are remote transmissions approved and audited?
- Can providers request data incrementally rather than receiving a full task package?

### Plugin boundary

- Are initial local extensions limited to data and declarative workflows?
- When, if ever, are signed in-process .NET plugins allowed?
- What sandbox technology will be used for generated code and container workers?

### Secrets

- Which self-hosted secret stores are supported first?
- How are secret references backed up and migrated?
- How are provider credentials scoped to tools and companies?

## P0: Marketplace and remote providers

### Marketplace dependency

Decision direction: the official marketplace is optional. Direct manifests and private registries should be supported.

Still unresolved:

- What metadata is cached locally?
- How are delisting and emergency warnings distributed to disconnected installations?

### Provider protocol

- Adopt A2A directly, define a CSweet protocol, or support both through adapters?
- How are long-running callbacks authenticated?
- Should usage receipts be signed?
- How are quote and execution disputes reconciled?

### Commercial enforcement

Decision direction: proprietary paid workers execute on provider infrastructure. They are expected to be unavailable without provider connectivity.

Still unresolved:

- Are any paid downloadable local workers supported?
- Is the official marketplace involved in billing, or may providers bill directly?
- What marketplace fee model is appropriate?

### Provider workspace

- What data remains provider-side?
- What portable export is required when switching providers?
- Can several provider workers share one connection safely?

## P0: Human workforce

### Engagement legal model

- Which countries or states are supported first?
- How does the platform distinguish contractors, agencies, managed services, and employees?
- Is an employer-of-record integration needed later?

### Payments

- Fixed-price milestones first or hourly first?
- Does the platform hold funds, authorize cards, or only record external agreements?
- How are refunds, chargebacks, and disputes handled?
- What internal ledger design is required?

### Credentials

- Which credentials can be verified automatically?
- Who is liable for false professional claims?
- How are jurisdiction and expiration checked before assignment?

### Human availability

- How are calendars, response times, working hours, and time zones modeled?
- Can an AI manager schedule a human without direct confirmation?
- What notification channels are required?

## P1: Budgeting

- Which budget scopes are required in the MVP?
- Are monetary estimates optional for local compute?
- How are currencies converted?
- Do unused allocations roll over?
- How are recurring subscriptions forecast?
- How are reservations reconciled after crashes?
- How are provider-defined functionality meters displayed consistently?

## P1: Models and memory

### Model profiles

- Which provider protocols are supported initially?
- How are model capabilities tested rather than merely declared?
- How should model routing account for local hardware and cost?

### Memory

- What belongs in company memory versus project memory?
- Which information is summarized, embedded, or stored verbatim?
- How are stale facts corrected?
- How can users inspect and delete remembered information?

### Context packaging

- How are large projects reduced to task-specific context?
- What context manifest is sufficient for audit and replay?
- How are provider token limits handled?

## P1: User experience

### CEO experience

- Should the interface feel more like a business application, simulation game, or selectable mode?
- How much internal worker activity is visible by default?
- How are recommendations distinguished from approved actions?

### Org chart

- Can one worker belong to multiple teams?
- How are temporary project teams represented?
- How are remote provider outages and human availability displayed?

### Executive inbox

- What constitutes a decision, approval, issue, warning, or FYI?
- How are related requests grouped?
- Can the CEO issue standing policies from an inbox decision?

### Professional experience

- Is the professional portal part of the same Blazor application?
- How do professionals separate work for multiple companies?
- What context is available before accepting an engagement?

## P1: Open-source governance

- Which license should the core use?
- Is the official marketplace implementation open source, closed source, or open core?
- How are community worker packs reviewed and distributed?
- What compatibility and deprecation policy will the SDK use?
- How are security disclosures handled?
- What project name is used publicly after trademark review?

## P2: Simulation and progression

- Should agents have simulated salaries separate from real costs?
- Are morale, leveling, performance reviews, and company progression desirable?
- How are game mechanics prevented from obscuring real financial commitments?
- Can the simulation layer be optional?

## P2: Reputation and learning

- Which performance metrics are fair across task difficulty?
- How does a company-specific rating differ from public marketplace reputation?
- Can routing learn automatically while remaining explainable?
- How are manipulated reviews and artificial task histories detected?

## Current decision summary

The following directions are considered agreed unless later revised:

1. The core application and Blazor interface are open source and self-hostable.
2. Users may point workers at local or hosted LLMs.
3. Included local workers can operate offline when their dependencies are local.
4. The system is agent-first but supports humans where needed.
5. Local agents, remote agents, humans, and hybrid services share one company workforce model.
6. Proprietary commercial workers execute on provider infrastructure and require provider connectivity.
7. Providers may define one-time, subscription, token, functionality, or other usage pricing.
8. Human professionals may join the marketplace and set their own rates.
9. Budgets apply hierarchically to companies, departments, teams, projects, workers, and tasks.
10. The application owns durable company state, artifacts, decisions, permissions, and audit history.
11. The official marketplace is optional for self-hosted operation.
12. Arbitrary untrusted in-process plugins are not part of the initial design.

## Decision-record template

```text
Decision:
Date:
Status: Proposed | Accepted | Superseded
Context:
Options considered:
Chosen direction:
Consequences:
Follow-up work:
```
