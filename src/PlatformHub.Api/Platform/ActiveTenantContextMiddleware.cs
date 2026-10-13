using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Platform;

/// <summary>
/// Resolves optional X-Platform-* headers into a validated, per-request tenant context.
/// It never grants access by itself; tenant-facing endpoints must still call IScopedAccessService
/// with the permission required by that operation.
/// </summary>
public sealed class ActiveTenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext http,
        PlatformDbContext db,
        ActiveTenantContext activeContext,
        ILogger<ActiveTenantContextMiddleware> logger)
    {
        var tenantHeader = http.Request.Headers["X-Platform-TenantId"].FirstOrDefault();
        var branchHeader = http.Request.Headers["X-Platform-BranchId"].FirstOrDefault();
        var projectHeader = http.Request.Headers["X-Platform-ProjectId"].FirstOrDefault();
        var anyHeader = tenantHeader is not null || branchHeader is not null || projectHeader is not null;

        if (!anyHeader)
        {
            await next(http);
            return;
        }

        if (http.User.Identity?.IsAuthenticated != true ||
            !long.TryParse(http.User.FindFirst("ph_user_id")?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!TryOptionalPositiveId(tenantHeader, required: true, out var tenantId) ||
            !TryOptionalPositiveId(branchHeader, required: false, out var branchId) ||
            !TryOptionalPositiveId(projectHeader, required: false, out var projectId))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { message = "Workspace scope headers must contain positive numeric IDs; tenant is required." });
            return;
        }

        var isPlatformAdmin = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive && u.IsPlatformAdmin, http.RequestAborted);
        if (isPlatformAdmin)
        {
            // Platform administrators may call platform-wide APIs. A supplied tenant context
            // is still structurally validated below rather than treated as a scope bypass.
        }

        var tenantValid = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.IsActive, http.RequestAborted);
        var branchValid = !branchId.HasValue || await db.Branches.AsNoTracking()
            .AnyAsync(b => b.Id == branchId.Value && b.TenantId == tenantId && b.IsActive, http.RequestAborted);
        var projectValid = !projectId.HasValue || await db.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == projectId.Value && p.IsActive &&
                (p.TenantId == tenantId || db.TenantProjects.Any(tp =>
                    tp.TenantId == tenantId && tp.ProjectId == p.Id && tp.IsEnabled &&
                    tp.Tenant.IsActive)), http.RequestAborted);

        if (!tenantValid || !branchValid || !projectValid)
        {
            logger.LogWarning("Active workspace rejected. UserId={UserId}, TenantId={TenantId}, BranchId={BranchId}, ProjectId={ProjectId}, Reason={Reason}",
                userId, tenantId, branchId, projectId, "Inactive or inconsistent tenant/project/branch.");
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            await http.Response.WriteAsJsonAsync(new { message = "The selected workspace is no longer active or is inconsistent." });
            return;
        }

        if (!isPlatformAdmin)
        {
            var assignmentExists = await db.UserRoleAssignments.AsNoTracking()
                .AnyAsync(a => a.UserId == userId && a.Role.IsActive &&
                    (a.TenantId == tenantId ||
                     (a.TenantId == null && branchId.HasValue && a.BranchId == branchId.Value) ||
                     (a.TenantId == null && projectId.HasValue && a.ProjectId == projectId.Value)) &&
                    (a.BranchId == null || (branchId.HasValue && a.BranchId == branchId.Value)) &&
                    (a.ProjectId == null || (projectId.HasValue && a.ProjectId == projectId.Value)),
                    http.RequestAborted);

            if (!assignmentExists)
            {
                logger.LogWarning("Active workspace rejected. UserId={UserId}, TenantId={TenantId}, BranchId={BranchId}, ProjectId={ProjectId}, Reason={Reason}",
                    userId, tenantId, branchId, projectId, "No matching active role assignment.");
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                await http.Response.WriteAsJsonAsync(new { message = "You are not assigned to the selected workspace." });
                return;
            }
        }

        activeContext.TenantId = tenantId;
        activeContext.BranchId = branchId;
        activeContext.ProjectId = projectId;
        activeContext.IsResolved = true;
        http.Items[typeof(IActiveTenantContext)] = activeContext;
        await next(http);
    }

    private static bool TryOptionalPositiveId(string? value, bool required, out long? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(value)) return !required;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            return false;
        id = parsed;
        return true;
    }
}
