using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Areas.PlatformAdmin.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class RolesController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var model = await LoadAsync(token, ct);
        if (model is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleManagementViewModel model, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();

        if (ModelState.IsValid)
        {
            var created = await api.PostAsync<RoleRowViewModel>(
                "api/v1/platform/access/roles",
                new { model.Code, model.Name, model.Description, model.BaseRedirectUrl },
                ct, token);
            if (created is not null)
            {
                TempData["SuccessMessage"] = $"Role '{created.Name}' created.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty,
                "Role could not be created. Check the code is unique and your account has Role.Create permission.");
        }

        var current = await LoadAsync(token, ct);
        if (current is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        current.Code = model.Code;
        current.Name = model.Name;
        current.Description = model.Description;
        current.BaseRedirectUrl = model.BaseRedirectUrl;
        return View("Index", current);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePermissions(RoleManagementViewModel model, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();

        var result = await api.PutAsync<ApiOperationResult>(
            $"api/v1/platform/access/roles/{model.EditingRoleId}/permissions",
            new { permissionIds = model.SelectedPermissionIds ?? [] },
            ct, token);
        if (result is not null)
            TempData["SuccessMessage"] = "Role permissions updated. Users must sign in again to receive refreshed permission claims.";
        else
            TempData["ErrorMessage"] = "Permissions could not be updated. Built-in roles are protected; verify the selected role and your Role.Update permission.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<RoleManagementViewModel?> LoadAsync(string token, CancellationToken ct)
    {
        var roles = await api.GetAsync<List<RoleRowViewModel>>("api/v1/platform/access/roles", token, ct);
        var permissions = await api.GetAsync<List<PermissionRowViewModel>>("api/v1/platform/access/permissions", token, ct);
        if (roles is null || permissions is null) return null;
        return new RoleManagementViewModel { Roles = roles, Permissions = permissions };
    }

    private bool IsPlatformAdmin() => User.FindFirst("ph_platform_admin")?.Value == "true";
    private string? AccessToken() => User.FindFirst("ph_api_access_token")?.Value;
    public sealed record ApiOperationResult(string Message, long RoleId, int PermissionCount);
}
