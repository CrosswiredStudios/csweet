# Phase 6 - Core Business Domain

## Goal

Implement the core business operating model: organizations, roles, objectives, workers, tasks, task runs, artifacts, and approvals.

## Why this phase matters

This domain model is the heart of C-Sweet. It must support local AI workers today and marketplace human/agent workers later.

## Deliverables

- Domain entities.
- EF Core mappings and migration.
- API endpoints for basic CRUD.
- Application services/use cases.
- Validation rules.
- Audit events.
- Tests.

## Entities

### Organization

```csharp
public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Mission { get; set; }
    public string? Stage { get; set; }
    public string? PrimaryGoal { get; set; }
    public string? ConstraintsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### OrganizationUser

```csharp
public sealed class OrganizationUser
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public OrganizationPermissionLevel PermissionLevel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

### Role

```csharp
public sealed class Role
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResponsibilitiesJson { get; set; } = "[]";
    public AuthorityLevel AuthorityLevel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Default roles:

```text
CEO
Operations
Finance
Marketing
Product
```

### StrategicObjective

```csharp
public sealed class StrategicObjective
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObjectiveStatus Status { get; set; }
    public DateTimeOffset? TargetDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### Worker

```csharp
public sealed class Worker
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkerType WorkerType { get; set; }
    public WorkerExecutionMode ExecutionMode { get; set; }
    public string CapabilitiesJson { get; set; } = "[]";
    public string? CostModelJson { get; set; }
    public string? EndpointConfigurationJson { get; set; }
    public bool IsEnabled { get; set; }
    public bool RequiresHumanApproval { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### Task

Use a non-conflicting name in code such as `WorkTask` or `BusinessTask` to avoid confusion with `System.Threading.Tasks.Task`.

```csharp
public sealed class WorkTask
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? StrategicObjectiveId { get; set; }
    public Guid? AssignedRoleId { get; set; }
    public Guid? AssignedWorkerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkTaskStatus Status { get; set; }
    public WorkTaskPriority Priority { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### TaskRun

```csharp
public sealed class TaskRun
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid? WorkerId { get; set; }
    public TaskRunStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? FailureMessage { get; set; }
    public decimal? CostAmount { get; set; }
    public string? CostCurrency { get; set; }
}
```

### Artifact

```csharp
public sealed class Artifact
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? TaskRunId { get; set; }
    public ArtifactType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### Approval

```csharp
public sealed class Approval
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public ApprovalStatus Status { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

## Enums

```csharp
public enum AuthorityLevel
{
    Advisory,
    Drafting,
    ExecutionWithApproval,
    Autonomous
}

public enum WorkerType
{
    LocalAgent,
    RemoteAgent,
    Human,
    McpServer,
    OpenAiCompatibleService,
    MarketplaceProxy,
    BuiltInSystem
}

public enum WorkerExecutionMode
{
    InProcess,
    LocalWorkerHost,
    HttpRemote,
    McpRemote,
    MarketplaceManaged,
    HumanFulfillment
}

public enum WorkTaskStatus
{
    Backlog,
    Ready,
    Assigned,
    Running,
    WaitingForApproval,
    Completed,
    Failed,
    Cancelled
}

public enum TaskRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum ArtifactType
{
    Document,
    Email,
    Plan,
    Code,
    Research,
    Decision,
    File
}

public enum ApprovalStatus
{
    NotRequired,
    Pending,
    Approved,
    Rejected,
    RevisionRequested
}
```

## API endpoints

Initial endpoints:

```http
GET /api/organizations
POST /api/organizations
GET /api/organizations/{id}
PUT /api/organizations/{id}

GET /api/organizations/{organizationId}/roles
POST /api/organizations/{organizationId}/roles

GET /api/organizations/{organizationId}/workers
POST /api/organizations/{organizationId}/workers

GET /api/organizations/{organizationId}/tasks
POST /api/organizations/{organizationId}/tasks
GET /api/tasks/{taskId}

GET /api/tasks/{taskId}/runs
GET /api/artifacts/{artifactId}
POST /api/artifacts/{artifactId}/approve
POST /api/artifacts/{artifactId}/reject
```

## Validation rules

- Organization name is required.
- Role name is required and unique per organization.
- Task title is required.
- Task must belong to organization.
- Assigned role must belong to same organization.
- Assigned worker must be global or belong to same organization.
- Artifact approval requires artifact to exist.
- Approved artifact cannot be rejected without creating a new decision or revision record.

## Testing requirements

### Unit tests

- Organization creation requires name.
- Role uniqueness per organization.
- Task assignment validates same organization.
- Artifact approval state transition works.
- Invalid approval transition fails.

### Integration tests

- Create organization.
- Add role.
- Add worker.
- Create task.
- Create artifact.
- Approve artifact.

## Acceptance criteria

- [ ] Core entities exist.
- [ ] Migration exists.
- [ ] Basic CRUD endpoints work.
- [ ] Domain validation prevents invalid cross-organization links.
- [ ] Artifact approval/rejection works.
- [ ] Audit events are written for important changes.
- [ ] Tests cover core flows.
