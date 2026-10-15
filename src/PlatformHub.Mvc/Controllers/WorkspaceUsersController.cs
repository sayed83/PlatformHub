using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
[Route("WorkspaceUsers")]
public sealed class WorkspaceUsersController(PlatformApiClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Users", new { area = "PlatformAdmin" });

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        if (!long.TryParse(User.FindFirst("ph_active_tenant_id")?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var tenantId) || tenantId <= 0)
        {
            TempData["ErrorMessage"] = "Select an active workspace before managing users.";
            return RedirectToAction("Index", "Workspace");
        }

        long? ParseScope(string claim) =>
            long.TryParse(User.FindFirst(claim)?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

        var branchId = ParseScope("ph_active_branch_id");
        var projectId = ParseScope("ph_active_project_id");
        var query = $"api/v1/workspace/users?tenantId={tenantId}" +
            (branchId.HasValue ? $"&branchId={branchId.Value}" : "") +
            (projectId.HasValue ? $"&projectId={projectId.Value}" : "") +
            (!string.IsNullOrWhiteSpace(search) ? $"&search={Uri.EscapeDataString(search.Trim())}" : "");

        var users = await api.GetAsync<List<WorkspaceUserRowViewModel>>(query, token, ct);
        if (users is null)
        {
            TempData["ErrorMessage"] = "Could not load users for the active workspace. Check User.View permission and your assigned scope.";
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var scopeQuery = $"tenantId={tenantId}" +
            (branchId.HasValue ? $"&branchId={branchId.Value}" : "") +
            (projectId.HasValue ? $"&projectId={projectId.Value}" : "");
        var roles = await api.GetAsync<List<WorkspaceAssignableRoleViewModel>>(
            $"api/v1/workspace/users/assignable-roles?{scopeQuery}", token, ct);

        ViewData["Search"] = search ?? "";
        var model = new WorkspaceUsersPageViewModel
        {
            Users = users,
            Roles = roles ?? new List<WorkspaceAssignableRoleViewModel>(),
            CanCreate = roles is not null
        };
        return View("~/Views/WorkspaceUsers/Index.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkspaceUserCreateViewModel model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        if (!long.TryParse(User.FindFirst("ph_active_tenant_id")?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var tenantId) || tenantId <= 0)
            return RedirectToAction("Index", "Workspace");

        long? ParseScope(string claim) =>
            long.TryParse(User.FindFirst(claim)?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
        var branchId = ParseScope("ph_active_branch_id");
        var projectId = ParseScope("ph_active_project_id");
        var query = $"tenantId={tenantId}" +
            (branchId.HasValue ? $"&branchId={branchId.Value}" : "") +
            (projectId.HasValue ? $"&projectId={projectId.Value}" : "");

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Check the required fields. Password must contain at least 12 characters.";
            return RedirectToAction(nameof(Index));
        }

        var response = await api.PostWithErrorAsync<WorkspaceUserCreateResult>(
            $"api/v1/workspace/users?{query}",
            new { model.UserName, model.DisplayName, model.Password, model.RoleId, model.BaseRedirectUrl },
            ct, token);
        if (response.Value is null)
            TempData["ErrorMessage"] = response.Error ?? "User creation failed. Check User.Create and Role.Assign permissions.";
        else
            TempData["SuccessMessage"] = $"User '{response.Value.UserName}' created and assigned to the active workspace.";

        return RedirectToAction(nameof(Index));
    }
}

public sealed class WorkspaceUserRowViewModel
{
    public long Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class WorkspaceUsersPageViewModel
{
    public List<WorkspaceUserRowViewModel> Users { get; set; } = new();
    public List<WorkspaceAssignableRoleViewModel> Roles { get; set; } = new();
    public bool CanCreate { get; set; }
}
public sealed class WorkspaceAssignableRoleViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? TenantId { get; set; }
    public bool IsSystemRole { get; set; }
}
public sealed class WorkspaceUserCreateViewModel
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100)]
    public string UserName { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(200)]
    public string DisplayName { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(12)]
    public string Password { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Range(1, long.MaxValue)]
    public long RoleId { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? BaseRedirectUrl { get; set; }
}
public sealed class WorkspaceUserCreateResult
{
    public long Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
