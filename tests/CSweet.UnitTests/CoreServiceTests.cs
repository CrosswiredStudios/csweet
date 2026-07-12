using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public class CoreServiceTests
{
    #region Organization Tests

    [Fact]
    public async Task OrganizationCreation_RequiresName()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var roleService = new RoleService(dbContext, auditWriter);
        var service = new CoreOrganizationService(dbContext, auditWriter, roleService);

        var request = new CreateOrganizationRequest(
            Name: "  ", // whitespace only
            Industry: null, Mission: null, Stage: null,
            PrimaryGoal: null, ConstraintsJson: null);

        var result = await service.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
        Assert.Contains("name is required", result.Message!);
    }

    [Fact]
    public async Task OrganizationCreation_SucceedsWithValidName()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var roleService = new RoleService(dbContext, auditWriter);
        var service = new CoreOrganizationService(dbContext, auditWriter, roleService);

        var request = new CreateOrganizationRequest(
            Name: "Acme Corp", Industry: "Technology", Mission: null, Stage: null,
            PrimaryGoal: null, ConstraintsJson: null);

        var result = await service.CreateAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Organization);
        Assert.Equal("Acme Corp", result.Organization.Name);
    }

    [Fact]
    public async Task OrganizationCreation_SeesDefaultRoles()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var roleService = new RoleService(dbContext, auditWriter);
        var service = new CoreOrganizationService(dbContext, auditWriter, roleService);

        var request = new CreateOrganizationRequest(
            Name: "Acme Corp", Industry: null, Mission: null, Stage: null,
            PrimaryGoal: null, ConstraintsJson: null);

        var result = await service.CreateAsync(request);

        Assert.True(result.Succeeded);
        var roles = await dbContext.CoreRoles
            .Where(r => r.OrganizationId == result.Organization!.Id)
            .ToListAsync();

        Assert.Equal(5, roles.Count);
        Assert.Contains(roles, r => r.Name == "CEO");
        Assert.Contains(roles, r => r.Name == "Operations");
        Assert.Contains(roles, r => r.Name == "Finance");
        Assert.Contains(roles, r => r.Name == "Marketing");
        Assert.Contains(roles, r => r.Name == "Product");
    }

    [Fact]
    public async Task OrganizationCreation_SeedsCeoAndPersonalAssistantHierarchy()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var roleService = new RoleService(dbContext, auditWriter);
        var service = new CoreOrganizationService(dbContext, auditWriter, roleService);

        var result = await service.CreateAsync(new CreateOrganizationRequest(
            Name: "Acme Corp", Industry: null, Mission: null, Stage: null,
            PrimaryGoal: null, ConstraintsJson: "{\"personalAssistant\":{\"avatar\":\"strategist\",\"demeanor\":\"Balanced and practical\"}}"));

        Assert.True(result.Succeeded);

        var organizationId = result.Organization!.Id;
        var employees = await dbContext.CoreOrganizationUsers
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync();
        var personalAssistantWorker = await dbContext.CoreWorkers
            .SingleAsync(x => x.OrganizationId == organizationId && x.Name == "Personal Assistant");
        var ceoRole = await dbContext.CoreRoles
            .SingleAsync(x => x.OrganizationId == organizationId && x.Name == "CEO");

        var ceo = Assert.Single(employees, x => x.DisplayName == "Self");
        var assistant = Assert.Single(employees, x => x.DisplayName == "Personal Assistant");

        Assert.Equal(EmployeeType.Human, ceo.EmployeeType);
        Assert.Equal(OrganizationPermissionLevel.Owner, ceo.PermissionLevel);
        Assert.Equal(ceoRole.Id, ceo.RoleId);
        Assert.Equal(EmployeeType.Agent, assistant.EmployeeType);
        Assert.Equal(ceo.Id, assistant.ReportsToOrganizationUserId);
        Assert.Equal(personalAssistantWorker.Id, assistant.WorkerId);
        Assert.Contains("Balanced and practical", personalAssistantWorker.EndpointConfigurationJson);
    }

    [Fact]
    public async Task OrganizationUpdate_RequiresExistingOrganization()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var roleService = new RoleService(dbContext, auditWriter);
        var service = new CoreOrganizationService(dbContext, auditWriter, roleService);

        var request = new UpdateOrganizationRequest(
            Name: "New Name", Industry: null, Mission: null, Stage: null,
            PrimaryGoal: null, ConstraintsJson: null);

        var result = await service.UpdateAsync(Guid.NewGuid(), request);

        Assert.False(result.Succeeded);
        Assert.Equal("not_found", result.ErrorCode);
    }

    #endregion

    #region Role Tests

    [Fact]
    public async Task RoleCreation_RequiresName()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new RoleService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var request = new CreateRoleRequest(
            Name: "  ", Description: "Test", ResponsibilitiesJson: "[]", AuthorityLevel: 0);

        var result = await service.CreateAsync(org.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
    }

    [Fact]
    public async Task RoleCreation_EnforcesUniquenessPerOrganization()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new RoleService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var request = new CreateRoleRequest(
            Name: "CEO", Description: "Test", ResponsibilitiesJson: "[]", AuthorityLevel: 0);

        var result1 = await service.CreateAsync(org.Id, request);
        Assert.True(result1.Succeeded);

        var result2 = await service.CreateAsync(org.Id, request);
        Assert.False(result2.Succeeded);
        Assert.Equal("duplicate_role", result2.ErrorCode);
    }

    [Fact]
    public async Task RoleCreation_AllowsSameNameInDifferentOrganizations()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new RoleService(dbContext, auditWriter);

        var org1 = CreateOrganization();
        var org2 = CreateOrganization();
        dbContext.CoreOrganizations.AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var request = new CreateRoleRequest(
            Name: "CEO", Description: "Test", ResponsibilitiesJson: "[]", AuthorityLevel: 0);

        var result1 = await service.CreateAsync(org1.Id, request);
        var result2 = await service.CreateAsync(org2.Id, request);

        Assert.True(result1.Succeeded);
        Assert.True(result2.Succeeded);
    }

    #endregion

    #region Task Tests

    [Fact]
    public async Task TaskCreation_RequiresTitle()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new WorkTaskService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var request = new CreateWorkTaskRequest(
            Title: "  ", Description: string.Empty, StrategicObjectiveId: null,
            AssignedRoleId: null, AssignedWorkerId: null, Status: 0,
            Priority: 0, DueDate: null, RequiresApproval: false);

        var result = await service.CreateAsync(org.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
    }

    [Fact]
    public async Task TaskCreation_ValidatesStrategicObjectiveBelongsToOrganization()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new WorkTaskService(dbContext, auditWriter);

        var org1 = CreateOrganization();
        var org2 = CreateOrganization();
        dbContext.CoreOrganizations.AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var objective = new StrategicObjective
        {
            Id = Guid.NewGuid(),
            OrganizationId = org2.Id,
            Title = "Objective",
            Description = "Test",
            Status = ObjectiveStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreStrategicObjectives.Add(objective);
        await dbContext.SaveChangesAsync();

        var request = new CreateWorkTaskRequest(
            Title: "Test Task", Description: string.Empty, StrategicObjectiveId: objective.Id,
            AssignedRoleId: null, AssignedWorkerId: null, Status: 0,
            Priority: 0, DueDate: null, RequiresApproval: false);

        var result = await service.CreateAsync(org1.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_objective", result.ErrorCode);
    }

    [Fact]
    public async Task TaskCreation_ValidatesAssignedRoleBelongsToOrganization()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new WorkTaskService(dbContext, auditWriter);

        var org1 = CreateOrganization();
        var org2 = CreateOrganization();
        dbContext.CoreOrganizations.AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = org2.Id,
            Name = "CEO",
            Description = "Test",
            ResponsibilitiesJson = "[]",
            AuthorityLevel = AuthorityLevel.Autonomous,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreRoles.Add(role);
        await dbContext.SaveChangesAsync();

        var request = new CreateWorkTaskRequest(
            Title: "Test Task", Description: string.Empty, StrategicObjectiveId: null,
            AssignedRoleId: role.Id, AssignedWorkerId: null, Status: 0,
            Priority: 0, DueDate: null, RequiresApproval: false);

        var result = await service.CreateAsync(org1.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_role", result.ErrorCode);
    }

    [Fact]
    public async Task TaskCreation_AllowsGlobalWorkerAssignment()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new WorkTaskService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var worker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = null, // Global worker
            Name = "Global Worker",
            Description = "Test",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(worker);
        await dbContext.SaveChangesAsync();

        var request = new CreateWorkTaskRequest(
            Title: "Test Task", Description: string.Empty, StrategicObjectiveId: null,
            AssignedRoleId: null, AssignedWorkerId: worker.Id, Status: 0,
            Priority: 0, DueDate: null, RequiresApproval: false);

        var result = await service.CreateAsync(org.Id, request);

        Assert.True(result.Succeeded);
    }

    #endregion

    #region Approval Tests

    [Fact]
    public async Task ArtifactApproval_StateTransitionWorks()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ArtifactApprovalService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Type = ArtifactType.Document,
            Title = "Test Document",
            Content = "Test content",
            Version = 1,
            ApprovalStatus = ApprovalStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreArtifacts.Add(artifact);
        await dbContext.SaveChangesAsync();

        var result = await service.ApproveAsync(artifact.Id, "Looks good");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Approval);
        Assert.Equal((int)ApprovalStatus.Approved, result.Approval.Status);

        var updated = await dbContext.CoreArtifacts.FindAsync(artifact.Id);
        Assert.Equal(ApprovalStatus.Approved, updated!.ApprovalStatus);
    }

    [Fact]
    public async Task ArtifactApproval_RejectionWorks()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ArtifactApprovalService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Type = ArtifactType.Document,
            Title = "Test Document",
            Content = "Test content",
            Version = 1,
            ApprovalStatus = ApprovalStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreArtifacts.Add(artifact);
        await dbContext.SaveChangesAsync();

        var result = await service.RejectAsync(artifact.Id, "Needs revision");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Approval);
        Assert.Equal((int)ApprovalStatus.Rejected, result.Approval.Status);
    }

    [Fact]
    public async Task ArtifactApproval_CannotReApproveAlreadyApprovedArtifact()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ArtifactApprovalService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Type = ArtifactType.Document,
            Title = "Test Document",
            Content = "Test content",
            Version = 1,
            ApprovalStatus = ApprovalStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreArtifacts.Add(artifact);
        await dbContext.SaveChangesAsync();

        var result = await service.ApproveAsync(artifact.Id, "Already approved");

        Assert.False(result.Succeeded);
        Assert.Equal("approval_conflict", result.ErrorCode);
    }

    [Fact]
    public async Task ArtifactApproval_RequiresArtifactToExist()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ArtifactApprovalService(dbContext, auditWriter);

        var result = await service.ApproveAsync(Guid.NewGuid(), "Test");

        Assert.False(result.Succeeded);
        Assert.Equal("not_found", result.ErrorCode);
    }

    [Fact]
    public async Task ArtifactApproval_RequestRevisionWorks()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ArtifactApprovalService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Type = ArtifactType.Document,
            Title = "Test Document",
            Content = "Test content",
            Version = 1,
            ApprovalStatus = ApprovalStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreArtifacts.Add(artifact);
        await dbContext.SaveChangesAsync();

        var result = await service.RequestRevisionAsync(artifact.Id, "Please revise section 3");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Approval);
        Assert.Equal((int)ApprovalStatus.RevisionRequested, result.Approval.Status);
    }

    #endregion

    #region Helpers

    private static DbContextOptions<CSweetDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private static CSweetDbContext CreateDbContext()
    {
        return new CSweetDbContext(CreateDbContextOptions());
    }

    private static Organization CreateOrganization()
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion
}

public class TestAuditEventWriter : IAuditEventWriter
{
    public Task WriteAsync(string eventType, string entityTypeName, Guid? entityId = null, string? summary = null, string? metadataJson = null, CancellationToken cancellationToken = default)
    {
        // No-op for unit tests
        return Task.CompletedTask;
    }
}
