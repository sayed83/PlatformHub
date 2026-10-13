using PlatformHub.Mvc.Models;
using PlatformHub.Mvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace PlatformHub.Mvc.Components;

public sealed class WorkspaceSidebarViewComponent(PlatformApiClient api, IHttpContextAccessor contextAccessor) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken = default)
    {
        var context = contextAccessor.HttpContext;
        var user = context?.User;
        if (user?.Identity?.IsAuthenticated != true ||
            user.FindFirst("ph_platform_admin")?.Value == "true")
            return View(Array.Empty<WorkspaceMenuItemViewModel>());

        var token = user.FindFirst("ph_api_access_token")?.Value;
        var projectValue = user.FindFirst("ph_active_project_id")?.Value;
        if (string.IsNullOrWhiteSpace(token) ||
            !long.TryParse(projectValue, out var projectId) || projectId <= 0)
            return View(Array.Empty<WorkspaceMenuItemViewModel>());

        var menus = await api.GetAsync<List<WorkspaceMenuItemViewModel>>(
            $"api/v1/navigation/menus?projectId={projectId}", token, cancellationToken);

        return View(menus ?? new List<WorkspaceMenuItemViewModel>());
    }
}
