# Budgeting, Authority, and Governance

## Purpose

Every type of labor and tool usage consumes scarce resources.

CSweet needs one budget and authority system that covers:

- Local compute
- Hosted models
- Remote worker charges
- Human labor
- Expenses
- Provider-defined usage meters
- Subscriptions
- Tool usage

## Budget hierarchy

Budgets may be attached to:

```text
Company
└── Department
    └── Team
        └── Project
            └── Worker
                └── Task or execution
```

An execution must satisfy every applicable limit.

The most restrictive rule wins.

Example:

```text
Company monthly budget:          $1,500
Engineering department:           $700
Game team:                         $300
Developer worker:                  $125
Current task maximum:               $15
```

## Budget dimensions

### Monetary

- Daily
- Weekly
- Monthly
- Per project
- Per task
- Per execution
- Per provider

### Model usage

- Input tokens
- Output tokens
- Total tokens
- Requests
- Context size
- Model-specific limits

### Tool and service usage

- Web searches
- Image generations
- Build minutes
- Storage
- API requests
- Premium database queries
- Transactions processed
- Human professional reviews

### Labor

- Hours
- Weekly capacity
- Fixed-price commitments
- Milestones
- Retainer consumption
- Expenses

## Thresholds

Each budget can define:

- Warning threshold
- Approval threshold
- Hard limit

Example:

```text
Monthly worker budget: $100
At $70: surface warning
At $90: require approval
At $100: block additional execution
```

## Reservation model

Before starting work:

1. Calculate or request a maximum estimated cost.
2. Check all applicable budget scopes.
3. Create an atomic reservation.
4. Execute the work.
5. Record actual usage.
6. Convert the reservation into charges.
7. Release unused capacity.

Reservations prevent concurrent workers from collectively overspending a shared budget.

## Quotes

Remote providers and human engagements may require quotes.

A quote should contain:

- Scope reference
- Currency
- Estimated cost
- Maximum cost
- Meter estimates
- Expiration
- Assumptions
- Provider or professional identity

The execution cannot exceed the authorized maximum without a new quote or change order.

## Charges and receipts

Actual usage may come from:

- Local model telemetry
- Hosted model invoices
- Remote-provider usage receipts
- Approved human time entries
- Accepted milestones
- Approved expenses
- Subscription allocations

Every charge should reference:

- Reservation
- Task
- Worker
- Organization unit
- Source receipt
- Currency
- Meter

## Internal ledger

Do not derive financial state solely from provider dashboards or payment webhooks.

Suggested records:

- BudgetAllocation
- BudgetReservation
- Quote
- SpendingAuthorization
- UsageEvent
- Charge
- Adjustment
- Credit
- Refund
- ExpenseClaim
- FundingAuthorization
- PayoutAllocation

A double-entry-style ledger should be evaluated before real marketplace funds are handled.

## Budget ownership

Every budget should have:

- Owner
- Approvers
- Escalation path
- Allowed categories
- Time period
- Rollover policy
- Overrun policy

Example escalation:

```text
Task exceeds worker limit
→ Team manager

Team exceeds monthly allocation
→ Department head

Department exceeds allocation
→ Personal Assistant and CEO
```

## Authority scopes

Authority should be explicit and separate from role titles.

Examples:

- Create tasks up to $10
- Hire free local workers
- Request marketplace proposals
- Approve remote data transmission below a sensitivity threshold
- Commit up to $100 per month
- Merge non-production branches
- Send external email drafts only after approval

## Approval classes

At minimum:

- Informational
- Reversible write
- External communication
- Data disclosure
- Financial commitment
- Hiring or contract
- Destructive action
- Production deployment
- Regulated filing
- Professional certification

## Delegated hiring

Policies may permit automatic staffing within limits:

- Approved role types
- Maximum monthly commitment
- Maximum organizational depth
- Maximum active workers
- Temporary-worker expiration
- Required credentials
- Allowed execution types

Agents should normally submit `HireRequest` records rather than creating unlimited workers directly.

## Change orders

Human and provider work may require scope changes.

A change order should include:

- Original scope
- Requested change
- Cost impact
- Schedule impact
- Reason
- New acceptance criteria
- Required approval

Existing work should pause when a required increase exceeds authorization.

## Forecasting

Managers should forecast:

- Remaining project cost
- Monthly recurring workforce commitments
- Expected model usage
- Human hours
- Provider subscriptions
- Risk-adjusted contingency

The Personal Assistant should surface projected overruns before hard limits are reached.

## Company policies

A company should be able to choose operating preferences such as:

- Cheapest capable workforce
- Fastest completion
- Highest expected quality
- Local-first
- Privacy-first
- Human-reviewed
- Balanced

These preferences guide recommendations but cannot override hard permissions, laws, credentials, or budgets.

## Budget reporting

The CEO view should answer:

- What have we spent?
- What is reserved?
- What recurring commitments exist?
- Which departments and workers are driving cost?
- What work is blocked by budget?
- What is the forecast to complete current goals?
- Which costs came from models, tools, providers, or people?
- Which workers are generating the most rework?

## Initial implementation guidance

For the prototype:

- Support money, tokens, and execution counts
- Support company, team, worker, project, and task scopes
- Implement warning, approval, and hard-limit thresholds
- Use atomic database reservations
- Simulate provider and human receipts before integrating real payments
