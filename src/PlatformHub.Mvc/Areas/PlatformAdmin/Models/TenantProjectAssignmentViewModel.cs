namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class TenantProjectAssignmentViewModel
{
    public long TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public List<long> SelectedProjectIds { get; set; } = new();
    public IReadOnlyList<TenantProjectOptionViewModel> Projects { get; set; } = Array.Empty<TenantProjectOptionViewModel>();
}

public sealed record TenantProjectOptionViewModel(
    long Id, string Code, string Name, string? BaseUrl, bool IsActive, bool IsAssigned);

public sealed class TenantProjectAssignmentResponse
{
    public TenantProjectTenantInfo Tenant { get; set; } = new();
    public List<TenantProjectOptionViewModel> Projects { get; set; } = new();
    public List<long> AssignedProjectIds { get; set; } = new();
}

public sealed class TenantProjectTenantInfo
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
