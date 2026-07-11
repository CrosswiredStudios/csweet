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
        return await _dbContext.CoreOrganizationUsers
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.DisplayName)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationUserResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.CoreOrganizationUsers
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

        var now = DateTimeOffset.UtcNow;
        var user = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email,
            PermissionLevel = (OrganizationPermissionLevel)request.PermissionLevel,
            CreatedAt = now
        };

        _dbContext.CoreOrganizationUsers.Add(user);
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
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null)
        {
            return Failure("not_found", "User was not found.");
        }

        var name = user.DisplayName;
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

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
