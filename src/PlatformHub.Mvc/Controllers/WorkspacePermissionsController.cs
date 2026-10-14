using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
[Route("WorkspacePermissions")]
public sealed class WorkspacePermissionsController(PlatformApiClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string targetType = "role", long? targetId = null, CancellationToken ct = default)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Roles", new { area = "PlatformAdmin" });
        if (targetType is not ("role" or "user")) targetType = "role";
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        if (!long.TryParse(User.FindFirst("ph_active_tenant_id")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId) || tenantId <= 0)
        {
            TempData["ErrorMessage"] = "Select an active workspace first.";
            return RedirectToAction("Index", "Workspace");
        }
        long? ParseScope(string claim) => long.TryParse(User.FindFirst(claim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
        var branchId = ParseScope("ph_active_branch_id");
        var projectId = ParseScope("ph_active_project_id");
        if (!projectId.HasValue)
        {
            TempData["ErrorMessage"] = "Select an active project before managing menu permissions.";
            return RedirectToAction("Index", "Workspace");
        }

        var suffix = $"tenantId={tenantId}" + (branchId.HasValue ? $"&branchId={branchId}" : "") + $"&projectId={projectId}";
        var roles = targetType == "role"
            ? await api.GetAsync<List<WorkspacePermissionTarget>>($"api/v1/workspace/roles?{suffix}", token, ct) ?? new()
            : new List<WorkspacePermissionTarget>();
        var users = targetType == "user"
            ? await api.GetAsync<List<WorkspacePermissionTarget>>($"api/v1/workspace/users?{suffix}", token, ct) ?? new()
            : new List<WorkspacePermissionTarget>();
        var targets = targetType == "role" ? roles : users;
        if (!targetId.HasValue || targets.All(x => x.Id != targetId.Value)) targetId = targets.FirstOrDefault()?.Id;
        var vm = new WorkspacePermissionPageViewModel
        {
            TargetType = targetType, TargetId = targetId, Roles = roles, Users = users,
            ActiveTenantId = tenantId, BranchId = branchId, ProjectId = projectId
        };
        if (targetId.HasValue)
        {
            var query = $"api/v1/workspace/menu-access?{suffix}&targetType={targetType}&targetId={targetId.Value}";
            var result = await api.GetWithStatusAsync<List<WorkspaceMenuPermissionRow>>(query, token, ct);
            if (result.Value is null)
            {
                ViewData["Error"] = result.Error ?? "Unable to load permission matrix. Verify Role.Update or User.Update access.";
                Response.StatusCode = result.StatusCode ?? 403;
            }
            else vm.Menus = result.Value;
        }
        return View("~/Views/WorkspacePermissions/Index.cshtml", vm);
    }

    [HttpPost("delete-menu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMenu(WorkspaceMenuDeleteRequest request, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return Forbid();
        if (request.TargetType is not ("role" or "user") || request.TargetId <= 0 ||
            request.MenuItemId <= 0 || request.DeleteType is not ("soft" or "hard"))
            return BadRequest();

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        if (!long.TryParse(User.FindFirst("ph_active_tenant_id")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId) || tenantId <= 0)
            return Forbid();
        long? ParseScope(string claim) => long.TryParse(User.FindFirst(claim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
        var branchId = ParseScope("ph_active_branch_id");
        var projectId = ParseScope("ph_active_project_id");
        if (!projectId.HasValue) return Forbid();

        var suffix = $"tenantId={tenantId}" + (branchId.HasValue ? $"&branchId={branchId}" : "") + $"&projectId={projectId}";
        var result = await api.PostAsync<WorkspaceMenuDeleteResult>(
            $"api/v1/workspace/menu-items/{request.MenuItemId}/delete?{suffix}",
            new { request.TargetType, request.TargetId, request.DeleteType }, ct, token);

        if (result?.Success == true)
            TempData["SuccessMessage"] = result.Message ?? (request.DeleteType == "soft" ? "Menu deactivated." : "Menu permanently deleted.");
        else
            TempData["ErrorMessage"] = result?.Message ?? "Menu could not be deleted. It may be a seeded menu or have dependent access records.";

        return RedirectToAction(nameof(Index), new { targetType = request.TargetType, targetId = request.TargetId });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(WorkspacePermissionSaveRequest request, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return Forbid();
        if (request.TargetType is not ("role" or "user") || request.TargetId <= 0 || request.MenuItemId <= 0)
            return BadRequest();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        if (!long.TryParse(User.FindFirst("ph_active_tenant_id")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId) || tenantId <= 0)
            return Forbid();
        long? ParseScope(string claim) => long.TryParse(User.FindFirst(claim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
        var branchId = ParseScope("ph_active_branch_id");
        var projectId = ParseScope("ph_active_project_id");
        if (!projectId.HasValue) return Forbid();
        var suffix = $"tenantId={tenantId}" + (branchId.HasValue ? $"&branchId={branchId}" : "") + $"&projectId={projectId}";
        var path = request.TargetType == "role"
            ? $"api/v1/workspace/roles/{request.TargetId}/menu-access?{suffix}"
            : $"api/v1/workspace/users/{request.TargetId}/menu-access?{suffix}";
        var saved = await api.PutAsync<WorkspacePermissionSaveResult>(path,
            new { MenuItemId = request.MenuItemId, IsAllowed = request.IsAllowed }, ct, token);
        if (saved is null)
        {
            TempData["ErrorMessage"] = "Permission was not saved. Check your scoped update permission and try again.";
        }
        else TempData["SuccessMessage"] = "Menu permission saved.";
        return RedirectToAction(nameof(Index), new { targetType = request.TargetType, targetId = request.TargetId });
    }
}

public sealed class WorkspacePermissionTarget
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
public sealed class WorkspaceMenuPermissionRow
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ModuleName { get; set; }
    public long? ParentMenuId { get; set; }
    public bool IsAllowed { get; set; }
    public bool HasExplicitDecision { get; set; }
}
public sealed class WorkspacePermissionPageViewModel
{
    public string TargetType { get; set; } = "role";
    public long? TargetId { get; set; }
    public long ActiveTenantId { get; set; }
    public long? BranchId { get; set; }
    public long? ProjectId { get; set; }
    public List<WorkspacePermissionTarget> Roles { get; set; } = new();
    public List<WorkspacePermissionTarget> Users { get; set; } = new();
    public List<WorkspaceMenuPermissionRow> Menus { get; set; } = new();
}
public sealed class WorkspacePermissionSaveRequest
{
    public string TargetType { get; set; } = "role";
    public long TargetId { get; set; }
    public long MenuItemId { get; set; }
    public bool IsAllowed { get; set; }
}
public sealed class WorkspacePermissionSaveResult
{
    public bool Success { get; set; }
}

public sealed class WorkspaceMenuDeleteRequest
{
    public string TargetType { get; set; } = "role";
    public long TargetId { get; set; }
    public long MenuItemId { get; set; }
    public string DeleteType { get; set; } = "soft";
}
public sealed class WorkspaceMenuDeleteResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
