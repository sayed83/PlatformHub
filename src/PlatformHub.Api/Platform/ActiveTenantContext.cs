namespace PlatformHub.Api.Platform;

/// <summary>
/// Validated request-scoped workspace context. Null means no workspace was selected for this request.
/// This context identifies the scope; it does not replace permission checks.
/// </summary>
public interface IActiveTenantContext
{
    long? TenantId { get; }
    long? BranchId { get; }
    long? ProjectId { get; }
    bool IsResolved { get; }
}

public sealed class ActiveTenantContext : IActiveTenantContext
{
    public ActiveTenantContext() { }

    // Public value constructor makes the immutable scope easy to exercise in isolated tests.
    public ActiveTenantContext(long tenantId, long? branchId = null, long? projectId = null)
    {
        TenantId = tenantId;
        BranchId = branchId;
        ProjectId = projectId;
        IsResolved = tenantId > 0 && (!branchId.HasValue || branchId.Value > 0) &&
            (!projectId.HasValue || projectId.Value > 0);
    }

    public long? TenantId { get; internal set; }
    public long? BranchId { get; internal set; }
    public long? ProjectId { get; internal set; }
    public bool IsResolved { get; internal set; }
}
