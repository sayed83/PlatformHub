namespace PlatformHub.Domain.Entities;

public sealed class Project
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? BaseUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
