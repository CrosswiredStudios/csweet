using CSweet.Application.Core;
using CSweet.Contracts.BusinessOnboarding;
using CSweet.Domain.Core;
using CSweet.Infrastructure.BusinessOnboarding;
using CSweet.Infrastructure.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public class BusinessOnboardingServiceTests
{
    [Fact]
    public async Task CompleteAsync_CreatesOrganizationDefaultsObjectiveTasksAndWorker()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var roleService = new RoleService(dbContext, auditWriter);
        var organizationService = new CoreOrganizationService(dbContext, auditWriter, roleService);
        var objectiveService = new StrategicObjectiveService(dbContext, auditWriter);
        var taskService = new WorkTaskService(dbContext, auditWriter);
        var workerService = new WorkerService(dbContext, auditWriter);
        var service = new BusinessOnboardingService(
            organizationService,
            roleService,
            objectiveService,
            taskService,
            workerService,
            auditWriter);

        var result = await service.CompleteAsync(new CompleteBusinessOnboardingRequest(
            "Example Co",
            "Software",
            "Idea",
            "Launch a paid MVP in 30 days",
            ["solo founder", "limited budget"],
            "Balanced and practical"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Onboarding);
        Assert.Equal(5, result.Onboarding.CreatedRoleCount);
        Assert.Equal(5, result.Onboarding.CreatedTaskCount);
        Assert.Equal($"/organizations/{result.Onboarding.OrganizationId}/command-center", result.Onboarding.NextRoute);

        var organizationId = result.Onboarding.OrganizationId;
        var organization = await dbContext.CoreOrganizations.SingleAsync(x => x.Id == organizationId);
        var roles = await dbContext.CoreRoles.Where(x => x.OrganizationId == organizationId).ToListAsync();
        var employees = await dbContext.CoreOrganizationUsers.Where(x => x.OrganizationId == organizationId).ToListAsync();
        var objective = await dbContext.CoreStrategicObjectives.SingleAsync(x => x.OrganizationId == organizationId);
        var tasks = await dbContext.CoreWorkTasks.Where(x => x.OrganizationId == organizationId).ToListAsync();
        var worker = await dbContext.CoreWorkers.SingleAsync(x => x.Id == result.Onboarding.DefaultWorkerId);

        Assert.Equal("Example Co", organization.Name);
        Assert.Equal("Software", organization.Industry);
        Assert.Equal("Idea", organization.Stage);
        Assert.Equal("Launch a paid MVP in 30 days", organization.PrimaryGoal);
        Assert.Contains("Balanced and practical", organization.ConstraintsJson);
        Assert.Contains(roles, x => x.Name == "CEO" && x.AuthorityLevel == AuthorityLevel.ExecutionWithApproval);
        var self = Assert.Single(employees);
        Assert.Equal("Self", self.DisplayName);
        Assert.Equal(EmployeeType.Human, self.EmployeeType);
        Assert.Contains(roles, x => x.Name == "Marketing" && x.ResponsibilitiesJson.Contains("Define target customer"));
        Assert.Equal(ObjectiveStatus.Active, objective.Status);
        Assert.Equal("Launch a paid MVP in 30 days", objective.Title);
        Assert.Equal(5, tasks.Count);
        Assert.Contains(tasks, x => x.Title == "Create 30-day execution plan" && x.AssignedWorkerId == worker.Id && x.Status == WorkTaskStatus.Ready);
        Assert.Equal("Local Strategy Agent", worker.Name);
        Assert.Equal(WorkerType.LocalAgent, worker.WorkerType);
        Assert.True(worker.RequiresHumanApproval);
    }

    [Fact]
    public async Task CompleteAsync_RequiresBusinessNameAndPrimaryGoal()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var roleService = new RoleService(dbContext, auditWriter);
        var service = new BusinessOnboardingService(
            new CoreOrganizationService(dbContext, auditWriter, roleService),
            roleService,
            new StrategicObjectiveService(dbContext, auditWriter),
            new WorkTaskService(dbContext, auditWriter),
            new WorkerService(dbContext, auditWriter),
            auditWriter);

        var missingName = await service.CompleteAsync(new CompleteBusinessOnboardingRequest(
            " ",
            null,
            "Idea",
            "Launch",
            null,
            "Balanced and practical"));
        var missingGoal = await service.CompleteAsync(new CompleteBusinessOnboardingRequest(
            "Example Co",
            null,
            "Idea",
            " ",
            null,
            "Balanced and practical"));

        Assert.False(missingName.Succeeded);
        Assert.Equal("validation_error", missingName.ErrorCode);
        Assert.False(missingGoal.Succeeded);
        Assert.Equal("validation_error", missingGoal.ErrorCode);
    }

    private static CSweetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CSweetDbContext(options);
    }
}
