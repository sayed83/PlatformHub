using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
[Route("WorkspaceRoles")]
public sealed class WorkspaceRolesController(PlatformApiClient api) : Controller
{

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkspaceRoleRowViewModel model, CancellationToken ct)
    {
        if (!User.HasClaim("ph_permission", "Role.Create")) return Forbid();
        var scope = ScopeQuery(); if (scope is null) return RedirectToAction("Index", "Workspace");
        var result = await api.PostAsync<WorkspaceRoleRowViewModel>($"api/v1/workspace/roles?{scope}", new { model.Code, model.Name, model.Description, model.BaseRedirectUrl }, ct, User.FindFirst("ph_api_access_token")?.Value);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null ? "Role could not be created. Check Role.Create permission and unique role code." : $"Role '{result.Name}' created.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(WorkspaceRoleRowViewModel model, CancellationToken ct)
    {
        if (!User.HasClaim("ph_permission", "Role.Update")) return Forbid();
        var scope = ScopeQuery(); if (scope is null) return RedirectToAction("Index", "Workspace");
        var result = await api.PutAsync<RoleActionResult>($"api/v1/workspace/roles/{model.Id}?{scope}", new { model.Name, model.Description, model.BaseRedirectUrl }, ct, User.FindFirst("ph_api_access_token")?.Value);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result?.Message ?? "Role could not be updated.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(long id, bool isActive, CancellationToken ct)
    {
        if (!User.HasClaim("ph_permission", isActive ? "Role.Update" : "Role.Deactivate")) return Forbid();
        var scope = ScopeQuery(); if (scope is null) return RedirectToAction("Index", "Workspace");
        var result = await api.PutAsync<RoleActionResult>($"api/v1/workspace/roles/{id}/status?{scope}", new { IsActive = isActive }, ct, User.FindFirst("ph_api_access_token")?.Value);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result?.Message ?? "Role status could not be changed.";
        return RedirectToAction(nameof(Index));
    }
    private string? ScopeQuery()
    {
        if (!long.TryParse(User.FindFirst("ph_active_tenant_id")?.Value, out var tenantId) || tenantId <= 0) return null;
        var branch = long.TryParse(User.FindFirst("ph_active_branch_id")?.Value, out var branchId) && branchId > 0 ? $"&branchId={branchId}" : "";
        var project = long.TryParse(User.FindFirst("ph_active_project_id")?.Value, out var projectId) && projectId > 0 ? $"&projectId={projectId}" : "";
        return $"tenantId={tenantId}{branch}{project}";
    }
    public sealed class RoleActionResult { public string Message { get; set; } = ""; }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Roles", new { area = "PlatformAdmin" });

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        if (!long.TryParse(User.FindFirst("ph_active_tenant_id")?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var tenantId) || tenantId <= 0)
        {
            TempData["ErrorMessage"] = "Select an active workspace before viewing roles.";
            return RedirectToAction("Index", "Workspace");
        }

        long? ParseScope(string claim) =>
            long.TryParse(User.FindFirst(claim)?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

        var branchId = ParseScope("ph_active_branch_id");
        var projectId = ParseScope("ph_active_project_id");
        var query = $"api/v1/workspace/roles?tenantId={tenantId}" +
            (branchId.HasValue ? $"&branchId={branchId.Value}" : "") +
            (projectId.HasValue ? $"&projectId={projectId.Value}" : "");

        var roles = await api.GetAsync<List<WorkspaceRoleRowViewModel>>(query, token, ct);
        if (roles is null)
        {
            TempData["ErrorMessage"] = "Could not load roles for the active workspace. Check Role.View permission and your assigned scope.";
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            roles = roles.Where(r => r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.Code.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        ViewData["Search"] = search ?? "";
        return View("~/Views/WorkspaceRoles/Index.cshtml", roles);
    }
}

public sealed class WorkspaceRoleRowViewModel
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemRole { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? BaseRedirectUrl { get; set; }
    public int UserCount { get; set; }
    public List<string> Permissions { get; set; } = new();
    public List<string> MenuPermissions { get; set; } = new();
}
