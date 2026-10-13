namespace PlatformHub.Domain.Entities;

/// <summary>
/// Chart-of-accounts master based on the Inventory repository's AccHeadModel.
/// TenantId replaces ComId because Tenant is the platform's owning company/workspace.
/// </summary>
public sealed class AccountHead : ScopedBusinessEntity
{
    public string AccHeadName { get; set; } = null!;
    public string? AccHeadCode { get; set; }
    public long? ParentAccHeadId { get; set; }
    public AccountHead? ParentAccHead { get; set; }
    public ICollection<AccountHead> Children { get; set; } = new List<AccountHead>();

    public string AccType { get; set; } = null!;
    public string AccCategory { get; set; } = null!;
    public int Level { get; set; }

    public int? TransId { get; set; }
    public string? TransFrom { get; set; }

    public bool IsTaxable { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public bool IsSystemAccount { get; set; }
    public bool IsTaxInclusive { get; set; } = true;
    public bool IsGroup { get; set; }
    public bool IsEditable { get; set; } = true;
    public bool IsDeletable { get; set; } = true;

    public string? PaymentType { get; set; }
    public string? Description { get; set; }
    public string? NatureOfGroup { get; set; }
    public string? AccountPurpose { get; set; }
    public string? CashFlowType { get; set; }
    public int? ReportSequence { get; set; }
    public string? TaxCategory { get; set; }
    public decimal? BudgetAmount { get; set; }
}

public static class AccHeadTypes
{
    public const string Cash = "Cash";
    public const string Bank = "Bank";
    public const string Cheque = "Cheque";
    public const string MFS = "MFS";
}
