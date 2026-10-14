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

    [HttpGet("roles")]
    public async Task<IActionResult> GetWorkspaceRoles(
        [FromQuery] long tenantId,
        [FromQuery] long? branchId,
        [FromQuery] long? projectId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(AppClaimTypes.UserId)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Unauthorized();

        var allowed = await scopedAccess.CanAccessAsync(
            userId, "Role.View", tenantId, branchId, projectId, cancellationToken);
        if (!allowed) return Forbid();

        // Show only active roles actually assigned within this exact workspace scope.
        // Role definitions are shared platform catalog entries; workspace admins do not
        // get the global role editor from this read-only page.
        var assignments = db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.TenantId == tenantId ||
                (a.TenantId == null && branchId.HasValue && a.BranchId == branchId.Value));
        if (branchId.HasValue)
            assignments = assignments.Where(a => a.BranchId == null || a.BranchId == branchId.Value);
        else
            assignments = assignments.Where(a => a.BranchId == null);
        if (projectId.HasValue)
            assignments = assignments.Where(a => a.ProjectId == null || a.ProjectId == projectId.Value);
        else
            assignments = assignments.Where(a => a.ProjectId == null);

        var roleIds = assignments.Select(a => a.RoleId).Distinct();
        var roles = await db.Roles.AsNoTracking()
            .Where(r => roleIds.Contains(r.Id) && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id, r.Code, r.Name, r.Description, r.BaseRedirectUrl,
                UserCount = assignments.Where(a => a.RoleId == r.Id)
                    .Select(a => a.UserId).Distinct().Count(),
                Permissions = db.RolePermissions.Where(rp => rp.RoleId == r.Id)
                    .Select(rp => rp.Permission.Code).OrderBy(code => code).ToList()
            }).ToListAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetWorkspaceUsers(
        [FromQuery] long tenantId,
        [FromQuery] long? branchId,
        [FromQuery] long? projectId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(AppClaimTypes.UserId)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Unauthorized();

        var allowed = await scopedAccess.CanAccessAsync(
            userId, "User.View", tenantId, branchId, projectId, cancellationToken);
        if (!allowed) return Forbid();

        // Return only accounts explicitly assigned within the selected scope.
        // A tenant/branch role assignment never grants access to unrelated branches.
        var assignments = db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.TenantId == tenantId ||
                (a.TenantId == null && branchId.HasValue && a.BranchId == branchId.Value));
        if (branchId.HasValue)
            assignments = assignments.Where(a => a.BranchId == null || a.BranchId == branchId.Value);
        else
            assignments = assignments.Where(a => a.BranchId == null);
        if (projectId.HasValue)
            assignments = assignments.Where(a => a.ProjectId == null || a.ProjectId == projectId.Value);
        else
            assignments = assignments.Where(a => a.ProjectId == null);

        var assignedUserIds = assignments.Select(a => a.UserId).Distinct();
        var users = db.Users.AsNoTracking().Where(u => assignedUserIds.Contains(u.Id) && !u.IsPlatformAdmin);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            users = users.Where(u => u.UserName.Contains(term) || u.DisplayName.Contains(term));
        }

        var rows = await users.OrderBy(u => u.UserName)
            .Select(u => new
            {
                u.Id, u.UserName, u.DisplayName, u.IsActive,
                Roles = db.UserRoleAssignments.Where(a => a.UserId == u.Id &&
                        (a.TenantId == tenantId ||
                         (a.TenantId == null && branchId.HasValue && a.BranchId == branchId.Value)) &&
                        (a.BranchId == null || a.BranchId == branchId) &&
                        (a.ProjectId == null || a.ProjectId == projectId) && a.Role.IsActive)
                    .Select(a => a.Role.Name).Distinct().OrderBy(n => n).ToList()
            }).ToListAsync(cancellationToken);
        return Ok(rows);
    }


    public sealed record MenuAccessUpdateRequest(long MenuItemId, bool IsAllowed);

    [HttpPut("roles/{roleId:long}/menu-access")]
    public async Task<IActionResult> UpdateRoleMenuAccess(
        long roleId,
        [FromQuery] long tenantId,
        [FromQuery] long? branchId,
        [FromQuery] long? projectId,
        [FromBody] MenuAccessUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(AppClaimTypes.UserId)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Unauthorized();
        if (roleId <= 0 || request.MenuItemId <= 0 || !projectId.HasValue || projectId.Value <= 0)
            return BadRequest(new { message = "Role, menu, and active project are required." });

        if (!await scopedAccess.CanAccessAsync(userId, "Role.Update", tenantId, branchId, projectId, cancellationToken))
            return Forbid();

        var roleAssignedInScope = await db.UserRoleAssignments.AsNoTracking().AnyAsync(a =>
            a.RoleId == roleId && a.Role.IsActive &&
            (a.TenantId == tenantId || (a.TenantId == null && branchId.HasValue && a.BranchId == branchId.Value)) &&
            (a.BranchId == null || (branchId.HasValue && a.BranchId == branchId.Value)) &&
            (a.ProjectId == null || a.ProjectId == projectId.Value), cancellationToken);
        if (!roleAssignedInScope) return NotFound(new { message = "Role is not assigned in this workspace scope." });

        var menuExists = await db.MenuItems.AnyAsync(m => m.Id == request.MenuItemId &&
            m.ProjectId == projectId.Value && m.IsActive, cancellationToken);
        if (!menuExists) return NotFound(new { message = "Menu was not found in the active project." });

        var access = await db.RoleWiseMenuAccesses.SingleOrDefaultAsync(a =>
            a.ProjectId == projectId.Value && a.RoleId == roleId && a.MenuItemId == request.MenuItemId, cancellationToken);
        if (access is null)
            db.RoleWiseMenuAccesses.Add(new PlatformHub.Domain.Entities.RoleWiseMenuAccess
            {
                ProjectId = projectId.Value, RoleId = roleId,
                MenuItemId = request.MenuItemId, IsAllowed = request.IsAllowed
            });
        else
        {
            access.IsAllowed = request.IsAllowed;
            access.UpdatedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, request.MenuItemId, request.IsAllowed });
    }

    [HttpPut("users/{targetUserId:long}/menu-access")]
    public async Task<IActionResult> UpdateUserMenuAccess(
        long targetUserId,
        [FromQuery] long tenantId,
        [FromQuery] long? branchId,
        [FromQuery] long? projectId,
        [FromBody] MenuAccessUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(AppClaimTypes.UserId)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Unauthorized();
        if (targetUserId <= 0 || request.MenuItemId <= 0 || !projectId.HasValue || projectId.Value <= 0)
            return BadRequest(new { message = "User, menu, and active project are required." });

        if (!await scopedAccess.CanAccessAsync(userId, "User.Update", tenantId, branchId, projectId, cancellationToken))
            return Forbid();

        var targetAssignedInScope = await db.UserRoleAssignments.AsNoTracking().AnyAsync(a =>
            a.UserId == targetUserId && !a.User.IsPlatformAdmin && a.Role.IsActive &&
            (a.TenantId == tenantId || (a.TenantId == null && branchId.HasValue && a.BranchId == branchId.Value)) &&
            (a.BranchId == null || (branchId.HasValue && a.BranchId == branchId.Value)) &&
            (a.ProjectId == null || a.ProjectId == projectId.Value), cancellationToken);
        if (!targetAssignedInScope) return NotFound(new { message = "User is not assigned in this workspace scope." });

        var menuExists = await db.MenuItems.AnyAsync(m => m.Id == request.MenuItemId &&
            m.ProjectId == projectId.Value && m.IsActive, cancellationToken);
        if (!menuExists) return NotFound(new { message = "Menu was not found in the active project." });

        var access = await db.UserWiseMenuAccesses.SingleOrDefaultAsync(a =>
            a.ProjectId == projectId.Value && a.UserId == targetUserId && a.MenuItemId == request.MenuItemId, cancellationToken);
        if (access is null)
            db.UserWiseMenuAccesses.Add(new PlatformHub.Domain.Entities.UserWiseMenuAccess
            {
                ProjectId = projectId.Value, UserId = targetUserId,
                MenuItemId = request.MenuItemId, IsAllowed = request.IsAllowed
            });
        else
        {
            access.IsAllowed = request.IsAllowed;
            access.UpdatedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, request.MenuItemId, request.IsAllowed });
    }

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

        // Load active projects once. A branch/tenant-scoped role assignment may not carry
        // ProjectId, but the tenant can still have reusable projects assigned through TenantProjects.
        var projects = await db.Projects.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.TenantId, x.Code, x.Name })
            .ToListAsync(cancellationToken);

        var tenantIds = assignments.Where(x => x.TenantId.HasValue)
            .Select(x => x.TenantId!.Value)
            .Concat(branches.Select(x => x.TenantId))
            .Concat(projects.Where(p => assignments.Any(a => a.ProjectId == p.Id) && p.TenantId.HasValue)
                .Select(p => p.TenantId!.Value))
            .Distinct().ToArray();
        var tenants = await db.Tenants.AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id) && x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToListAsync(cancellationToken);

        var activeTenantProjectPairs = await db.TenantProjects.AsNoTracking()
            .Where(tp => tp.IsEnabled && tp.Tenant.IsActive)
            .Select(tp => new { tp.TenantId, tp.ProjectId })
            .ToListAsync(cancellationToken);
        var activeTenantProjectKeys = activeTenantProjectPairs
            .Select(tp => (tp.TenantId, tp.ProjectId))
            .ToHashSet();

        var resolvedScopes = new List<object>();
        foreach (var assignment in assignments)
        {
            var branch = assignment.BranchId.HasValue
                ? branches.FirstOrDefault(x => x.Id == assignment.BranchId.Value) : null;
            var assignedProject = assignment.ProjectId.HasValue
                ? projects.FirstOrDefault(x => x.Id == assignment.ProjectId.Value) : null;
            var resolvedTenantId = assignment.TenantId ?? branch?.TenantId ?? assignedProject?.TenantId;
            var tenant = resolvedTenantId.HasValue
                ? tenants.FirstOrDefault(t => t.Id == resolvedTenantId.Value) : null;

            // Reject invalid or cross-tenant assignments before expanding projects.
            if (tenant is null
                || (assignment.BranchId.HasValue && (branch is null || branch.TenantId != tenant.Id))
                || (assignment.ProjectId.HasValue && assignedProject is null))
                continue;

            var availableProjects = projects
                .Where(project =>
                    (!assignment.ProjectId.HasValue || project.Id == assignment.ProjectId.Value) &&
                    (project.TenantId == tenant.Id ||
                     activeTenantProjectKeys.Contains((tenant.Id, project.Id))))
                .ToList();

            // If the assignment is tenant/branch scoped, offer each active project assigned
            // to the tenant. This lets branch admins select a project without changing the
            // scope of their role assignment.
            if (availableProjects.Count > 0)
            {
                foreach (var project in availableProjects)
                {
                    resolvedScopes.Add(new
                    {
                        Id = tenant.Id,
                        TenantCode = tenant.Code,
                        TenantName = tenant.Name,
                        BranchId = branch?.Id,
                        BranchCode = branch?.Code,
                        BranchName = branch?.Name,
                        ProjectId = (long?)project.Id,
                        ProjectCode = (string?)project.Code,
                        ProjectName = (string?)project.Name,
                        assignment.RoleCode,
                        assignment.RoleName
                    });
                }
            }
            else if (!assignment.ProjectId.HasValue)
            {
                // Keep a tenant/branch-only scope available when the tenant has no project yet.
                resolvedScopes.Add(new
                {
                    Id = tenant.Id,
                    TenantCode = tenant.Code,
                    TenantName = tenant.Name,
                    BranchId = branch?.Id,
                    BranchCode = branch?.Code,
                    BranchName = branch?.Name,
                    ProjectId = (long?)null,
                    ProjectCode = (string?)null,
                    ProjectName = (string?)null,
                    assignment.RoleCode,
                    assignment.RoleName
                });
            }
        }

        var scopes = resolvedScopes
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
                .Where(x => x.Id == projectId.Value && x.IsActive &&
                    (x.TenantId == tenantId ||
                     db.TenantProjects.Any(tp => tp.TenantId == tenantId &&
                         tp.ProjectId == x.Id && tp.IsEnabled)))
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
