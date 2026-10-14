using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class RoleManagementViewModel
{
    public List<RoleRowViewModel> Roles { get; set; } = [];
    public List<PermissionRowViewModel> Permissions { get; set; } = [];

    [Required, StringLength(100)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, hyphens, or underscores.")]
    public string Code { get; set; } = "";

    [Required, StringLength(150)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? BaseRedirectUrl { get; set; }

    public long EditingRoleId { get; set; }
    public List<long> SelectedPermissionIds { get; set; } = [];
}

public sealed class RoleRowViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; }
    public string? BaseRedirectUrl { get; set; }
    public List<string> Permissions { get; set; } = [];
}

public sealed class PermissionRowViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Module { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemPermission { get; set; }
}

public sealed class RoleHardDeleteViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; }
    public string? BaseRedirectUrl { get; set; }
    public List<RoleAssignmentDetailViewModel> Assignments { get; set; } = [];
    public List<RolePermissionDetailViewModel> Permissions { get; set; } = [];
    public List<RoleMenuAccessDetailViewModel> MenuAccess { get; set; } = [];
    public bool CanDelete { get; set; }
}
public sealed class RoleAssignmentDetailViewModel
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long? TenantId { get; set; }
    public string? TenantName { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public long? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}
public sealed class RolePermissionDetailViewModel
{
    public long PermissionId { get; set; }
    public string Code { get; set; } = "";
    public string PermissionName { get; set; } = "";
    public string Module { get; set; } = "";
}
public sealed class RoleMenuAccessDetailViewModel
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public long MenuItemId { get; set; }
    public string MenuName { get; set; } = "";
    public bool IsAllowed { get; set; }
}
