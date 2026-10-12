using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class ProjectManagementViewModel
{
    public IReadOnlyList<ProjectRowViewModel> Projects { get; init; } = Array.Empty<ProjectRowViewModel>();
    public IReadOnlyList<TenantViewModel> Tenants { get; init; } = Array.Empty<TenantViewModel>();

    public long? TenantId { get; set; }

    [Required(ErrorMessage = "Project code is required.")]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Use letters, numbers, hyphens, or underscores.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Project name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Url(ErrorMessage = "Enter a valid absolute URL, including https://, or leave it empty.")]
    [StringLength(500)]
    public string? BaseUrl { get; set; }
}

public sealed record ProjectRowViewModel(
    long Id, long? TenantId, string? TenantName, string Code, string Name, string? BaseUrl, bool IsActive);
