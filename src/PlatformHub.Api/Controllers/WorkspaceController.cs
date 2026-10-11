using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;

namespace PlatformHub.Api.Controllers;

/// <summary>
/// Example tenant-scoped workspace endpoint. Business endpoints should call
/// IScopedAccessService with their own permission and resource scope before returning data.
/// </summary>
[ApiController]
[Route("api/v1/workspace")]
[Authorize]
public sealed class WorkspaceController(
    PlatformDbContext db,
    IScopedAccessService scopedAccess) : ControllerBase
{

    [HttpGet("scopes")]
    public async Task<IActionResult> GetAvailableScopes(CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(AppClaimTypes.UserId)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Unauthorized();

        var user = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new { x.Id, x.IsPlatformAdmin })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.IsPlatformAdmin) return Forbid();

        var assignments = await db.UserRoleAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.Role.IsActive)
            .Select(x => new
            {
                x.TenantId, x.BranchId, x.ProjectId,
                RoleCode = x.Role.Code, RoleName = x.Role.Name
            })
            .ToListAsync(cancellationToken);

        var branchIds = assignments.Where(x => x.BranchId.HasValue)
            .Select(x => x.BranchId!.Value).Distinct().ToArray();
        var branches = await db.Branches.AsNoTracking()
            .Where(x => branchIds.Contains(x.Id) && x.IsActive)
            .Select(x => new { x.Id, x.TenantId, x.Code, x.Name })
            .ToListAsync(cancellationToken);

        var projectIds = assignments.Where(x => x.ProjectId.HasValue)
            .Select(x => x.ProjectId!.Value).Distinct().ToArray();
        var projects = await db.Projects.AsNoTracking()
            .Where(x => projectIds.Contains(x.Id) && x.IsActive)
            .Select(x => new { x.Id, x.TenantId, x.Code, x.Name })
            .ToListAsync(cancellationToken);

        var tenantIds = assignments.Where(x => x.TenantId.HasValue)
            .Select(x => x.TenantId!.Value)
            .Concat(branches.Select(x => x.TenantId))
            .Concat(projects.Select(x => x.TenantId))
            .Distinct().ToArray();
        var tenants = await db.Tenants.AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id) && x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToListAsync(cancellationToken);

        var scopes = assignments.Select(a =>
        {
            var branch = a.BranchId.HasValue
                ? branches.FirstOrDefault(x => x.Id == a.BranchId.Value) : null;
            var project = a.ProjectId.HasValue
                ? projects.FirstOrDefault(x => x.Id == a.ProjectId.Value) : null;
            var resolvedTenantId = a.TenantId ?? branch?.TenantId ?? project?.TenantId;
            var tenant = resolvedTenantId.HasValue
                ? tenants.FirstOrDefault(t => t.Id == resolvedTenantId.Value) : null;

            // Discard invalid or cross-tenant assignments rather than exposing them in the UI.
            if (tenant is null
                || (branch is not null && branch.TenantId != tenant.Id)
                || (project is not null && project.TenantId != tenant.Id)
                || (a.BranchId.HasValue && branch is null)
                || (a.ProjectId.HasValue && project is null))
                return null;

            return new
            {
                Id = tenant.Id,
                TenantCode = tenant.Code,
                TenantName = tenant.Name,
                BranchId = branch?.Id,
                BranchCode = branch?.Code,
                BranchName = branch?.Name,
                ProjectId = project?.Id,
                ProjectCode = project?.Code,
                ProjectName = project?.Name,
                a.RoleCode,
                a.RoleName
            };
        })
        .Where(x => x is not null)
        .Distinct()
        .ToList();

        return Ok(scopes);
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext(
        [FromQuery] long tenantId,
        [FromQuery] long? branchId,
        [FromQuery] long? projectId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(AppClaimTypes.UserId)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Unauthorized();

        if (tenantId <= 0)
            return BadRequest(new { message = "A valid tenantId is required." });

        var allowed = await scopedAccess.CanAccessAsync(
            userId, "Tenant.View", tenantId, branchId, projectId, cancellationToken);
        if (!allowed)
            return Forbid();

        var tenant = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId && x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (tenant is null) return NotFound(new { message = "Active tenant not found." });

        object? branch = null;
        if (branchId.HasValue)
        {
            branch = await db.Branches.AsNoTracking()
                .Where(x => x.Id == branchId.Value && x.TenantId == tenantId && x.IsActive)
                .Select(x => new { x.Id, x.Code, x.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (branch is null) return NotFound(new { message = "Active branch not found in this tenant." });
        }

        object? project = null;
        if (projectId.HasValue)
        {
            project = await db.Projects.AsNoTracking()
                .Where(x => x.Id == projectId.Value && x.TenantId == tenantId && x.IsActive)
                .Select(x => new { x.Id, x.Code, x.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (project is null) return NotFound(new { message = "Active project not found in this tenant." });
        }

        return Ok(new
        {
            tenant,
            branch,
            project,
            message = "Scope validated. Apply IScopedAccessService to each tenant-facing API operation."
        });
    }
}
