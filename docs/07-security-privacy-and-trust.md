# Security, Privacy, and Trust

## Security principle

Workers propose actions. Application-enforced policy grants authority.

No prompt, model, marketplace listing, provider response, or human message may bypass the application’s:

- Permissions
- Budgets
- Approvals
- Data-access controls
- Audit requirements

## Trust boundaries

CSweet must distinguish:

- Core application code
- Included worker definitions
- Locally installed community workers
- Local MCP servers
- Remote workforce providers
- Human professionals
- Marketplace metadata
- Company users and administrators
- Untrusted project content

Each boundary requires explicit authentication, authorization, and audit behavior.

## Tool permissions

Every staff member should have a tool allowlist with capability-level grants.

Example:

```text
Researcher
- Web search: allowed
- Repository read: allowed
- Repository write: denied
- Email send: denied

Developer
- Repository read: allowed
- Branch write: allowed
- Merge to main: approval required
- Production deploy: denied
```

Permissions must be enforced by tool adapters, not merely described in prompts.

## Data scopes

Access grants should identify actual scope:

- Company
- Department
- Project
- Task
- Artifact IDs
- Record types
- Time window
- Read, write, export, or share permission

Broad permissions such as `project.read` should resolve to concrete authorized resources before context is assembled.

## Data classification

Initial classifications:

- Public
- Internal
- Confidential
- Restricted
- Regulated

Company policy should determine which worker execution types may receive each class.

Example:

- Public: any approved worker
- Internal: local or approved remote provider
- Confidential: local by default; remote with approval
- Restricted: selected workers only
- Regulated: credentialed and policy-approved workflows

## Remote data disclosure

Before a remote provider receives data, CSweet should record:

- Provider
- Worker
- Purpose
- Data items
- Classification
- Retention disclosure
- Authorization source
- Timestamp

The UI should make first-use and sensitive disclosures visible to the company administrator.

## Human access

Human professional access should be:

- Engagement-scoped
- Time-limited
- Least-privilege
- Revocable
- Audited
- Separate from agent tool grants

Viewing, downloading, editing, exporting, and resharing should be separately controllable where practical.

## Credentials and secrets

Secrets must:

- Be stored through a dedicated secret provider
- Be referenced by opaque connection IDs
- Never be placed into normal task context
- Be retrievable only by authorized tool adapters
- Support rotation and revocation
- Be isolated by company

Workers should receive capability grants, not raw API keys.

## Prompt injection

Web pages, documents, emails, repository content, provider responses, and marketplace descriptions are untrusted data.

Defenses include:

- Clear separation of system instructions and retrieved content
- Minimal context
- Tool-layer authorization
- Output validation
- Data-classification checks
- Confirmation for sensitive actions
- Detection and logging of instruction-like external content

Prompt-injection resistance cannot rely solely on model instructions.

## Sandboxed execution

Code-writing workers must not execute arbitrary code on the application host.

Sandbox requirements:

- Disposable workspace
- Filesystem isolation
- Network allowlist
- CPU and memory limits
- Time limits
- Secret scoping
- Artifact export controls
- Process and package restrictions
- Audit logs

Containerized third-party code should be treated as high risk and is not required for the first marketplace release.

## Side effects and idempotency

Every side-effecting operation should support:

- Idempotency key
- Dry-run or preview where possible
- Approval reference
- Result receipt
- Retry classification
- Cancellation behavior

Examples include:

- Emails
- Purchases
- Git merges
- Deployments
- Payments
- Filings
- Data deletion

## Worker updates

Published machine-worker versions are immutable.

Updates cannot silently:

- Add permissions
- Change data-processing destination
- Add human access
- Change billing meters
- Broaden capability scope
- Replace package content under the same version

Material changes require a new version and company approval.

## Marketplace trust tiers

Suggested display categories:

- Included and reviewed
- Community declarative
- Remote verified provider
- Human identity verified
- Professional credential verified
- Hybrid managed service
- Experimental or unverified

Trust labels should state exactly what was verified.

## Provider health and kill switch

The marketplace or company administrator should be able to:

- Prevent new installations
- Warn existing customers
- Disable a compromised version or endpoint
- Revoke a provider connection
- Suspend future task assignment
- Preserve company artifacts and history

A kill switch should not destroy customer-owned data.

## Audit events

Capture:

- Authentication and connection changes
- Permission grants and revocations
- Data disclosures
- Context manifests
- Tool calls
- Approvals
- Budget reservations
- Provider quotes and receipts
- Human access
- Artifact creation and downloads
- Worker updates
- Security warnings

Audit records should be append-oriented and protected from ordinary worker modification.

## Multi-tenancy

All company records, artifacts, vector indexes, credentials, sessions, and events require tenant isolation.

Authorization should be checked in the application and data-access layers.

Do not rely only on UI filtering.

## Privacy and provider disclosures

Remote and hybrid providers should disclose:

- Data categories received
- Processing locations
- Retention period
- Training use
- Subprocessors
- Human access
- Deletion process
- Security contact

CSweet records the disclosure version accepted by the company.

## Human marketplace safety

Before paid human engagements, the hosted marketplace needs plans for:

- Identity verification
- Credential verification
- Fraud controls
- Harassment and abuse reporting
- Dispute handling
- Payment risk
- Account suspension
- Legal process and record retention

## Initial security milestones

1. Company and user authorization model
2. Scoped tool grants
3. Secret abstraction
4. Context manifest and data classification
5. Approval service
6. Budget enforcement
7. Sandboxed code execution
8. Provider authentication and signed receipts
9. Human engagement access expiry
10. Security review before real financial or regulated workflows
