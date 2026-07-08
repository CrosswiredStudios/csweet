# System Boundaries and Deployment Model

## Purpose

This document captures the high-level architecture decisions for CSweet after clarifying the relationship between the self-hosted core application, the live marketplace, worker execution, persistence boundaries, and multi-company support.

The key design goal is simple:

> A user should be able to clone the CSweet core repository, deploy it, configure models and tools, and have everything needed to operate one or more companies without depending on the official marketplace.

The marketplace is an optional live network that extends the system with commercial agents, human professionals, hybrid services, payments, reputation, discovery, and publishing workflows.

## Product boundary

CSweet should be treated as two products with shared contracts, not one product split across shared tables.

```text
┌───────────────────────────────────────────────┐
│             Self-Hosted CSweet Core            │
│                                               │
│  Open-source company operating system          │
│  Blazor WASM company interface                 │
│  ASP.NET Core API                              │
│  Job orchestration                             │
│  Worker Host                                   │
│  Local worker registry                         │
│  Company data                                  │
│  Authentication and permissions                │
│  PostgreSQL                                    │
│  Artifact storage                              │
│  Local or hosted model configuration           │
│  MCP and external tool integrations            │
│                                               │
│  Fully useful without the marketplace          │
└───────────────────────┬───────────────────────┘
                        │ Optional HTTPS/OIDC/API/events
                        ▼
┌───────────────────────────────────────────────┐
│             Live CSweet Marketplace            │
│                                               │
│  Fiverr/Upwork-style labor platform            │
│  Human worker registration                     │
│  Agent worker registration                     │
│  Publisher and provider portals                │
│  Search, discovery, matching                   │
│  Pricing, contracts, and milestones            │
│  Billing, payments, and payouts                │
│  Messaging for marketplace engagements         │
│  Reviews and reputation                        │
│  Disputes, moderation, and trust operations    │
└───────────────────────────────────────────────┘
```

## Core is complete on its own

The core repository must include everything required to launch an independent CSweet installation.

A cloned deployment should support:

- Creating and managing one or more companies.
- Managing users, company memberships, roles, and permissions.
- Configuring local or hosted OpenAI-compatible model endpoints.
- Configuring MCP servers and direct tool integrations.
- Installing or defining local workers.
- Running jobs and long-running workflows.
- Storing company history, artifacts, decisions, approvals, and audit records.
- Operating without an official marketplace account.

The marketplace must enhance the core, not activate it.

## Worker Host terminology

Earlier discussions used the phrase `Worker Runtime`. The preferred name is **Worker Host**.

The Worker Host is not a separate hosted product. It is the part of the self-hosted core installation responsible for executing work.

It may initially run as a background service inside the main ASP.NET Core process. Later, it may be split into a separate process or container for isolation and scale.

```text
CSweet Core
├── Blazor WASM UI
├── ASP.NET Core API
├── Company and user management
├── Job orchestration
├── Worker management
└── Worker Host
    ├── Local agent execution
    ├── Local LLM calls
    ├── MCP tool calls
    ├── Remote provider calls
    ├── Marketplace engagement synchronization
    └── Human-work task synchronization
```

The initial deployment may expose it as:

```text
csweet-web-api
csweet-worker-host
postgres
object-storage
optional-redis
optional-queue
```

But this is still one self-hosted CSweet Core product.

## No required official hosted CSweet environment

The architecture should not assume an official hosted CSweet SaaS product.

A hosted CSweet offering could exist in the future, but it is not part of the core architecture. The primary product is the open-source, self-hostable core.

The only official live service required for the platform concept is the optional marketplace.

## Live marketplace model

The marketplace is a live multi-sided labor platform similar to Fiverr or Upwork, but designed for both humans and agents.

Participants may register themselves or their agents as workers.

Marketplace users include:

- Companies looking for additional workforce capacity.
- Human professionals offering services.
- Individual agent developers.
- Software vendors publishing commercial agents.
- Agencies offering hybrid human-and-agent services.
- Marketplace administrators handling moderation, disputes, and verification.

The marketplace should support:

- Public listings.
- Worker search and discovery.
- Provider onboarding.
- Human professional profiles.
- Agent publishing and versioning.
- Pricing plans.
- Job proposals.
- Contracts and milestones.
- Messaging.
- Payments and payouts.
- Reviews and reputation.
- Trust, safety, and dispute resolution.

## Marketplace worker types

A marketplace worker may be human, agentic, or hybrid.

```csharp
public enum MarketplaceWorkerType
{
    Human,
    Agent,
    Hybrid
}
```

Suggested execution modes:

```csharp
public enum MarketplaceExecutionMode
{
    HumanEngagement,
    ProviderHostedAgent,
    LocallyInstalledAgent,
    MarketplaceHostedAgent,
    HybridService
}
```

Suggested pricing models:

```csharp
public enum PricingModel
{
    Free,
    Hourly,
    FixedPrice,
    PerJob,
    PerUsage,
    Subscription,
    Milestone,
    Retainer,
    CustomQuote
}
```

Examples:

- A CPA charging hourly.
- A bookkeeper offering a fixed-price monthly reconciliation package.
- A software vendor offering an accounting agent backed by proprietary models and APIs.
- An open-source research agent that can be installed locally.
- A hybrid tax-preparation service where an AI prepares the draft and a licensed professional reviews it.

## Agent delivery models

Agent workers can be delivered several ways.

### Local or installable agent

The marketplace or another source provides a signed worker package or manifest. The worker runs inside the self-hosted core installation.

```text
Marketplace or registry
    → signed worker package
        → CSweet Core installs worker
            → Worker Host executes locally
```

This is best for open-source workers, privacy-sensitive workloads, and local LLM usage.

### Provider-hosted agent

The publisher runs the agent on its own infrastructure.

```text
CSweet Core
    → provider adapter
        → publisher API / proprietary model / proprietary MCP server
```

The core still owns the job, permissions, approval flow, local audit trail, and result storage. The provider only performs the authorized specialized operation.

### Marketplace-hosted or marketplace-relayed agent

The marketplace may host or relay execution for some agents.

```text
CSweet Core
    → CSweet Marketplace
        → agent execution
```

This can simplify billing and access control, but it may send job data through the marketplace. It should not be required for all agents and should be clearly disclosed.

### Human engagement

A human professional accepts a scoped engagement through the marketplace.

```text
CSweet Core
    → marketplace engagement
        → human professional accepts
            → scoped workspace and permissions
                → deliverables return to Core
```

The marketplace manages identity, availability, pricing, contracts, messaging, payment, reputation, and disputes. The core manages company-side authorization and internal records.

## One installation can manage multiple companies

CSweet Core must not assume one installation equals one company.

The correct model is:

```text
One CSweet Core instance = one or more companies
```

A single installation may be used by:

- An entrepreneur managing multiple LLCs.
- A holding company managing subsidiaries.
- An accountant or agency managing client companies.
- A consultant operating CSweet for several businesses.
- A family office managing multiple entities.

Suggested structure:

```text
CSweet Core Instance
├── Instance-level users and settings
├── Marketplace connection
├── Model providers
├── Installed worker packages
├── Companies
│   ├── Company A
│   │   ├── Members and roles
│   │   ├── Workers
│   │   ├── Jobs
│   │   ├── Documents
│   │   ├── Conversations
│   │   ├── Credentials
│   │   ├── Approvals
│   │   └── Marketplace engagements
│   └── Company B
│       ├── Members and roles
│       ├── Workers
│       ├── Jobs
│       ├── Documents
│       ├── Conversations
│       ├── Credentials
│       ├── Approvals
│       └── Marketplace engagements
└── System health and audit records
```

Most business records should be company-scoped.

```text
CompanyId should appear on:
- Jobs
- Tasks
- Documents
- Conversations
- Messages
- Credentials
- Approvals
- Worker configurations
- Marketplace engagements
- Audit events
- Budgets
- Artifacts
```

Instance-level records include:

```text
- Users
- Companies
- Installed worker packages
- Marketplace connection
- Model provider definitions
- System settings
- Instance audit events
```

## User and membership model

A user may belong to multiple companies with different permissions.

```text
User
├── Instance role
│   └── InstanceAdmin
└── Company memberships
    ├── Company A: Owner
    ├── Company B: CFO
    └── Company C: Advisor
```

Permissions should be scoped by company, role, capability, and operation.

```text
Permission = User + Company + Role + Capability + Scope
```

A marketplace worker or human professional hired for one company must not automatically gain access to another company in the same CSweet instance.

## Worker installation and activation

Worker availability should have two levels.

### Instance-level installation

The CSweet instance knows a worker package exists.

```text
InstalledWorkerPackage
├── WorkerId
├── Version
├── Source
├── ExecutionMode
├── PackageLocation
├── SignatureStatus
└── InstalledAt
```

### Company-level activation

Each company chooses whether to enable that worker and with what configuration.

```text
CompanyWorker
├── CompanyId
├── WorkerId
├── Enabled
├── Configuration
├── Permissions
├── ModelProfile
├── SecretReferences
├── BudgetLimits
└── PolicyOverrides
```

Example:

```text
QuickBooks Agent is installed once on the CSweet instance.

Company A enables it with Company A credentials.
Company B does not enable it.
Company C enables it with different credentials and tighter budget limits.
```

## Persistence boundary

The core and marketplace must not share a physical database.

The core database belongs to the self-hosted user or organization.

```text
Core database
├── Users
├── Companies
├── Memberships
├── Workers
├── Company worker configurations
├── Jobs and tasks
├── Conversations and messages
├── Documents and artifacts
├── Credentials and secret references
├── Approvals
├── Budgets
├── Local audit history
├── Marketplace connection references
└── Marketplace engagement references
```

The marketplace database belongs to the live marketplace operator.

```text
Marketplace database
├── Accounts
├── Provider profiles
├── Human professional profiles
├── Agent publisher profiles
├── Worker listings
├── Worker versions
├── Pricing plans
├── Availability
├── Orders
├── Contracts
├── Milestones
├── Messages
├── Payments and payouts
├── Reviews
├── Disputes
├── Moderation cases
└── Entitlements
```

They share contracts and identifiers, not tables.

Examples of shared identifiers:

```text
MarketplaceAccountId
MarketplaceOrganizationId
MarketplaceWorkerId
MarketplaceListingId
MarketplaceEngagementId
MarketplaceContractId
MarketplaceEntitlementId
CoreInstanceId
LocalCompanyId
LocalJobId
```

## Marketplace connection

A core instance should connect to the marketplace through a scoped authorization flow, not by sharing a database or requiring direct database credentials.

Suggested flow:

1. Instance admin clicks **Connect to CSweet Marketplace**.
2. Core opens the marketplace login and authorization page.
3. User signs in to the marketplace.
4. User selects the marketplace organization to connect.
5. Marketplace authorizes the specific core instance.
6. Core receives scoped credentials.
7. The instance can synchronize entitlements, listings, engagements, and marketplace messages.

The connection should be revocable.

Engagements and entitlements should be company-scoped inside the core.

```text
MarketplaceEngagementReference
├── MarketplaceEngagementId
├── LocalCompanyId
├── WorkerListingId
├── Status
├── Budget
├── PermissionsGranted
├── LinkedJobId
└── LastSynchronizedAt
```

## Job origin and synchronization

Jobs can originate in either system.

### Core-originated staffing

```text
CEO or assistant requests external help
    → Core searches marketplace
        → User approves provider and budget
            → Marketplace creates engagement
                → Engagement syncs into company workspace
```

### Marketplace-originated hiring

```text
Buyer hires worker directly on marketplace
    → Marketplace creates engagement
        → Connected Core instance imports engagement
            → Company workspace tracks work and deliverables
```

### Open marketplace job posting

```text
Company posts job
    → humans, agents, or hybrid providers submit proposals
        → company accepts proposal
            → engagement syncs into Core
```

## Messaging boundary

There should be two message domains.

### Marketplace messages

Marketplace messages support commercial engagements:

- Proposals.
- Scope negotiation.
- Pricing.
- Milestones.
- Support.
- Disputes.
- Deliverable submission.

These records belong to the marketplace because they may be needed for payments, moderation, and dispute resolution.

### Core messages

Core messages are internal company records:

- CEO and assistant conversations.
- Internal executive discussion.
- Private company documents.
- Approval reasoning.
- Internal planning.
- Local worker traces.

These records belong to the core and should not automatically sync to the marketplace.

The user or an authorized worker may explicitly share selected context with a marketplace engagement.

## UI boundaries

### Core web app

The core UI should be a Blazor WASM company console.

Primary areas:

- Company switcher.
- CEO / assistant interface.
- Executive inbox.
- Workers and staff.
- Jobs and projects.
- Documents and artifacts.
- Approvals.
- Budgets.
- Credentials and integrations.
- Marketplace engagements.
- Instance settings.

A company switcher is required.

```text
[ Acme Plumbing ▼ ]

Switch to:
- Acme Plumbing
- Wood Holdings LLC
- Example SaaS Co.
- Add company
```

Once a company is selected, all business screens should be scoped to that company.

Instance admins should have a separate system area for:

- Users.
- Companies.
- LLM providers.
- Storage.
- Marketplace connection.
- Installed worker packages.
- System health.

### Marketplace web app

The marketplace should be a live web app with role-based areas.

```text
marketplace.csweet.com/
    Public marketplace and listings

marketplace.csweet.com/buy/
    Buyer dashboard

marketplace.csweet.com/sell/
    Human worker and provider dashboard

marketplace.csweet.com/publish/
    Agent publishing and version management

marketplace.csweet.com/admin/
    Marketplace operations, moderation, and disputes
```

These can begin as route areas inside one application and split later if needed.

## Recommended technology choices

### CSweet Core

- .NET as the primary application platform.
- Blazor WASM for the self-hosted company interface.
- ASP.NET Core API / backend-for-frontend.
- Modular monolith to start.
- PostgreSQL for authoritative state.
- S3-compatible object storage for artifacts.
- Redis only for cache, locking, coordination, or ephemeral state.
- Optional queue when separate worker-host processes are introduced.
- SignalR for live updates.
- OpenTelemetry for observability.
- Local or hosted OpenAI-compatible model endpoints.
- MCP for tool and server integrations.
- Microsoft Agent Framework can be used behind application-owned abstractions.

### CSweet Marketplace

- Separate live web application and API.
- Separate marketplace PostgreSQL database.
- Role-based areas for buyers, sellers, publishers, and admins.
- Payment and payout integration.
- Search and filtering for human and agent workers.
- Marketplace-owned messaging, contracts, reputation, moderation, and dispute records.
- Public listing pages suitable for discovery.

### Shared packages

The products may share packages for contracts and SDKs.

```text
CSweet.Contracts
CSweet.Worker.Contracts
CSweet.WorkerSdk
CSweet.Marketplace.Contracts
CSweet.Marketplace.Client
CSweet.UI.Components
```

They should not share EF Core entities, migrations, or database tables.

## Suggested repository shape

The initial open-source core can remain one repository.

```text
csweet-core/
├── src/
│   ├── CSweet.Web
│   ├── CSweet.Api
│   ├── CSweet.Application
│   ├── CSweet.Domain
│   ├── CSweet.Infrastructure
│   ├── CSweet.WorkerHost
│   ├── CSweet.WorkerSdk
│   └── CSweet.Marketplace.Client
├── workers/
│   ├── ceo
│   ├── chief-of-staff
│   ├── cfo
│   ├── accountant
│   └── research-assistant
├── docker-compose.yml
└── docs/
```

The marketplace may eventually live in a separate repository.

```text
csweet-marketplace/
├── src/
│   ├── CSweet.Marketplace.Web
│   ├── CSweet.Marketplace.Api
│   ├── CSweet.Marketplace.Identity
│   ├── CSweet.Marketplace.Catalog
│   ├── CSweet.Marketplace.Engagements
│   ├── CSweet.Marketplace.Messaging
│   ├── CSweet.Marketplace.Payments
│   ├── CSweet.Marketplace.Reputation
│   └── CSweet.Marketplace.Infrastructure
└── docs/
```

## Architectural decisions to preserve

1. The open-source core is the main product.
2. The core must be cloneable, deployable, and useful without the official marketplace.
3. The marketplace is a live Fiverr/Upwork-style platform for human, agent, and hybrid workers.
4. The marketplace is optional for core operation but central to workforce discovery, commerce, reputation, and human engagements.
5. The core and marketplace share contracts and identifiers, not persistence.
6. A single core instance can manage multiple companies.
7. Most business records are company-scoped.
8. Worker packages are installed at the instance level and activated/configured at the company level.
9. The preferred name for the execution subsystem is Worker Host.
10. Local agents, remote commercial agents, human professionals, and hybrid services should fit into one workforce model.
11. Company data should remain in the core unless explicitly shared with a marketplace engagement or remote provider.
12. Human professionals and external workers receive scoped access only for the company and job they are engaged to support.

## Product definition

CSweet Core:

> An open-source, self-hosted operating system for managing one or more companies with AI workers, human workers, local tools, and marketplace-provided capabilities.

CSweet Marketplace:

> A live labor marketplace where humans, agent developers, software vendors, and service providers can register as workers that CSweet companies can hire.
