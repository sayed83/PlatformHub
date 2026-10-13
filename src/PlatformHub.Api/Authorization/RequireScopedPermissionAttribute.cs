using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PlatformHub.Api.Platform;
using PlatformHub.Infrastructure.Platform;

namespace PlatformHub.Api.Authorization;

/// <summary>
/// Enforces a database-backed permission against the validated active workspace.
/// Apply to tenant-facing business-data actions; do not apply to platform-wide administration.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireScopedPermissionAttribute(string permissionCode) : Attribute, IAsyncAuthorizationFilter
{
    public string PermissionCode { get; } = permissionCode;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var user = http.User;
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

        var scope = http.RequestServices.GetRequiredService<IActiveTenantContext>();
        if (!scope.IsResolved || scope.TenantId is not > 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Client-supplied IDs can only confirm the active scope, never switch it.
        if (!TryOptionalId(http.Request.Query["branchId"].FirstOrDefault()
                ?? http.Request.Headers["X-Branch-Id"].FirstOrDefault(), out var requestedBranch)
            || !TryOptionalId(http.Request.Query["projectId"].FirstOrDefault()
                ?? http.Request.Headers["X-Project-Id"].FirstOrDefault(), out var requestedProject))
        {
            context.Result = new BadRequestObjectResult(new { message = "Branch and project IDs must be positive integers." });
            return;
        }

        if ((requestedBranch.HasValue && requestedBranch != scope.BranchId)
            || (requestedProject.HasValue && requestedProject != scope.ProjectId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var access = http.RequestServices.GetRequiredService<IScopedAccessService>();
        var allowed = await access.CanAccessAsync(
            userId, PermissionCode, scope.TenantId.Value,
            scope.BranchId, scope.ProjectId, http.RequestAborted);
        if (!allowed)
            context.Result = new ForbidResult();
    }

    private static bool TryOptionalId(string? raw, out long? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!long.TryParse(raw, out var parsed) || parsed <= 0) return false;
        value = parsed;
        return true;
    }
}