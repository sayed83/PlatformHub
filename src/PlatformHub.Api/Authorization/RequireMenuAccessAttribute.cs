using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PlatformHub.Api.Services;
using PlatformHub.Api.Platform;

namespace PlatformHub.Api.Authorization;

/// <summary>
/// Enforces a project's effective menu permission on the API endpoint itself.
/// Apply to each protected application action/controller with its stable MenuItem.Code.
/// Requires projectId in the query string or X-Project-Id header; the project must also
/// be represented by the caller's assigned role scope.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireMenuAccessAttribute(string menuCode) : Attribute, IAsyncAuthorizationFilter
{
    public string MenuCode { get; } = menuCode;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!long.TryParse(user.FindFirst("ph_user_id")?.Value, out var userId) || userId <= 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        var request = context.HttpContext.Request;
        var rawProjectId = request.Query["projectId"].FirstOrDefault()
            ?? request.Headers["X-Project-Id"].FirstOrDefault()
            ?? user.FindFirst("ph_project_id")?.Value;

        if (!long.TryParse(rawProjectId, out var projectId) || projectId <= 0)
        {
            context.Result = new BadRequestObjectResult(new
            {
                message = "A projectId query parameter or X-Project-Id header is required for menu authorization."
            });
            return;
        }

        var activeTenant = context.HttpContext.RequestServices.GetRequiredService<IActiveTenantContext>();
        if (!activeTenant.IsResolved || activeTenant.TenantId is not > 0 ||
            (activeTenant.ProjectId.HasValue && activeTenant.ProjectId.Value != projectId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var access = context.HttpContext.RequestServices.GetRequiredService<EffectiveMenuAccessService>();
        if (!await access.HasAccessAsync(userId, projectId, activeTenant.TenantId.Value, MenuCode, context.HttpContext.RequestAborted))
            context.Result = new ForbidResult();
    }
}
