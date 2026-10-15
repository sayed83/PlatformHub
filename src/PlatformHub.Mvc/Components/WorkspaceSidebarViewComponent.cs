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

        var result = await api.GetWithStatusAsync<List<WorkspaceMenuItemViewModel>>(
            $"api/v1/navigation/menus?projectId={projectId}", token, cancellationToken);

        if (result.Value is not null)
        {
            context!.Items.Remove("PlatformHub.MenuLoadError");
            return View(result.Value);
        }

        // Do not present an API failure as if the project had no configured menus.
        // A 401 is handled by PlatformApiClient's refresh flow and the MVC session
        // middleware; 403/5xx responses get an actionable message in the sidebar.
        context!.Items["PlatformHub.MenuLoadError"] = result.StatusCode switch
        {
            StatusCodes.Status403Forbidden =>
                "The API denied menu access for the active workspace. Re-select your workspace or ask an administrator to verify project access.",
            StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout =>
                "Workspace menus are temporarily unavailable because the Platform API could not complete the request. Refresh the page shortly.",
            _ => result.Error ?? "Workspace menus could not be loaded. Refresh the page or contact your administrator."
        };
        return View(Array.Empty<WorkspaceMenuItemViewModel>());
    }
}
