using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
public sealed class WorkspaceController(PlatformApiClient api) : Controller
{
    private const string ActiveTenant = "ph_active_tenant_id";
    private const string ActiveBranch = "ph_active_branch_id";
    private const string ActiveProject = "ph_active_project_id";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Dashboard", new { area = "PlatformAdmin" });

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        var scopes = await api.GetAsync<List<WorkspaceScopeViewModel>>("api/v1/workspace/scopes", token, ct);
        if (scopes is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var tenantId = ParseClaim(ActiveTenant);
        var branchId = ParseClaim(ActiveBranch);
        var projectId = ParseClaim(ActiveProject);
        var selected = tenantId.HasValue
            ? scopes.FirstOrDefault(x => x.Id == tenantId.Value &&
                x.BranchId == branchId && x.ProjectId == projectId)
            : null;

        // A persisted context can become invalid after an administrator removes an assignment.
        // Never keep sending a stale scope; require the user to select a currently valid one again.
        if (tenantId.HasValue && selected is null)
        {
            await ReplaceActiveScopeClaimsAsync(null, null, null);
            ModelState.AddModelError(string.Empty,
                "Your previous workspace is no longer assigned or active. Please select a valid workspace.");
        }

        IReadOnlyList<WorkspaceMenuItemViewModel> menus = Array.Empty<WorkspaceMenuItemViewModel>();
        if (selected?.ProjectId is > 0)
        {
            var projectMenus = await api.GetAsync<List<WorkspaceMenuItemViewModel>>(
                $"api/v1/navigation/menus?projectId={selected.ProjectId.Value}", token, ct);
            menus = projectMenus ?? new List<WorkspaceMenuItemViewModel>();
        }

        var permissions = User.FindAll("ph_permission").Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roles = User.FindAll("ph_role").Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return View(new WorkspaceViewModel
        {
            DisplayName = User.FindFirst("ph_display_name")?.Value ?? User.Identity?.Name ?? "User",
            UserName = User.FindFirst("ph_user_name")?.Value ?? "",
            Roles = roles,
            Permissions = permissions,
            Scopes = scopes,
            SelectedScope = selected,
            Menus = menus
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string scopeKey, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return Forbid();

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        var scopes = await api.GetAsync<List<WorkspaceScopeViewModel>>("api/v1/workspace/scopes", token, ct);
        if (scopes is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);

        // The submitted key is untrusted. Accept it only if it exactly matches a scope
        // freshly returned by the API for this authenticated user.
        var selected = scopes.FirstOrDefault(x => string.Equals(x.Key, scopeKey, StringComparison.Ordinal));
        if (selected is null)
        {
            ModelState.AddModelError(string.Empty, "That workspace is not assigned to your account.");
            return await Index(ct);
        }

        await ReplaceActiveScopeClaimsAsync(selected.Id, selected.BranchId, selected.ProjectId);
        return RedirectToAction(nameof(Index));
    }

    private long? ParseClaim(string type) =>
        long.TryParse(User.FindFirst(type)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id : null;

    private async Task ReplaceActiveScopeClaimsAsync(long? tenantId, long? branchId, long? projectId)
    {
        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(User.Identity);
        foreach (var type in new[] { ActiveTenant, ActiveBranch, ActiveProject })
            foreach (var claim in identity.FindAll(type).ToArray())
                identity.RemoveClaim(claim);

        if (tenantId.HasValue) identity.AddClaim(new Claim(ActiveTenant, tenantId.Value.ToString(CultureInfo.InvariantCulture)));
        if (branchId.HasValue) identity.AddClaim(new Claim(ActiveBranch, branchId.Value.ToString(CultureInfo.InvariantCulture)));
        if (projectId.HasValue) identity.AddClaim(new Claim(ActiveProject, projectId.Value.ToString(CultureInfo.InvariantCulture)));

        var principal = new ClaimsPrincipal(identity);
        HttpContext.User = principal;
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            auth.Properties ?? new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }
}
