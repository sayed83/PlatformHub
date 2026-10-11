using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class UserAccessViewModel
{
    public List<ManagedUserViewModel> Users { get; set; } = [];
    public List<ManagedRoleViewModel> Roles { get; set; } = [];
    public List<ManagedTenantViewModel> Tenants { get; set; } = [];
    public string? Search { get; set; }
    public bool? ActiveFilter { get; set; }

    [Required, StringLength(100)]
    [RegularExpression(@"^[A-Za-z0-9._@-]+$", ErrorMessage = "Username may contain letters, numbers, dots, underscores, @ and hyphens.")]
    public string UserName { get; set; } = "";

    [Required, StringLength(200)]
    public string DisplayName { get; set; } = "";

    [Required, MinLength(12), StringLength(200)]
    public string Password { get; set; } = "";

    [StringLength(500)]
    public string? BaseRedirectUrl { get; set; }

    public long AssignUserId { get; set; }
    public long RoleId { get; set; }
    public long? TenantId { get; set; }
    public long? BranchId { get; set; }
    public long? ProjectId { get; set; }
}

public sealed class ManagedUserViewModel
{
    public long Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public string? BaseRedirectUrl { get; set; }
    public DateTime CreatedUtc { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<UserRoleAssignmentViewModel> RoleAssignments { get; set; } = [];
}

public sealed class UserRoleAssignmentViewModel
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public string RoleCode { get; set; } = "";
    public string RoleName { get; set; } = "";
    public long? TenantId { get; set; }
    public string? TenantName { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public long? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}

public sealed class ManagedRoleViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class ManagedTenantViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed record UserOperationResult(string Message);
