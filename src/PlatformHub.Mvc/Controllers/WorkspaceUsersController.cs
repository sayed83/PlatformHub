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

        ViewData["Search"] = search ?? "";
        return View("~/Views/WorkspaceUsers/Index.cshtml", users);
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
