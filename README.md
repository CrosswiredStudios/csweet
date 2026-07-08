# CSweet

CSweet is an open-source, self-hostable operating environment for **agent-first companies**.

A user acts as the CEO. The CEO communicates primarily with a Personal Assistant / Chief of Staff, who translates executive intent into projects, staffing decisions, delegated work, approvals, and results.

Work can be performed by:

- Local AI agents using local or hosted LLMs
- Remote commercial workers operated by workforce providers
- Real human professionals
- Hybrid services combining software, agents, and people

## Product principles

1. **Agent-first, not agent-only.** Routine digital work should default to agents. People are added where judgment, credentials, accountability, relationships, or physical action are required.
2. **Outcomes over conversations.** Projects, tasks, decisions, artifacts, budgets, and audit history are first-class records.
3. **Capabilities over job titles.** Roles are convenient bundles of capabilities, responsibilities, permissions, and accountability.
4. **Local-first and provider-neutral.** The core application and Blazor interface are open source and can use local or hosted LLMs.
5. **Docker-first distribution.** A self-hosted installation should be runnable with Docker Compose out of the box.
6. **One mixed workforce.** Local agents, remote vendor workers, people, and hybrid services participate in the same organizational model.
7. **Explicit authority.** Autonomy, budgets, approvals, data access, and tool permissions are assigned per capability and scope.
8. **Durable execution.** Long-running work survives restarts, approvals, delays, retries, and provider outages.
9. **Company-owned history.** The company retains its work history, decisions, and artifacts even when a worker or provider is removed.
10. **Human effort at leverage points.** Agents prepare, organize, monitor, and execute routine work so people can focus on judgment, relationships, accountability, and real-world execution.
11. **Open core, optional network.** A self-hosted installation remains useful without the official marketplace or commercial providers.

## Initial technology direction

- .NET
- Microsoft Agent Framework as the initial agent and workflow runtime
- Blazor web interface
- PostgreSQL for durable company state
- Docker Compose for default self-hosted distribution
- S3-compatible object storage for artifacts
- Optional pgvector or Qdrant for semantic retrieval
- MCP and provider APIs for external capabilities
- Local or hosted OpenAI-compatible model endpoints
- SignalR for live company activity
- OpenTelemetry for runtime observability

The first implementation should be a modular monolith with containerized runtime services:

- `CSweet.App`
- `CSweet.Api`
- `CSweet.WorkerHost`
- `PostgreSQL`

Framework-specific types should remain behind application-owned abstractions so other runtimes can be supported later.

## Planning documents

1. [Product vision and operating model](docs/00-product-vision.md)
2. [Domain model](docs/01-domain-model.md)
3. [Agent orchestration](docs/02-agent-orchestration.md)
4. [Unified workforce marketplace](docs/03-workforce-marketplace.md)
5. [Remote workforce provider protocol](docs/04-remote-worker-provider-protocol.md)
6. [Human workforce and engagements](docs/05-human-workforce.md)
7. [Budgeting, authority, and governance](docs/06-budgeting-and-governance.md)
8. [Security, privacy, and trust](docs/07-security-privacy-and-trust.md)
9. [Application architecture](docs/08-application-architecture.md)
10. [Prototype roadmap](docs/09-prototype-roadmap.md)
11. [Open questions and decision log](docs/10-open-questions.md)
12. [Brand and naming notes](docs/11-brand-and-naming.md)
13. [Example end-to-end scenarios](docs/12-example-scenarios.md)
14. [System boundaries and deployment model](docs/13-system-boundaries-and-deployment.md)
15. [Phased implementation plans](docs/implementation/README.md)

## Working name

`CSweet` is the current working project name. Public branding, domain availability, and trademark clearance remain open decisions.

## Status

Planning and prototyping. These documents capture the current direction so future design and implementation sessions can build cumulatively rather than restarting from conversation history.
