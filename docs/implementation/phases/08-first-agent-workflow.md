# Phase 8 - First Agent Workflow: Generate 30-Day Operating Plan

## Goal

Implement the first complete C-Sweet workflow:

> Generate a 30-day operating plan for the user's business using the configured LLM provider and local strategy agent.

## Why this phase matters

This is the first moment C-Sweet feels real. It proves that system setup, provider configuration, business onboarding, agents, tasks, task runs, artifacts, and approvals all work together.

## Deliverables

- `Generate30DayOperatingPlan` use case.
- API endpoint to run the workflow.
- Task run state transitions.
- Agent prompt/context assembly.
- Artifact creation.
- Artifact review UI.
- Approve/reject actions.
- Error handling and logs.

## User story

As a business owner, I want C-Sweet to generate a practical 30-day operating plan, so I can review and approve an actionable plan for my business.

## Workflow

```text
User opens command center
  → clicks Generate 30-day plan
  → API creates TaskRun
  → application assembles organization context
  → Local Strategy Agent runs
  → output is saved as Artifact
  → task moves to WaitingForApproval
  → user reviews artifact
  → user approves or rejects
```

## Endpoint

```http
POST /api/organizations/{organizationId}/workflows/generate-30-day-operating-plan
```

Request:

```json
{
  "taskId": "optional-existing-task-id",
  "additionalInstructions": "Focus on finding first paying customers."
}
```

Response:

```json
{
  "taskRunId": "...",
  "artifactId": "...",
  "status": "WaitingForApproval"
}
```

## Context assembly

Collect:

- Organization name.
- Industry.
- Stage.
- Primary goal.
- Constraints.
- Preferred operating style.
- Relevant roles.
- Existing open tasks.
- Existing approved artifacts if any.

Do not send unnecessary data.

## Prompt design

### System prompt

```text
You are the Local Strategy Agent inside C-Sweet.
You create practical operating plans for small businesses.
Be concrete, useful, and realistic.
Do not pretend to know facts not provided by the user.
List assumptions clearly.
Prefer action steps with owners, timing, risks, and expected outputs.
```

### User prompt template

```text
Create a 30-day operating plan for this business.

Business:
- Name: {{OrganizationName}}
- Industry: {{Industry}}
- Stage: {{Stage}}
- Primary Goal: {{PrimaryGoal}}
- Constraints: {{Constraints}}
- Operating Style: {{PreferredOperatingStyle}}

The plan should include:
1. Executive summary
2. Assumptions
3. Week-by-week plan
4. Recommended owners/roles
5. Risks and mitigations
6. Metrics to track
7. Suggested next tasks for C-Sweet

Return the plan in Markdown.
```

## Output handling

The first version can accept Markdown text.

Later versions can require structured JSON for task extraction.

Initial artifact:

```text
ArtifactType: Plan
Title: 30-Day Operating Plan
Content: agent output markdown
ApprovalStatus: Pending
Version: 1
```

## State transitions

### Success path

```text
TaskRun.Pending
  → TaskRun.Running
  → TaskRun.Completed

Task.Ready or Assigned
  → Task.Running
  → Task.WaitingForApproval

Artifact.Pending
```

### Failure path

```text
TaskRun.Pending
  → TaskRun.Running
  → TaskRun.Failed

Task.Running
  → Task.Failed or Ready
```

Recommended first behavior:

- If the run fails, set task back to `Ready` and store failure on task run.
- Let user retry.

## Artifact approval

### Approve

```http
POST /api/artifacts/{artifactId}/approve
```

Effects:

- Artifact status = Approved.
- Approval record status = Approved.
- Task status = Completed if the artifact belongs to a task.
- Audit event created.

### Reject

```http
POST /api/artifacts/{artifactId}/reject
```

Request:

```json
{
  "comment": "Too vague. Add more concrete sales actions."
}
```

Effects:

- Artifact status = Rejected or RevisionRequested.
- Approval record status = Rejected or RevisionRequested.
- Task status = Ready or WaitingForRevision.
- Audit event created.

## UI requirements

Command center should show a clear call-to-action:

```text
Generate 30-Day Operating Plan
```

While running:

- Show task run status.
- Show spinner/progress message.
- Do not block the whole app if the request is long.

After completion:

- Show artifact title.
- Render Markdown.
- Show Approve button.
- Show Request Revision or Reject button.
- Show task run metadata.

## Error messages

Common errors:

```text
No default chat provider configured.
Configured provider is disabled.
Provider test has not succeeded.
Provider unavailable.
Agent run failed.
Task not found.
Organization not found.
```

Keep messages user-friendly.

## Testing requirements

### Unit tests

- Context assembler includes required fields.
- Missing provider causes friendly failure.
- Successful agent result creates artifact.
- Failed agent result stores task run failure.
- Approving artifact completes task.

### Integration tests

Use fake agent runner:

- Create organization.
- Run workflow.
- Verify task run completed.
- Verify artifact created.
- Approve artifact.
- Verify task completed.

### Manual LM Studio QA

- Run setup with LM Studio.
- Create business.
- Click generate plan.
- Confirm a real plan appears.
- Approve plan.
- Stop LM Studio and retry to confirm error handling.

## Acceptance criteria

- [ ] User can run generate 30-day plan workflow.
- [ ] Workflow uses configured default chat provider.
- [ ] Task run is persisted.
- [ ] Artifact is persisted.
- [ ] Artifact can be approved or rejected.
- [ ] Failure cases are visible and retryable.
- [ ] Tests cover success and failure paths.
