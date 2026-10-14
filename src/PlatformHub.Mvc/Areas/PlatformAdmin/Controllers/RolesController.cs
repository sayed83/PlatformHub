using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Areas.PlatformAdmin.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class RolesController(PlatformApiClient api, ILogger<RolesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var rolesResult = await api.GetWithStatusAsync<List<RoleRowViewModel>>(
            "api/v1/platform/access/roles", token, ct);
        var permissionsResult = await api.GetWithStatusAsync<List<PermissionRowViewModel>>(
            "api/v1/platform/access/permissions", token, ct);

        if (rolesResult.StatusCode == StatusCodes.Status401Unauthorized ||
            permissionsResult.StatusCode == StatusCodes.Status401Unauthorized)
        {
            TempData["ErrorMessage"] = "Your Platform API session has expired. Please sign in again.";
            return Redirect("/PlatformAdmin/Account/Login");
        }

        if (rolesResult.Value is null || permissionsResult.Value is null)
        {
            var failedEndpoint = rolesResult.Value is null
                ? "api/v1/platform/access/roles"
                : "api/v1/platform/access/permissions";
            var failedStatus = rolesResult.Value is null
                ? rolesResult.StatusCode
                : permissionsResult.StatusCode;
            var failure = rolesResult.Value is null
                ? rolesResult.Error
                : permissionsResult.Error;

            logger.LogWarning(
                "Roles page could not load required API data. Endpoint={Endpoint}, StatusCode={StatusCode}, Error={Error}, TraceId={TraceId}",
                failedEndpoint, failedStatus, failure, HttpContext.TraceIdentifier);

            TempData["ErrorMessage"] = $"Could not load role management data from {failedEndpoint}. API status: {failedStatus?.ToString() ?? "unavailable"}. {failure}";
            var statusCode = failedStatus is >= 400 and <= 599
                ? failedStatus.Value
                : StatusCodes.Status503ServiceUnavailable;
            return StatusCode(statusCode);
        }

        return View(new RoleManagementViewModel
        {
            Roles = rolesResult.Value,
            Permissions = permissionsResult.Value
        });
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(long id, bool isActive, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var result = await api.PutAsync<ApiOperationResult>($"api/v1/platform/access/roles/{id}/status", new { IsActive = isActive }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result?.Message
            ?? "Role status could not be changed. The role may be protected or your account may lack Role.Update.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> HardDeleteDetails(long id, CancellationToken ct)
    {
        if (!IsPlatformAdmin() || !User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN")) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var result = await api.GetWithStatusAsync<RoleHardDeleteViewModel>($"api/v1/platform/access/roles/{id}/hard-delete-details", token, ct);
        if (result.Value is null)
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not load role deletion details.";
            return RedirectToAction(nameof(Index));
        }
        return View("HardDeleteDetails", result.Value);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(long id, CancellationToken ct)
    {
        if (!IsPlatformAdmin() || !User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN")) return Forbid();
        var token = AccessToken();
        if (token is null) return Challenge();
        var deleted = await api.DeleteAsync($"api/v1/platform/access/roles/{id}", ct, token);
        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
            ? "Role permanently deleted."
            : "Role could not be deleted. Built-in roles and roles with user assignments are protected.";
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
