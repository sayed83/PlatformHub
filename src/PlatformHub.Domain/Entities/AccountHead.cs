namespace PlatformHub.Domain.Entities;

/// <summary>
/// Tenant-scoped chart-of-accounts node. Posting balances belong in ledger transactions,
/// not on the account master record.
/// </summary>
public sealed class AccountHead : ScopedBusinessEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public long? ParentAccountId { get; set; }
    public bool IsControlAccount { get; set; }
}
