using PlatformHub.Mvc.Models;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class DashboardViewModel
{
    public string DisplayName { get; init; } = "Administrator";
    public PlatformSummaryViewModel Summary { get; init; } = new();
    public IReadOnlyList<TenantViewModel> Tenants { get; init; } = Array.Empty<TenantViewModel>();
}

public sealed class PlatformSummaryViewModel
{
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int TotalTenants { get; init; }
    public int ActiveTenants { get; init; }
    public int TotalProjects { get; init; }
    public int ActiveProjects { get; init; }
}

public sealed record TenantViewModel(long Id, string Code, string Name, bool IsActive);
