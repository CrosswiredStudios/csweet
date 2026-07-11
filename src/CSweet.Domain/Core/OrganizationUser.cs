namespace CSweet.Domain.Core;

public sealed class OrganizationUser
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public OrganizationPermissionLevel PermissionLevel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
}
