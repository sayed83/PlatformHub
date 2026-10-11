namespace PlatformHub.Domain.Entities;

/// <summary>Role-level default menu access within one project.</summary>
public sealed class RoleWiseMenuAccess
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long RoleId { get; set; }
    public PlatformRole Role { get; set; } = null!;
    public long MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public bool IsAllowed { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
