using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class RoleService : IRoleService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public RoleService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyList<RoleResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CoreRoles
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureDefaultsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.CoreOrganizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
            return;

        var defaultRoles = new[]
        {
            ("CEO", "Chief Executive Officer", AuthorityLevel.Autonomous),
            ("Operations", "Operations Manager", AuthorityLevel.ExecutionWithApproval),
            ("Finance", "Finance Manager", AuthorityLevel.ExecutionWithApproval),
            ("Marketing", "Marketing Manager", AuthorityLevel.ExecutionWithApproval),
            ("Product", "Product Manager", AuthorityLevel.ExecutionWithApproval),
        };

        var existingNames = await _dbContext.CoreRoles
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.Name)
            .ToHashSetAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var (name, description, authorityLevel) in defaultRoles)
        {
            if (existingNames.Contains(name))
                continue;

            _dbContext.CoreRoles.Add(new Role
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = name,
                Description = description,
                ResponsibilitiesJson = "[]",
                AuthorityLevel = authorityLevel,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RoleResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.CoreRoles
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return role?.ToResponse();
    }

    public async Task<CoreActionResponse> CreateAsync(Guid organizationId, CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.CoreOrganizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
        {
            return Failure("organization_not_found", "Organization was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Failure("validation_error", "Role name is required.");
        }

        var exists = await _dbContext.CoreRoles
            .AnyAsync(x => x.OrganizationId == organizationId && x.Name == request.Name.Trim(), cancellationToken);

        if (exists)
        {
            return Failure("duplicate_role", $"A role with the name '{request.Name}' already exists in this organization.");
        }

        var now = DateTimeOffset.UtcNow;
        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Description = request.Description ?? string.Empty,
            ResponsibilitiesJson = request.ResponsibilitiesJson ?? "[]",
            AuthorityLevel = (AuthorityLevel)request.AuthorityLevel,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.CoreRoles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "role.created",
            "Role",
            role.Id,
            $"Role '{role.Name}' created in organization {organizationId}.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Role created successfully.", Role: role.ToResponse());
    }

    public async Task<CoreActionResponse> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.CoreRoles
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (role is null)
        {
            return Failure("not_found", "Role was not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var exists = await _dbContext.CoreRoles
                .AnyAsync(x => x.OrganizationId == role.OrganizationId && x.Name == request.Name!.Trim() && x.Id != id, cancellationToken);

            if (exists)
            {
                return Failure("duplicate_role", $"A role with the name '{request.Name}' already exists in this organization.");
            }

            role.Name = request.Name.Trim();
        }

        if (!string.IsNullOrEmpty(request.Description))
            role.Description = request.Description;
        if (!string.IsNullOrEmpty(request.ResponsibilitiesJson))
            role.ResponsibilitiesJson = request.ResponsibilitiesJson;
        if (request.AuthorityLevel.HasValue)
            role.AuthorityLevel = (AuthorityLevel)request.AuthorityLevel.Value;

        role.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "role.updated",
            "Role",
            role.Id,
            $"Role '{role.Name}' updated.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Role updated successfully.", Role: role.ToResponse());
    }

    public async Task<CoreActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.CoreRoles
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (role is null)
        {
            return Failure("not_found", "Role was not found.");
        }

        var name = role.Name;
        _dbContext.CoreRoles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "role.deleted",
            "Role",
            role.Id,
            $"Role '{name}' deleted.",
            cancellationToken: cancellationToken);

        return new CoreActionResponse(true, null, "Role deleted successfully.");
    }

    static CoreActionResponse Failure(string errorCode, string message) =>
        new CoreActionResponse(false, errorCode, message);
}
