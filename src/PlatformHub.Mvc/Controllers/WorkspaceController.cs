using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
public sealed class WorkspaceController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? scopeIndex, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Dashboard", new { area = "PlatformAdmin" });

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        var scopes = await api.GetAsync<List<WorkspaceScopeViewModel>>("api/v1/workspace/scopes", token, ct);
        if (scopes is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Could not load your workspace assignments. Check that the Platform API is running.");

        var selected = scopeIndex.HasValue && scopeIndex.Value >= 0 && scopeIndex.Value < scopes.Count
            ? scopes[scopeIndex.Value]
            : null;

        var hasSelection = scopeIndex.HasValue;
        if (hasSelection && selected is null)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            ModelState.AddModelError(string.Empty,
                "That tenant, branch, or project is not assigned to your account.");
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
            SelectedScope = selected
        };

        return View(model);
    }
}
