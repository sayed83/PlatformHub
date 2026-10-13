namespace PlatformHub.Domain.Entities;

/// <summary>
/// Common ownership and audit fields for tenant-scoped business records.
/// Tenant is the owning workspace; branch and project scopes are optional.
/// </summary>
public abstract class ScopedBusinessEntity
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long? BranchId { get; set; }
    public long? ProjectId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
