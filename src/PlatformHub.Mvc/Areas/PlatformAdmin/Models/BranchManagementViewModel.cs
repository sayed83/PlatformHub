using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class BranchManagementViewModel
{
    public long TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public IReadOnlyList<BranchRowViewModel> Branches { get; init; } = Array.Empty<BranchRowViewModel>();

    [Required(ErrorMessage = "Branch code is required.")]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Use letters, numbers, hyphens, or underscores.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Branch name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Address { get; set; }
}

public sealed record BranchRowViewModel(
    long Id, long TenantId, string TenantCode, string TenantName, string Code, string Name, string? Address, bool IsActive);
