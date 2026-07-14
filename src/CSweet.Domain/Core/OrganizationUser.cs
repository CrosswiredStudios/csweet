using CSweet.Domain.Setup;

namespace CSweet.Domain.Core;

public sealed class OrganizationUser
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ReportsToOrganizationUserId { get; set; }
    public Guid? RoleId { get; set; }
    public Guid? WorkerId { get; set; }
    public Guid? AgentInstallationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public EmployeeType EmployeeType { get; set; }
    public OrganizationPermissionLevel PermissionLevel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
    public OrganizationUser? ReportsToOrganizationUser { get; set; }
    public Role? Role { get; set; }
    public Worker? Worker { get; set; }
    public AgentInstallation? AgentInstallation { get; set; }
}
