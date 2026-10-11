namespace PlatformHub.Mvc.Models;

public sealed class WorkspaceScopeViewModel
{
    public long Id { get; set; }
    public string TenantCode { get; set; } = "";
    public string TenantName { get; set; } = "";
    public long? BranchId { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public long? ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectName { get; set; }
    public string RoleCode { get; set; } = "";
    public string RoleName { get; set; } = "";
    public string ScopeLabel => string.Join(" · ", new[]
    {
        TenantName,
        BranchName,
        ProjectName
    }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class WorkspaceViewModel
{
    public string DisplayName { get; init; } = "User";
    public string UserName { get; init; } = "";
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>();
    public IReadOnlyList<WorkspaceScopeViewModel> Scopes { get; init; } = Array.Empty<WorkspaceScopeViewModel>();
    public WorkspaceScopeViewModel? SelectedScope { get; init; }
}
