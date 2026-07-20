using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class HiringServiceTests
{
    [Fact]
    public async Task CurrentStaffWorkflow_RequiresOwnerAndAssignsApprovedRole()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow; var organizationId = Guid.NewGuid(); var applicationUserId = Guid.NewGuid();
        var installationId = Guid.NewGuid(); var workerId = Guid.NewGuid();
        var owner = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, ApplicationUserId = applicationUserId,
            DisplayName = "Owner", EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Owner, CreatedAt = now };
        var employee = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, WorkerId = workerId,
            DisplayName = "Alex", EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Contributor, CreatedAt = now };
        db.CoreOrganizations.Add(new Organization { Id = organizationId, Name = "Example", CreatedAt = now, UpdatedAt = now });
        db.CoreOrganizationUsers.AddRange(owner, employee);
        db.CoreWorkers.Add(new Worker { Id = workerId, OrganizationId = organizationId, Name = "Alex", WorkerType = WorkerType.Human,
            CapabilitiesJson = "[\"operations\"]", IsEnabled = true, CreatedAt = now, UpdatedAt = now });
        var candidate = new WorkforceCandidate { Id = Guid.NewGuid(), OrganizationId = organizationId, Source = "CurrentStaff",
            ExternalCandidateId = workerId.ToString(), DisplayName = "Alex", CapabilitiesJson = "[\"operations\"]",
            Score = .95m, IsHuman = true, IsAvailable = true, ExplanationJson = "{}" };
        db.WorkforceCandidates.Add(candidate);
        await db.SaveChangesAsync();
        var service = new HiringService(db, new OrganizationUserService(db, new TestAuditEventWriter()), new TestAuditEventWriter());
        var recommendation = await service.UpsertRecommendationAsync(organizationId, installationId,
            new("Operations lead", "Own reliable delivery", null, [$"candidate:{candidate.Id:N}"], $"candidate:{candidate.Id:N}", "rec-1"));
        var workflow = await service.StageWorkflowAsync(organizationId, installationId,
            new(recommendation.Id, recommendation.RecommendedCandidateReference, "Operations Lead", null, [], "workflow-1"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConfirmWorkflowAsync(organizationId, workflow.Id,
            Guid.NewGuid(), new("not-owner")));
        var approved = await service.ConfirmWorkflowAsync(organizationId, workflow.Id, applicationUserId, new("owner-approval"));

        Assert.Equal("Approved", approved?.Status);
        var updated = await db.CoreOrganizationUsers.SingleAsync(x => x.Id == employee.Id);
        Assert.Equal("Operations Lead", (await db.CoreRoles.SingleAsync(x => x.Id == updated.RoleId)).Name);
    }

    private static CSweetDbContext CreateDb() => new(new DbContextOptionsBuilder<CSweetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
