# C-Sweet Implementation Plans

Last updated: 2026-07-08

## Purpose

This folder breaks the proposed C-Sweet implementation into phased plans that can be handed to junior developers. The project vision is an open-source, self-hostable application that lets a user model a business, define executive-style goals, assign work to AI or human workers, review artifacts, and eventually connect to a live marketplace of third-party workers.

The first production goal is not the marketplace. The first production goal is a reliable local vertical slice:

> A user launches C-Sweet, completes first-run system setup, connects to a local or hosted LLM provider, creates their first business, runs a first agent-generated task, and approves the resulting artifact.

## Document map

Read these files in order:

1. [00 Architecture Baseline](./00-architecture-baseline.md)
2. [01 Phase 1 - Repository and Solution Bootstrap](./phases/01-repository-and-solution-bootstrap.md)
3. [02 Phase 2 - Configuration Persistence and First-Run Guard](./phases/02-configuration-persistence-and-first-run-guard.md)
4. [03 Phase 3 - LLM Provider Abstraction and LM Studio](./phases/03-llm-provider-abstraction-and-lm-studio.md)
5. [04 Phase 4 - System Setup Wizard](./phases/04-system-setup-wizard.md)
6. [05 Phase 5 - Microsoft Agent Framework Integration](./phases/05-microsoft-agent-framework-integration.md)
7. [06 Phase 6 - Core Business Domain](./phases/06-core-business-domain.md)
8. [07 Phase 7 - Business Onboarding](./phases/07-business-onboarding.md)
9. [08 Phase 8 - First Agent Workflow](./phases/08-first-agent-workflow.md)
10. [09 Phase 9 - Worker Runtime and Worker Contract](./phases/09-worker-runtime-and-worker-contract.md)
11. [10 Phase 10 - Observability, Security, and Operations](./phases/10-observability-security-operations.md)
12. [11 Marketplace Readiness](./11-marketplace-readiness.md)
13. [12 Junior Developer Task Checklist](./12-junior-developer-task-checklist.md)

## Recommended implementation order

Do not start by building the marketplace or a complex autonomous company brain. Build in this order:

1. Solution structure and local dev orchestration.
2. Database and first-run configuration state.
3. LLM provider profiles and connection tests.
4. First-run setup wizard.
5. Minimal Microsoft Agent Framework adapter.
6. Core organization, role, task, run, artifact, and approval entities.
7. Business onboarding.
8. One useful agent workflow: generate a 30-day operating plan.
9. Worker runtime and worker contract.
10. Observability, security, and deployment hardening.
11. Marketplace integration points.

## Core architectural assumptions

- C-Sweet is self-hostable first.
- The default first provider is LM Studio running on the local machine.
- Provider configuration must be flexible enough to support local and hosted OpenAI-compatible endpoints, Azure OpenAI, OpenAI, Ollama, Anthropic, Microsoft Foundry, and later custom providers.
- Enterprise-ready .NET libraries are preferred over custom infrastructure.
- Microsoft.Extensions.AI should be used as the normal LLM abstraction layer.
- Microsoft Agent Framework should be used where agent behavior, multi-agent workflows, tool use, human-in-the-loop, or workflow orchestration adds value.
- Normal deterministic code should still handle CRUD, validation, security, persistence, billing, and state transitions.

## Reference docs

Use these official references when implementing:

- Microsoft Agent Framework overview: https://learn.microsoft.com/en-us/agent-framework/overview/
- Microsoft Agent Framework workflows: https://learn.microsoft.com/en-us/agent-framework/workflows/
- Microsoft Agent Framework human-in-the-loop workflows: https://learn.microsoft.com/en-us/agent-framework/workflows/human-in-the-loop
- Microsoft.Extensions.AI: https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai
- Microsoft.Extensions.AI chat quickstart: https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-chat-app
- .NET Aspire overview: https://learn.microsoft.com/en-ca/dotnet/aspire/get-started/aspire-overview
- .NET Aspire AppHost: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview
- EF Core overview: https://learn.microsoft.com/en-us/ef/core/
- EF Core DbContext configuration: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
- EF Core migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- LM Studio OpenAI compatibility: https://lmstudio.ai/docs/developer/openai-compat
- LM Studio local server: https://lmstudio.ai/docs/developer/core/server
- LM Studio chat completions: https://lmstudio.ai/docs/developer/openai-compat/chat-completions
- LM Studio embeddings: https://lmstudio.ai/docs/developer/openai-compat/embeddings
- LM Studio tool use: https://lmstudio.ai/docs/developer/openai-compat/tools

## Definition of done for the full first vertical slice

The first full vertical slice is complete when:

- A fresh database starts with `IsFirstRunComplete = false`.
- The UI redirects to `/setup` until setup is complete.
- The user can configure LM Studio using `http://localhost:1234/v1`.
- The system can list or accept a model ID.
- The system can run a chat completion test.
- The system can store provider capability results.
- The user can create the first admin account or local owner profile.
- The user can create the first business.
- The system creates default executive roles and an initial strategic objective.
- The user can run “Generate 30-day operating plan.”
- A task run is persisted with logs and status changes.
- An artifact is created and shown in the UI.
- The user can approve or reject the artifact.
- Errors are visible in logs and task run history.
