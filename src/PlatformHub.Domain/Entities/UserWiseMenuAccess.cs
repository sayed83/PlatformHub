namespace PlatformHub.Domain.Entities;

/// <summary>User-specific menu override within one project. No row means inherit role access.</summary>
public sealed class UserWiseMenuAccess
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public long MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public bool IsAllowed { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
