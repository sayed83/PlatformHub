namespace PlatformHub.Domain.Entities;

public sealed class TenantProject
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;
}
