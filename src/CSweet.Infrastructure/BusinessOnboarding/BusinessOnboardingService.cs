using System.Text.Json;
using CSweet.Application.BusinessOnboarding;
using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.BusinessOnboarding;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;

namespace CSweet.Infrastructure.BusinessOnboarding;

public sealed class BusinessOnboardingService : IBusinessOnboardingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICoreOrganizationService _organizationService;
    private readonly IRoleService _roleService;
    private readonly IStrategicObjectiveService _objectiveService;
    private readonly IWorkTaskService _taskService;
    private readonly IWorkerService _workerService;
    private readonly IAuditEventWriter _auditEventWriter;

    public BusinessOnboardingService(
        ICoreOrganizationService organizationService,
        IRoleService roleService,
        IStrategicObjectiveService objectiveService,
        IWorkTaskService taskService,
        IWorkerService workerService,
        IAuditEventWriter auditEventWriter)
    {
        _organizationService = organizationService;
        _roleService = roleService;
        _objectiveService = objectiveService;
        _taskService = taskService;
        _workerService = workerService;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<BusinessOnboardingActionResponse> CompleteAsync(
        CompleteBusinessOnboardingRequest request,
        CancellationToken cancellationToken = default,
        Guid? applicationUserId = null)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return Failure("validation_error", "Business name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryGoal))
        {
            return Failure("validation_error", "Primary goal is required.");
        }

        var constraints = NormalizeConstraints(request.Constraints);
        var contextJson = JsonSerializer.Serialize(new
        {
            constraints,
            preferredOperatingStyle = TrimOrNull(request.PreferredOperatingStyle)
        }, JsonOptions);

        var organizationResult = await _organizationService.CreateAsync(
            new CreateOrganizationRequest(
                request.BusinessName,
                TrimOrNull(request.Industry),
                BuildMission(request.PrimaryGoal, request.PreferredOperatingStyle),
                TrimOrNull(request.Stage),
                request.PrimaryGoal.Trim(),
                contextJson),
            cancellationToken,
            applicationUserId);

        if (!organizationResult.Succeeded || organizationResult.Organization is null)
        {
            return Failure(organizationResult.ErrorCode ?? "organization_create_failed", organizationResult.Message ?? "Organization could not be created.");
        }

        var organizationId = organizationResult.Organization.Id;
        var roles = await _roleService.ListByOrganizationAsync(organizationId, cancellationToken);

        var objectiveResult = await _objectiveService.CreateAsync(
            organizationId,
            new CreateStrategicObjectiveRequest(
                request.PrimaryGoal.Trim(),
                "Create a practical operating plan that turns the business goal into immediate actions, risks, owners, and deliverables.",
                (int)ObjectiveStatus.Active,
                DateTimeOffset.UtcNow.AddDays(30)),
            cancellationToken);

        if (!objectiveResult.Succeeded || objectiveResult.StrategicObjective is null)
        {
            return Failure(objectiveResult.ErrorCode ?? "objective_create_failed", objectiveResult.Message ?? "Strategic objective could not be created.");
        }

        var workerResult = await _workerService.CreateAsync(
            organizationId,
            new CreateWorkerRequest(
                "Local Strategy Agent",
                "Default local agent for business planning, operating plans, task breakdown, and risk identification.",
                (int)WorkerType.LocalAgent,
                (int)WorkerExecutionMode.InProcess,
                JsonSerializer.Serialize(new[]
                {
                    "business-planning",
                    "operating-plan",
                    "task-breakdown",
                    "risk-identification"
                }, JsonOptions),
                null,
                null,
                IsEnabled: true,
                RequiresHumanApproval: true),
            cancellationToken);

        if (!workerResult.Succeeded || workerResult.Worker is null)
        {
            return Failure(workerResult.ErrorCode ?? "worker_create_failed", workerResult.Message ?? "Default local strategy worker could not be registered.");
        }

        var taskCount = 0;
        foreach (var task in BuildInitialTasks(
            objectiveResult.StrategicObjective.Id,
            workerResult.Worker.Id,
            roles))
        {
            var taskResult = await _taskService.CreateAsync(organizationId, task, cancellationToken);
            if (!taskResult.Succeeded)
            {
                return Failure(taskResult.ErrorCode ?? "task_create_failed", taskResult.Message ?? "Initial task backlog could not be created.");
            }

            taskCount++;
        }

        await _auditEventWriter.WriteAsync(
            "business_onboarding.completed",
            "Organization",
            organizationId,
            $"Business onboarding completed for '{organizationResult.Organization.Name}'.",
            cancellationToken: cancellationToken);

        var response = new CompleteBusinessOnboardingResponse(
            organizationId,
            roles.Count,
            taskCount,
            workerResult.Worker.Id,
            $"/organizations/{organizationId}/command-center");

        return new BusinessOnboardingActionResponse(true, null, "Business onboarding completed.", response);
    }

    private static IReadOnlyList<CreateWorkTaskRequest> BuildInitialTasks(
        Guid objectiveId,
        Guid workerId,
        IReadOnlyList<RoleResponse> roles)
    {
        var marketingRoleId = FindRoleId(roles, "Marketing");
        var operationsRoleId = FindRoleId(roles, "Operations");
        var financeRoleId = FindRoleId(roles, "Finance");
        var productRoleId = FindRoleId(roles, "Product");

        return
        [
            new CreateWorkTaskRequest(
                "Define target customer",
                "Clarify the customer segment, buyer pain, and first reachable audience.",
                objectiveId,
                marketingRoleId,
                null,
                (int)WorkTaskStatus.Backlog,
                (int)WorkTaskPriority.High,
                null,
                RequiresApproval: false),
            new CreateWorkTaskRequest(
                "Draft basic operating plan",
                "Outline the first operating cadence, owners, decisions, and execution rhythm.",
                objectiveId,
                operationsRoleId,
                null,
                (int)WorkTaskStatus.Backlog,
                (int)WorkTaskPriority.Medium,
                null,
                RequiresApproval: false),
            new CreateWorkTaskRequest(
                "Identify first revenue channel",
                "Choose the most practical initial channel for validating revenue.",
                objectiveId,
                marketingRoleId,
                null,
                (int)WorkTaskStatus.Backlog,
                (int)WorkTaskPriority.High,
                null,
                RequiresApproval: false),
            new CreateWorkTaskRequest(
                "List operational risks",
                "Identify the constraints, assumptions, financial risks, and execution bottlenecks.",
                objectiveId,
                financeRoleId,
                null,
                (int)WorkTaskStatus.Backlog,
                (int)WorkTaskPriority.Medium,
                null,
                RequiresApproval: false),
            new CreateWorkTaskRequest(
                "Create 30-day execution plan",
                "Turn the primary goal into a sequenced 30-day plan with actions, owners, risks, and deliverables.",
                objectiveId,
                productRoleId,
                workerId,
                (int)WorkTaskStatus.Ready,
                (int)WorkTaskPriority.Critical,
                DateTimeOffset.UtcNow.AddDays(30),
                RequiresApproval: true)
        ];
    }

    private static Guid? FindRoleId(IReadOnlyList<RoleResponse> roles, string name)
    {
        return roles.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static string BuildMission(string primaryGoal, string? preferredOperatingStyle)
    {
        var mission = $"Primary goal: {primaryGoal.Trim()}";
        var style = TrimOrNull(preferredOperatingStyle);
        return style is null ? mission : $"{mission}\nPreferred operating style: {style}";
    }

    private static IReadOnlyList<string> NormalizeConstraints(IReadOnlyList<string>? constraints)
    {
        return constraints?
            .Select(TrimOrNull)
            .Where(x => x is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BusinessOnboardingActionResponse Failure(string errorCode, string message) =>
        new(false, errorCode, message);
}
