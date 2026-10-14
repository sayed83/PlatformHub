namespace PlatformHub.Domain.Entities;

public sealed class PlatformRole
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;
    public string? BaseRedirectUrl { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PlatformPermission
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Module { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemPermission { get; set; } = true;
}

public sealed class PlatformRolePermission
{
    public long RoleId { get; set; }
    public PlatformRole Role { get; set; } = null!;
    public long PermissionId { get; set; }
    public PlatformPermission Permission { get; set; } = null!;
}

public sealed class UserRoleAssignment
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public long RoleId { get; set; }
    public PlatformRole Role { get; set; } = null!;
    public long? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
