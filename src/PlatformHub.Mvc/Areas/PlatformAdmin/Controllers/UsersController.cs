using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Areas.PlatformAdmin.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class UsersController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool? active, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var model = await LoadAsync(token, search, active, ct);
        if (model is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Unable to load users, roles, or tenants from the Platform API.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserAccessViewModel model, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();

        if (ModelState.IsValid)
        {
            var created = await api.PostAsync<ManagedUserViewModel>("api/v1/platform/access/users",
                new { model.UserName, model.DisplayName, model.Password, model.BaseRedirectUrl }, ct, token);
            if (created is not null)
            {
                TempData["SuccessMessage"] = $"User '{created.UserName}' created. Assign a role and scope before they access tenant data.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", "User creation failed. Check username uniqueness and your User.Create permission.");
        }

        var current = await LoadAsync(token, null, null, ct);
        if (current is null) return StatusCode(503, "Unable to reload user data.");
        current.UserName = model.UserName;
        current.DisplayName = model.DisplayName;
        current.BaseRedirectUrl = model.BaseRedirectUrl;
        return View("Index", current);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(long id, string displayName, string? baseRedirectUrl, bool isActive, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();

        var result = await api.PutAsync<UserOperationResult>($"api/v1/platform/access/users/{id}",
            new { displayName, baseRedirectUrl, isActive }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] =
            result?.Message ?? "User update failed. Check User.Update permission and the last-Super-Admin protection.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(UserAccessViewModel model, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        if (model.AssignUserId <= 0 || model.RoleId <= 0)
        {
            TempData["ErrorMessage"] = "Select a user and role.";
            return RedirectToAction(nameof(Index));
        }

        var response = await api.PostWithErrorAsync<UserOperationResult>(
            $"api/v1/platform/access/users/{model.AssignUserId}/roles",
            new { roleId = model.RoleId, tenantId = model.TenantId, branchId = model.BranchId, projectId = model.ProjectId },
            ct, token);
        if (response.Value is not null)
            TempData["SuccessMessage"] = response.Value.Message ?? "Role assigned successfully.";
        else
            TempData["ErrorMessage"] = $"Role assignment failed: {response.Error ?? "The API returned no response."}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(long userId, long assignmentId, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var ok = await api.DeleteAsync($"api/v1/platform/access/users/{userId}/roles/{assignmentId}", ct, token);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
            ? "Role assignment removed."
            : "Could not remove assignment. The protected Super Admin assignment cannot be removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(long id, string newPassword, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var result = await api.PostAsync<UserOperationResult>(
            $"api/v1/platform/access/users/{id}/reset-password", new { newPassword }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] =
            result?.Message ?? "Password reset failed. The new password must contain at least 12 characters.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<UserAccessViewModel?> LoadAsync(string token, string? search, bool? active, CancellationToken ct)
    {
        var usersPath = "api/v1/platform/access/users";
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search));
        if (active.HasValue) query.Add("active=" + active.Value.ToString().ToLowerInvariant());
        if (query.Count > 0) usersPath += "?" + string.Join("&", query);

        var users = await api.GetAsync<List<ManagedUserViewModel>>(usersPath, token, ct);
        var roles = await api.GetAsync<List<ManagedRoleViewModel>>("api/v1/platform/access/roles", token, ct);
        var tenants = await api.GetAsync<List<ManagedTenantViewModel>>("api/v1/platform/tenants", token, ct);
        if (users is null || roles is null || tenants is null) return null;

        var branches = new List<ManagedBranchViewModel>();
        var projects = new List<ManagedProjectViewModel>();
        foreach (var tenant in tenants)
        {
            var tenantBranches = await api.GetAsync<List<ManagedBranchViewModel>>(
                $"api/v1/platform/branches?tenantId={tenant.Id}", token, ct);
            var tenantProjects = await api.GetAsync<List<ManagedProjectViewModel>>(
                $"api/v1/platform/projects?tenantId={tenant.Id}", token, ct);
            if (tenantBranches is not null) branches.AddRange(tenantBranches);
            if (tenantProjects is not null) projects.AddRange(tenantProjects);
        }

        return new UserAccessViewModel
        {
            Users = users, Roles = roles.Where(x => x.IsActive).ToList(), Tenants = tenants,
            Branches = branches, Projects = projects, Search = search, ActiveFilter = active
        };
    }

    private bool IsPlatformAdmin() => User.FindFirst("ph_platform_admin")?.Value == "true";
    private string? AccessToken() => User.FindFirst("ph_api_access_token")?.Value;
}
