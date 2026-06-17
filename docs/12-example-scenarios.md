# Example End-to-End Scenarios

## Purpose

These scenarios should be used for product design, integration tests, demonstrations, and architecture validation.

They intentionally combine local agents, remote workers, humans, budgets, approvals, and durable execution.

## Scenario 1: Build a small puzzle game

CEO request:

> I want to create a small puzzle game and release it on itch.io.

Expected flow:

1. Personal Assistant creates a project proposal.
2. Required capabilities are identified:
   - Product planning
   - Game design
   - C# or engine development
   - Art
   - QA
   - Store publishing
3. Current staff is inspected.
4. Missing roles are proposed.
5. CEO approves team and budget.
6. Project Manager creates task graph.
7. Research and game design run concurrently.
8. Developer creates starter repository in sandbox.
9. Artist produces placeholders.
10. QA rejects incomplete build.
11. Developer revises.
12. Human player or reviewer is optionally hired for usability testing.
13. Personal Assistant reports deliverables, costs, risks, and next decisions.

Architecture validated:

- Local agent delegation
- Parallel work
- Artifacts
- Review loops
- Budget reservations
- Human escalation
- Restart recovery

## Scenario 2: Start a clothing company

CEO request:

> I would like to start a small clothing company focused on premium local production.

Capabilities:

- Market research
- Brand strategy
- Product design
- Pattern making
- Manufacturing sourcing
- Product photography
- Ecommerce
- Marketing
- Accounting
- Legal review

Possible workforce:

```text
Personal Assistant — local
├── Project Manager — local
├── Market Researcher — local
├── Brand Strategist — local or remote
├── Clothing Designer — human
├── Pattern Maker — human
├── Shopify Worker — remote provider
├── Product Photographer — human
├── Bookkeeper — remote provider
└── Attorney — human
```

Expected behavior:

- Agents complete research and preparation first
- Humans are hired only for physical, credentialed, or taste-sensitive work
- Shipping and photography logistics become human tasks
- Budgets are split by department and worker
- Provider and human data access is scoped
- CEO receives options rather than raw worker messages

## Scenario 3: Accounting and tax department

CEO request:

> Hire the help we need to manage expenses, keep the books current, and prepare for quarterly taxes.

Possible workforce:

```text
Finance Director — local agent
├── Bookkeeper — remote provider worker
├── Financial Analyst — local agent
└── CPA or tax professional — human or hybrid provider
```

Example provider:

A future Intuit provider might expose:

- Bookkeeper
- Accountant
- Tax Professional
- Payroll Specialist
- Expense Auditor

Provider-side implementation could use:

- Proprietary models
- Accounting rules
- QuickBooks data
- MCP servers
- Human professional review

Expected flow:

1. CEO approves provider connection.
2. Company grants selected financial scopes.
3. Bookkeeper categorizes transactions.
4. Local analyst prepares cash-flow forecast.
5. Tax worker identifies issues.
6. Human CPA reviews high-consequence recommendations.
7. CEO approves payments or filings.
8. Usage receipts and human charges are applied to department budgets.

Architecture validated:

- Provider workspace
- Remote worker quote and receipt
- Regulated human review
- Sensitive data scopes
- Recurring responsibilities
- Department budget

## Scenario 4: Build and launch a SaaS product

CEO request:

> Build a small SaaS product for property professionals and prepare it for a private beta.

Possible workforce:

- Product Manager — local
- Architect — local
- Developer — local
- QA — local
- Security Reviewer — remote or human
- Designer — remote or human
- Copy Editor — local
- Beta Recruiter — human or remote service

Expected flow:

- Product Manager converts outcome into requirements
- Architect proposes design
- CEO approves scope and architecture
- Developer works in sandbox
- QA validates builds
- Security review is required before deployment
- External communications require approval
- Beta feedback generates new tasks

Architecture validated:

- Git tooling
- Production-deployment approvals
- Artifacts and code
- Human and remote review
- Iterative project planning

## Scenario 5: Provider outage

A remote accounting worker becomes unavailable during monthly close.

Expected behavior:

1. Execution is marked `ProviderUnavailable`.
2. Task state remains intact.
3. No repeated billable calls occur.
4. Manager receives impact summary.
5. Workforce router suggests:
   - Wait
   - Assign compatible remote provider
   - Use local accounting agent
   - Hire human bookkeeper
6. CEO or delegated manager chooses.
7. Existing artifacts and history are preserved.

Architecture validated:

- Provider-health states
- Durable pause
- Workforce replacement
- Company-owned history

## Scenario 6: Budget pressure

The Game Development team has used 87% of its monthly budget.

Project Manager requests more work:

- Controller support
- Final QA
- Store packaging

Expected Personal Assistant summary:

```text
The Game Development team has used 87% of its monthly budget.

Requested additional work:
- Controller support
- Final QA
- Store packaging

Estimated cost: $75

Recommendation:
Approve $50 for controller support and QA.
Defer store packaging until next month.
```

Architecture validated:

- Forecasting
- Hierarchical budget checks
- Approval thresholds
- Executive decision packaging

## Scenario 7: Human professional uses company agents

A human accountant is engaged to review financial statements.

The accountant is authorized to:

- Ask document agent to organize receipts
- Ask local analyst to identify anomalies
- Ask research agent to summarize relevant public guidance

The accountant is not authorized to:

- Access unrelated projects
- Send payments
- Modify provider credentials
- Hire additional staff without approval

Architecture validated:

- Human-to-agent delegation
- Scoped access
- Mixed team hierarchy
- Credentialed accountability
