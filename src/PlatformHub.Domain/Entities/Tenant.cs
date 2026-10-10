namespace PlatformHub.Domain.Entities;

public sealed class Tenant
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
