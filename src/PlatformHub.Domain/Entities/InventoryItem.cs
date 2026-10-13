namespace PlatformHub.Domain.Entities;

/// <summary>
/// Tenant/workspace-owned inventory catalogue item. Stock quantities and accounting
/// postings belong in later stock-movement and voucher modules, not this catalogue row.
/// </summary>
public sealed class InventoryItem
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long? BranchId { get; set; }
    public long? ProjectId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
