# 00 - Architecture Baseline

## Product summary

C-Sweet is a self-hostable executive operating system for small businesses and solo founders. It lets a user define a company, create executive roles, assign goals and tasks, run AI or human workers, and review the artifacts produced by those workers.

The long-term marketplace will work like an agentic-first version of Fiverr or Upwork: C-Sweet can assign work to AI agents by default, and supplement with humans or proprietary third-party services when AI is not enough.

## Guiding principles

### 1. Self-hosted core first

A user should be able to clone the core project, run it locally or on their own server, connect it to their preferred LLM provider, and use the core system without needing the hosted marketplace.

The hosted marketplace is a separate product surface and should not be required for the first useful version.

### 2. Docker-first distribution

C-Sweet should support Docker containerization out of the box. A non-developer user should be able to run the platform with Docker Compose without installing the .NET SDK or manually configuring Postgres, the API, the app, and the worker host.

The default user-facing startup path should be:

```bash
cp .env.example .env
docker compose up -d
```

Aspire remains the developer orchestration path, but Docker Compose is the first self-hosted distribution path.

### 3. Configuration before business onboarding

The user cannot create a useful business automation environment until the system knows how to call an LLM. Therefore, first-run system onboarding must come before business onboarding.

The first-run setup flow must configure:

- Admin or owner identity.
- Default chat model provider.
- Optional default embedding model provider.
- Provider capabilities.
- Storage/database status.
- Local worker runtime status.

### 4. Provider abstraction over provider lock-in

The local default is LM Studio, but the architecture must not hard-code LM Studio. Treat LM Studio as one OpenAI-compatible provider profile.

The system should eventually support:

- LM Studio
- OpenAI-compatible local servers
- vLLM OpenAI server
- llama.cpp / llama-server OpenAI-compatible endpoints
- Ollama
- OpenAI
- Azure OpenAI
- Anthropic
- Microsoft Foundry
- Custom HTTP providers

### 5. Microsoft ecosystem preference

Prefer enterprise-ready Microsoft/.NET libraries when they fit:

- ASP.NET Core for APIs.
- Blazor WASM for the self-hosted app UI.
- EF Core for relational persistence.
- .NET Aspire for local orchestration, service discovery, health, and telemetry.
- Microsoft.Extensions.AI for model/provider abstraction.
- Microsoft Agent Framework for agents, multi-agent workflows, tool use, human-in-the-loop, and workflow orchestration.

### 6. Agents are not a replacement for normal code

Use deterministic code for:

- CRUD.
- Validation.
- Authorization.
- Persistence.
- Configuration.
- Billing.
- State transitions.
- Audit events.

Use Agent Framework for:

- Open-ended reasoning.
- Business planning.
- Role-based agents.
- Artifact drafting.
- Multi-agent collaboration.
- Tool/MCP interaction.
- Human-in-the-loop workflows.

## Proposed solution structure

```text
/src
  /CSweet.App
  /CSweet.Api
  /CSweet.Domain
  /CSweet.Application
  /CSweet.Infrastructure
  /CSweet.AI
  /CSweet.WorkerHost
  /CSweet.Contracts
  /CSweet.AppHost
  /CSweet.ServiceDefaults
/tests
  /CSweet.UnitTests
  /CSweet.IntegrationTests
/docker
  /api.Dockerfile
  /app.Dockerfile
  /worker.Dockerfile
/docs
  /implementation
  /deployment
```

Root deployment files:

```text
docker-compose.yml
docker-compose.override.yml optional
.env.example
.dockerignore
```

### Project responsibilities

| Project | Responsibility |
|---|---|
| `CSweet.App` | Blazor WASM frontend. No direct database access. Calls `CSweet.Api`. |
| `CSweet.Api` | ASP.NET Core API. Auth, endpoints, request validation, application service calls. |
| `CSweet.Domain` | Entities, value objects, enums, domain invariants. No infrastructure dependencies. |
| `CSweet.Application` | Use cases, command/query handlers, orchestration, workflow coordination. |
| `CSweet.Infrastructure` | EF Core DbContext, repositories, provider persistence, file storage, external service clients. |
| `CSweet.AI` | LLM provider abstraction, Microsoft.Extensions.AI setup, Agent Framework adapters. |
| `CSweet.WorkerHost` | Local worker process/runtime for built-in workers and future connector workers. |
| `CSweet.Contracts` | Shared DTOs and contracts for API, workers, provider tests, artifacts, and workflows. |
| `CSweet.AppHost` | .NET Aspire distributed app definition for local development. |
| `CSweet.ServiceDefaults` | Aspire shared defaults for health checks, logging, OpenTelemetry, resiliency. |

## Logical architecture

```text
Blazor WASM App
  ↓ HTTP
CSweet.Api
  ↓ application services
CSweet.Application
  ↓ domain model
CSweet.Domain
  ↓ infrastructure ports
CSweet.Infrastructure
  ↓
Postgres / file storage / secrets / queues

CSweet.Application
  ↓ AI interfaces
CSweet.AI
  ↓ Microsoft.Extensions.AI / Agent Framework
LLM providers and agent workflows

CSweet.Application
  ↓ worker contracts
CSweet.WorkerHost
  ↓
Built-in workers, local agents, future marketplace workers
```

## Containerized runtime architecture

```text
Docker Compose
  ├─ csweet-app       public web entry point
  ├─ csweet-api       API and application services
  ├─ csweet-worker    local worker runtime
  └─ postgres         durable database volume
```

The containerized stack should be able to connect to LM Studio running on the host machine through a configurable base URL. The default Docker-oriented LM Studio URL should be:

```text
http://host.docker.internal:1234/v1
```

The direct local development default can remain:

```text
http://localhost:1234/v1
```

## Initial domain model

### System configuration domain

These entities are required before business onboarding.

```text
SystemConfiguration
LlmProviderProfile
ModelCapabilityTest
OnboardingStep
AuditEvent
```

### Business operating domain

These entities are required for the first useful business workflow.

```text
Organization
OrganizationUser
Role
StrategicObjective
Worker
Task
TaskRun
Artifact
Decision
Approval
Conversation
MemoryEntry
```

## First vertical slice

The first vertical slice must be:

```text
Docker Compose startup or Aspire startup
  → first-run setup
  → configure LM Studio
  → test provider
  → save provider profile
  → create admin
  → create business
  → generate initial roles/tasks
  → run one agent task
  → create artifact
  → approve/reject artifact
```

## Architecture decisions

### ADR-001: Use first-run system setup before business setup

**Decision:** A fresh install starts in setup mode. The business onboarding UI is blocked until the system has a valid configuration.

**Reason:** C-Sweet depends on model access to produce useful work. Capturing provider configuration first avoids a broken first business experience.

### ADR-002: Use Microsoft.Extensions.AI as primary LLM abstraction

**Decision:** Application services should not call OpenAI, LM Studio, Ollama, or Azure SDKs directly. Use `CSweet.AI` abstractions backed by Microsoft.Extensions.AI.

**Reason:** The project needs provider flexibility while staying in enterprise-ready .NET patterns.

### ADR-003: Use Microsoft Agent Framework behind an adapter

**Decision:** Do not leak Microsoft Agent Framework types throughout the whole codebase. Add an adapter layer in `CSweet.AI`.

**Reason:** Agent Framework is strategically aligned with the project, but an adapter protects the domain/application layers from framework churn.

### ADR-004: Use .NET Aspire for local development orchestration

**Decision:** Add `CSweet.AppHost` and `CSweet.ServiceDefaults` early.

**Reason:** The project will quickly become distributed: app, API, database, worker host, cache, queue, local LLM, vector store, and storage. Aspire gives a clean local developer experience and observability.

### ADR-005: Treat LM Studio as an OpenAI-compatible provider profile

**Decision:** LM Studio gets a provider preset, but it uses the same underlying provider path as other OpenAI-compatible endpoints.

**Reason:** This avoids special-case business logic and lets the same path support vLLM, llama-server, and hosted OpenAI-compatible providers.

### ADR-006: Provide Docker Compose as the default self-hosted distribution

**Decision:** C-Sweet must include Dockerfiles, Compose configuration, environment examples, and Docker deployment docs as part of the initial implementation track.

**Reason:** Docker Compose gives self-hosted users a repeatable way to run the API, app, worker, and database with minimal setup. This is likely the easiest distribution path for early users.

## Cross-cutting requirements

### Auditing

Every important change should create an `AuditEvent`:

- Provider profile created/updated/deleted.
- Provider capability test run.
- First-run setup completed.
- Organization created.
- Task created.
- Task run started/completed/failed.
- Artifact approved/rejected.
- Worker registered/updated.

### Error handling

Every endpoint should return typed problem details. Do not return raw exception messages to the UI.

Minimum error types:

- Validation error.
- Configuration incomplete.
- Provider unavailable.
- Model not found.
- Provider capability unsupported.
- Agent run failed.
- Worker unavailable.
- Artifact not found.
- Approval conflict.

### Testing

Each phase must include:

- Unit tests for domain/application logic.
- Integration tests for API endpoints.
- At least one happy-path UI test or manual QA checklist.
- Manual test instructions for LM Studio when external local services are involved.
- Docker startup validation when runtime services are affected.

### Security baseline

- Store API keys as secrets, not plaintext when possible.
- Do not log API keys.
- Do not log full prompts by default if they may contain business-sensitive information.
- Support local-only mode without external network dependencies.
- Allow providers to be disabled without deleting their history.
- Do not bake secrets into Docker images.
- Do not expose database ports publicly by default in self-hosted Compose.
