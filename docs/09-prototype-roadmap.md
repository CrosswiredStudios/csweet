# Prototype Roadmap

## Goal

The prototype should prove that CSweet can operate a durable mixed workforce, not merely demonstrate several agents chatting.

The first end-to-end scenario should exercise:

- Executive intent
- Staffing
- Task decomposition
- Local agent execution
- Remote worker simulation
- Human escalation
- Budgets
- Approvals
- Artifacts
- Review
- Restart recovery

## Recommended demonstration

CEO request:

> I want to create a small puzzle game and release it on itch.io.

The system should:

1. Create a project proposal.
2. Identify required capabilities.
3. Recommend a Project Manager, Game Designer, Developer, Artist, QA worker, and optional human reviewer.
4. Request staffing and budget approval.
5. Build a task graph.
6. Run research and design in parallel.
7. Create a starter repository in a sandbox.
8. Produce design and implementation artifacts.
9. Route work through QA.
10. Detect a missing capability and request an additional worker.
11. Pause for a CEO decision.
12. Survive a worker-process restart.
13. Resume without repeating completed side effects.
14. Present an executive report with costs, artifacts, risks, and decisions.

## Phase 0: Technical spikes

### MAF delegation spike

- Personal Assistant receives a goal
- Project Manager decomposes it
- Specialists complete bounded tasks
- Results return through the hierarchy
- Only the Personal Assistant communicates the final executive summary

### Structured-output spike

- Define the worker result envelope
- Validate malformed and contradictory responses
- Persist proposed tasks, issues, decisions, artifacts, and usage

### Streaming spike

- Map runtime events to application events
- Stream meaningful activity into a minimal Blazor page

### Approval spike

- Require approval before a side-effecting action
- Persist the wait
- Resume after approval

### Restart spike

- Stop execution mid-workflow
- Restart the worker process
- Resume without duplicating completed work

### MCP spike

- Connect one read-only MCP server
- Connect one controlled write-capable MCP server
- Enforce tool grants outside the prompt

### Remote worker spike

- Implement a fake provider
- Retrieve an offering descriptor and quote
- Reserve budget
- Start and monitor work
- Record a usage receipt
- Simulate provider outage

### Human collaboration spike

- Invite a real user by email or development account
- Accept an engagement
- Assign a task
- Submit a deliverable
- Request revision
- Complete the task

## Phase 1: Company OS skeleton

Implement:

- Companies
- Organization units
- Staff members
- Role templates
- Capabilities
- Goals
- Projects
- Tasks and dependencies
- Responsibilities
- Artifacts
- Issues
- Decisions
- Approvals
- Execution records
- Activity events

Use deterministic fake workers initially so domain behavior can be tested without model variability.

## Phase 2: Local agent runtime

- Microsoft Agent Framework adapter
- Model profiles
- Personal Assistant
- Project Manager
- Researcher
- Developer
- QA Engineer
- Context assembler
- Structured results
- Agent sessions
- Runtime-event mapping

## Phase 3: Budget and authority

- Money and token budgets
- Company, team, project, worker, and task scopes
- Warning, approval, and hard thresholds
- Atomic reservations
- Usage records
- Approval workflow
- Hiring limits

## Phase 4: Blazor CEO console

Implement:

- CEO chat
- Executive inbox
- Company setup
- Org chart
- Staff directory
- Projects and tasks
- Activity feed
- Artifact browser
- Budget dashboard
- Approval and decision views
- Execution details

## Phase 5: Real tool execution

Add:

- Sandboxed filesystem and shell
- Git operations
- Test execution
- Web research
- Document generation
- One useful MCP integration
- Idempotency receipts

## Phase 6: Remote provider support

- Provider descriptor
- Authentication connection
- Worker offerings
- Quotes
- Execution and events
- Cancellation
- Usage receipts
- Provider health
- Data-disclosure UI

Create an example provider SDK project and reference implementation.

## Phase 7: Human collaboration

First release without payments:

- Professional profile
- Invitation
- Proposal and acceptance
- Staff membership
- Task assignment
- Messaging
- Deliverable submission
- Review and revision
- Access expiration

Then add fixed-price milestones before hourly billing.

## Phase 8: Marketplace prototype

Publish four demonstration offerings:

1. Included Project Manager — local and free
2. Community Unity Developer — local and free
3. Premium Market Researcher — simulated remote subscription
4. Human Product Reviewer — fixed-price simulated engagement

Demonstrate:

- Listing and search
- Installation or invitation
- Permission review
- Entitlement or engagement
- Company staffing
- Worker execution
- Version update
- Permission change
- License or provider unavailability
- Worker replacement without loss of history

## Phase 9: Production marketplace systems

Only after contracts stabilize:

- Publisher onboarding
- Identity verification
- Payments and payouts
- Internal ledger
- Reviews
- Credential verification
- Moderation
- Disputes
- Tax and legal review
- Provider certification

## Prototype success criteria

The prototype succeeds when:

- The CEO can express an outcome without manually creating every task
- The system creates a comprehensible workforce and task plan
- Work is routed across at least two execution types
- All task changes are represented in durable domain state
- A workflow survives restart
- Side effects are not duplicated
- Budgets prevent unauthorized execution
- The CEO receives useful decision summaries
- Artifacts can be inspected independently of chat history
- A worker can be replaced without losing project history

## Avoid during early implementation

- Full microservice decomposition
- Arbitrary in-process third-party plugins
- Complex marketplace payments
- Too many included worker roles
- Unbounded autonomous hiring
- Public performance scoring before metrics are trustworthy
- Relying on chat transcripts as state
- Building simulation mechanics before execution is reliable

## First implementation backlog

1. Create solution and projects
2. Define core IDs, entities, and state transitions
3. Add PostgreSQL persistence
4. Implement task graph and activity events
5. Implement fake worker adapter
6. Build minimal Blazor company and project views
7. Add MAF runtime adapter
8. Add Personal Assistant and Project Manager
9. Add structured worker results
10. Add approvals and budget reservations
11. Add sandboxed developer tool
12. Add restart and recovery integration test
