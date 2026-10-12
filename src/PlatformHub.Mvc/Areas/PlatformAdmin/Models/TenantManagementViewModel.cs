using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class TenantManagementViewModel
{
    public IReadOnlyList<TenantViewModel> Tenants { get; init; } = Array.Empty<TenantViewModel>();
    public IReadOnlySet<long> CanHardDeleteTenantIds { get; init; } = new HashSet<long>();

    [Required(ErrorMessage = "Tenant code is required.")]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Use letters, numbers, hyphens, or underscores for the tenant code.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tenant name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
}
