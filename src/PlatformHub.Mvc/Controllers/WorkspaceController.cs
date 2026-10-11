using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
public sealed class WorkspaceController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? scopeKey, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Dashboard", new { area = "PlatformAdmin" });

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        var scopes = await api.GetAsync<List<WorkspaceScopeViewModel>>("api/v1/workspace/scopes", token, ct);
        if (scopes is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Could not load your workspace assignments. Check that the Platform API is running.");

        var selected = string.IsNullOrWhiteSpace(scopeKey)
            ? null
            : scopes.FirstOrDefault(x => string.Equals(x.Key, scopeKey, StringComparison.Ordinal));

        var hasSelection = !string.IsNullOrWhiteSpace(scopeKey);
        if (hasSelection && selected is null)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            ModelState.AddModelError(string.Empty,
                "That tenant, branch, or project is not assigned to your account.");
        }

        IReadOnlyList<WorkspaceMenuItemViewModel> menus = Array.Empty<WorkspaceMenuItemViewModel>();
        if (selected?.ProjectId is > 0)
        {
            var projectMenus = await api.GetAsync<List<WorkspaceMenuItemViewModel>>(
                $"api/v1/navigation/menus?projectId={selected.ProjectId.Value}", token, ct);
            menus = projectMenus ?? Array.Empty<WorkspaceMenuItemViewModel>();
        }

        var permissions = User.FindAll("ph_permission").Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roles = User.FindAll("ph_role").Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var model = new WorkspaceViewModel
        {
            DisplayName = User.FindFirst("ph_display_name")?.Value ?? User.Identity?.Name ?? "User",
            UserName = User.FindFirst("ph_user_name")?.Value ?? "",
            Roles = roles,
            Permissions = permissions,
            Scopes = scopes,
            SelectedScope = selected,
            Menus = menus
        };

        return View(model);
    }
}
