namespace PlatformHub.Mvc.Areas.PlatformAdmin.Models;

public sealed class TenantDependencyViewModel
{
    public TenantDependencyTenant Tenant { get; set; } = new();
    public List<TenantDependencyBranch> Branches { get; set; } = new();
    public List<TenantDependencyProject> Projects { get; set; } = new();
    public List<TenantDependencyRoleAssignment> RoleAssignments { get; set; } = new();
}
public sealed class TenantDependencyTenant
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}
public sealed class TenantDependencyBranch
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}
public sealed class TenantDependencyProject
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}
public sealed class TenantDependencyRoleAssignment
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsUserActive { get; set; }
    public long RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public bool IsRoleActive { get; set; }
    public long? TenantId { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public long? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}
