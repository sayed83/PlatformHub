namespace PlatformHub.Domain.Entities;

/// <summary>
/// Links a tenant's system account purpose (such as SALES or CASH) to an account.
/// TenantId replaces the Inventory repository's ComId; branch scope remains optional.
/// </summary>
public sealed class SystemAccHeadSetting
{
    public long Id { get; set; }
    public long AccHeadId { get; set; }
    public AccountHead AccHead { get; set; } = null!;
    public string SystemName { get; set; } = null!;
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public long TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public long CreatedBy { get; set; }
    public AppUser User { get; set; } = null!;
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}
