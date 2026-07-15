using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class OrganizationUserService : IOrganizationUserService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public OrganizationUserService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyList<OrganizationUserResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.CoreOrganizationUsers
            .Where(x => x.OrganizationId == organizationId)
            .Include(x => x.AgentInstallation!)
                .ThenInclude(x => x.Grant)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
        return users.Select(x => x.ToResponse()).ToList();
    }

    public async Task<OrganizationUserResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.CoreOrganizationUsers
            .Include(x => x.AgentInstallation!)
                .ThenInclude(x => x.Grant)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return user?.ToResponse();
    }

    public async Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateOrganizationUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.CoreOrganizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
        {
            return Failure("organization_not_found", "Organization was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Failure("validation_error", "Display name is required.");
        }

        if (!Enum.IsDefined(typeof(EmployeeType), request.EmployeeType))
        {
            return Failure("validation_error", "Employee type is invalid.");
        }

        if (request.EmployeeType == (int)EmployeeType.Agent && !request.AgentInstallationId.HasValue)
        {
            return Failure("agent_instance_required", "An imported agent installation must be selected for an agent employee.");
        }

        if (request.AgentInstallationId.HasValue)
        {
            var installationAvailable = await _dbContext.AgentInstallations.AnyAsync(
                x => x.Id == request.AgentInstallationId && x.IsEnabled,
                cancellationToken);
            if (!installationAvailable)
            {
                return Failure("invalid_agent_instance", "The selected agent installation is not available.");
            }
        }

        if (request.ReportsToOrganizationUserId.HasValue)
        {
            var managerExists = await _dbContext.CoreOrganizationUsers
                .AnyAsync(x => x.Id == request.ReportsToOrganizationUserId && x.OrganizationId == organizationId, cancellationToken);

            if (!managerExists)
            {
                return Failure("invalid_manager", "Reporting manager must belong to the same organization.");
            }
        }

        var managedUserIds = (request.ManagedOrganizationUserIds ?? [])
            .Distinct()
            .ToArray();

        if (request.ReportsToOrganizationUserId.HasValue && managedUserIds.Contains(request.ReportsToOrganizationUserId.Value))
        {
            return Failure("invalid_hierarchy", "An employee cannot both manage and report to the same person.");
        }

        var managedUsers = managedUserIds.Length == 0
            ? []
            : await _dbContext.CoreOrganizationUsers
                .Where(x => managedUserIds.Contains(x.Id) && x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);

        if (managedUsers.Count != managedUserIds.Length)
        {
            return Failure("invalid_subordinate", "Every managed employee must belong to the same organization.");
        }

        if (request.RoleId.HasValue)
        {
            var roleExists = await _dbContext.CoreRoles
                .AnyAsync(x => x.Id == request.RoleId && x.OrganizationId == organizationId, cancellationToken);

            if (!roleExists)
            {
                return Failure("invalid_role", "Role must belong to the same organization.");
            }
        }

        if (request.WorkerId.HasValue)
        {
            var worker = await _dbContext.CoreWorkers
                .SingleOrDefaultAsync(x => x.Id == request.WorkerId && (x.OrganizationId == organizationId || x.OrganizationId == null), cancellationToken);

            if (worker is null)
            {
                return Failure("invalid_worker", "Worker must belong to the same organization or be global.");
            }

            if (request.EmployeeType == (int)EmployeeType.Agent &&
                (!worker.IsEnabled || !IsAgentWorkerType(worker.WorkerType)))
            {
                return Failure("invalid_agent", "The selected worker is not an available agent.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var user = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ReportsToOrganizationUserId = request.ReportsToOrganizationUserId,
            RoleId = request.RoleId,
            WorkerId = request.WorkerId,
            AgentInstallationId = request.AgentInstallationId,
            DisplayName = request.DisplayName.Trim(),
            Email = TrimOrNull(request.Email),
            EmployeeType = (EmployeeType)request.EmployeeType,
            PermissionLevel = (OrganizationPermissionLevel)request.PermissionLevel,
            CreatedAt = now
        };

        _dbContext.CoreOrganizationUsers.Add(user);
        foreach (var managedUser in managedUsers)
        {
            managedUser.ReportsToOrganizationUserId = user.Id;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "organization_user.created",
            "OrganizationUser",
            user.Id,
            $"User '{user.DisplayName}' added to organization {organizationId}.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "User added successfully.", OrganizationUser: user.ToResponse());
    }

    public async Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.CoreOrganizationUsers
            .Include(x => x.AgentInstallation!)
                .ThenInclude(x => x.Grant)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null)
        {
            return Failure("not_found", "User was not found.");
        }

        if (user.ApplicationUserId.HasValue || string.Equals(user.DisplayName.Trim(), "Self", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("cannot_delete_self", "The administrator membership cannot be removed.");
        }

        var name = user.DisplayName;
        var directReports = await _dbContext.CoreOrganizationUsers
            .Where(x => x.ReportsToOrganizationUserId == id)
            .ToListAsync(cancellationToken);
        foreach (var directReport in directReports)
        {
            directReport.ReportsToOrganizationUserId = null;
        }

        // Conversations require a live agent user. A hard-fired employee cannot remain
        // referenced by them, so remove their conversation history in the same save.
        var conversations = await _dbContext.CoreConversations
            .Where(x => x.AgentOrganizationUserId == id)
            .ToListAsync(cancellationToken);
        _dbContext.CoreConversations.RemoveRange(conversations);

        _dbContext.CoreOrganizationUsers.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "organization_user.deleted",
            "OrganizationUser",
            user.Id,
            $"User '{name}' removed from organization.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "User removed successfully.");
    }

    public async Task<CoreActionResponse> UpdateRoleAsync(
        Guid organizationId,
        Guid id,
        UpdateOrganizationUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.CoreOrganizationUsers
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, cancellationToken);
        if (user is null)
        {
            return Failure("not_found", "User was not found.");
        }

        if (request.RoleId.HasValue && !await _dbContext.CoreRoles.AnyAsync(
                x => x.Id == request.RoleId.Value && x.OrganizationId == organizationId,
                cancellationToken))
        {
            return Failure("invalid_role", "Role must belong to the same organization.");
        }

        user.RoleId = request.RoleId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditEventWriter.WriteAsync(
            "organization_user.role_updated",
            "OrganizationUser",
            user.Id,
            $"User '{user.DisplayName}' changed company role.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Role updated successfully.", OrganizationUser: user.ToResponse());
    }

    static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static bool IsAgentWorkerType(WorkerType workerType) => workerType is
        WorkerType.LocalAgent or
        WorkerType.RemoteAgent or
        WorkerType.MarketplaceProxy or
        WorkerType.BuiltInSystem;

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
