using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class CoreOrganizationService : ICoreOrganizationService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IRoleService _roleService;

    public CoreOrganizationService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter, IRoleService roleService)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
        _roleService = roleService;
    }

    public async Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CoreOrganizations
            .OrderBy(x => x.Name)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.CoreOrganizations
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return org?.ToResponse();
    }

    public async Task<CoreActionResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Failure("validation_error", "Organization name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Industry = TrimOrNull(request.Industry),
            Mission = TrimOrNull(request.Mission),
            Stage = TrimOrNull(request.Stage),
            PrimaryGoal = TrimOrNull(request.PrimaryGoal),
            ConstraintsJson = request.ConstraintsJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.CoreOrganizations.Add(org);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Seed default roles for new organization
        await _roleService.EnsureDefaultsAsync(org.Id, cancellationToken);
        await SeedFoundingEmployeesAsync(org.Id, request.ConstraintsJson, cancellationToken);

        await _auditEventWriter.WriteAsync(
            "organization.created",
            "Organization",
            org.Id,
            $"Organization '{org.Name}' created.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Organization created successfully.", Organization: org.ToResponse());
    }

    public async Task<CoreActionResponse> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.CoreOrganizations
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (org is null)
        {
            return Failure("not_found", "Organization was not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            org.Name = request.Name.Trim();
        if (request.Industry is not null)
            org.Industry = TrimOrNull(request.Industry);
        if (request.Mission is not null)
            org.Mission = TrimOrNull(request.Mission);
        if (request.Stage is not null)
            org.Stage = TrimOrNull(request.Stage);
        if (request.PrimaryGoal is not null)
            org.PrimaryGoal = TrimOrNull(request.PrimaryGoal);
        if (request.ConstraintsJson is not null)
            org.ConstraintsJson = request.ConstraintsJson;

        org.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "organization.updated",
            "Organization",
            org.Id,
            $"Organization '{org.Name}' updated.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Organization updated successfully.", Organization: org.ToResponse());
    }

    public async Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.CoreOrganizations
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (org is null)
        {
            return Failure("not_found", "Organization was not found.");
        }

        var name = org.Name;
        _dbContext.CoreOrganizations.Remove(org);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "organization.deleted",
            "Organization",
            org.Id,
            $"Organization '{name}' deleted.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Organization deleted successfully.");
    }

    static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task SeedFoundingEmployeesAsync(Guid organizationId, string? assistantContextJson, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var ceoRoleId = await _dbContext.CoreRoles
            .Where(x => x.OrganizationId == organizationId && x.Name == "CEO")
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var ceo = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            RoleId = ceoRoleId,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = now
        };

        var assistantWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Personal Assistant",
            Description = "Dedicated personal assistant agent for the organization's CEO.",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[\"personal-assistant\",\"chief-of-staff\",\"coordination\",\"executive-support\"]",
            EndpointConfigurationJson = TrimOrNull(assistantContextJson),
            IsEnabled = true,
            RequiresHumanApproval = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var assistant = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ReportsToOrganizationUserId = ceo.Id,
            WorkerId = assistantWorker.Id,
            DisplayName = "Personal Assistant",
            EmployeeType = EmployeeType.Agent,
            PermissionLevel = OrganizationPermissionLevel.Contributor,
            CreatedAt = now
        };

        _dbContext.CoreOrganizationUsers.Add(ceo);
        _dbContext.CoreWorkers.Add(assistantWorker);
        _dbContext.CoreOrganizationUsers.Add(assistant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
