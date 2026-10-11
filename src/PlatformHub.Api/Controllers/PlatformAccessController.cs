using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/platform/access")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformAccessController(PlatformDbContext db) : ControllerBase
{
    [HttpGet("roles")]
    [Authorize(Policy = "Role.View")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking()
            .OrderBy(x => x.IsSystemRole ? 0 : 1).ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id, x.Code, x.Name, x.Description, x.IsSystemRole, x.IsActive, x.BaseRedirectUrl,
                Permissions = db.RolePermissions.Where(rp => rp.RoleId == x.Id)
                    .Select(rp => rp.Permission.Code).OrderBy(code => code).ToList()
            }).ToListAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpPost("roles")]
    [Authorize(Policy = "Role.Create")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Role code and name are required." });
        if (code.Length > 100 || name.Length > 150 || (request.Description?.Length ?? 0) > 500
            || (request.BaseRedirectUrl?.Length ?? 0) > 500)
            return BadRequest(new { message = "Role fields exceed their allowed length." });
        if (code == PlatformRbacSeeder.SuperAdminCode || code.Contains(' '))
            return BadRequest(new { message = "This role code is reserved or invalid." });
        if (await db.Roles.AnyAsync(x => x.Code == code, cancellationToken))
            return Conflict(new { message = "Role code already exists." });

        var role = new PlatformRole
        {
            Code = code,
            Name = name,
            Description = request.Description?.Trim(),
            BaseRedirectUrl = request.BaseRedirectUrl?.Trim(),
            IsSystemRole = false,
            IsActive = true
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetRoles), new { id = role.Id }, new
        {
            role.Id, role.Code, role.Name, role.Description, role.IsSystemRole, role.IsActive, role.BaseRedirectUrl
        });
    }

    [HttpPut("roles/{roleId:long}/permissions")]
    [Authorize(Policy = "Role.Update")]
    public async Task<IActionResult> SetRolePermissions(long roleId, [FromBody] SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, cancellationToken);
        if (role is null) return NotFound(new { message = "Role not found." });
        if (role.IsSystemRole || role.Code == PlatformRbacSeeder.SuperAdminCode)
            return Conflict(new { message = "Built-in system roles and their critical permissions cannot be modified." });

        var permissionIds = request.PermissionIds?.Distinct().ToArray() ?? [];
        if (permissionIds.Length > 0)
        {
            var found = await db.Permissions.CountAsync(x => permissionIds.Contains(x.Id), cancellationToken);
            if (found != permissionIds.Length)
                return BadRequest(new { message = "One or more permission IDs are invalid." });
        }

        var current = await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(current);
        foreach (var permissionId in permissionIds)
            db.RolePermissions.Add(new PlatformRolePermission { RoleId = roleId, PermissionId = permissionId });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Role permissions updated.", roleId, permissionCount = permissionIds.Length });
    }

    [HttpGet("permissions")]
    [Authorize(Policy = "Permission.View")]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        return Ok(await db.Permissions.AsNoTracking()
            .OrderBy(x => x.Module).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Module, x.Description, x.IsSystemPermission })
            .ToListAsync(cancellationToken));
    }

    [HttpGet("users/{userId:long}/roles")]
    [Authorize(Policy = "Role.View")]
    public async Task<IActionResult> GetUserRoles(long userId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId, cancellationToken))
            return NotFound(new { message = "User not found." });

        var assignments = await db.UserRoleAssignments.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Role.Name)
            .Select(x => new
            {
                x.Id, x.RoleId, RoleCode = x.Role.Code, RoleName = x.Role.Name,
                x.TenantId, TenantName = x.Tenant == null ? null : x.Tenant.Name,
                x.BranchId, BranchName = x.Branch == null ? null : x.Branch.Name,
                x.ProjectId, ProjectName = x.Project == null ? null : x.Project.Name
            }).ToListAsync(cancellationToken);
        return Ok(assignments);
    }

    [HttpPost("users/{userId:long}/roles")]
    [Authorize(Policy = "Role.Assign")]
    public async Task<IActionResult> AssignRole(long userId, [FromBody] AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return NotFound(new { message = "User not found." });

        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == request.RoleId && x.IsActive, cancellationToken);
        if (role is null) return BadRequest(new { message = "Active role not found." });

        var isSuperAdmin = User.HasClaim(AppClaimTypes.Role, PlatformRbacSeeder.SuperAdminCode);
        if (role.Code == PlatformRbacSeeder.SuperAdminCode && !isSuperAdmin)
            return Forbid();

        if (role.Code == PlatformRbacSeeder.SuperAdminCode && !user.IsPlatformAdmin)
            return BadRequest(new { message = "The protected Platform Super Admin role can only be assigned to a platform administrator account." });

        if (request.ProjectId.HasValue)
        {
            var project = await db.Projects.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.ProjectId.Value, cancellationToken);
            if (project is null) return BadRequest(new { message = "Project scope does not exist." });
            if (request.TenantId.HasValue && request.TenantId != project.TenantId)
                return BadRequest(new { message = "Project and tenant scope do not match." });
            if (request.TenantId is null) request = request with { TenantId = project.TenantId };
        }

        if (request.BranchId.HasValue)
        {
            var branch = await db.Branches.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.BranchId.Value, cancellationToken);
            if (branch is null) return BadRequest(new { message = "Branch scope does not exist." });
            if (request.TenantId.HasValue && request.TenantId != branch.TenantId)
                return BadRequest(new { message = "Branch and tenant scope do not match." });
            if (request.TenantId is null) request = request with { TenantId = branch.TenantId };
        }

        if (request.TenantId.HasValue &&
            !await db.Tenants.AnyAsync(x => x.Id == request.TenantId.Value, cancellationToken))
            return BadRequest(new { message = "Tenant scope does not exist." });

        var duplicate = await db.UserRoleAssignments.AnyAsync(x =>
            x.UserId == userId && x.RoleId == role.Id &&
            x.TenantId == request.TenantId && x.BranchId == request.BranchId &&
            x.ProjectId == request.ProjectId, cancellationToken);
        if (duplicate) return Conflict(new { message = "This role is already assigned at the requested scope." });

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            UserId = userId,
            RoleId = role.Id,
            TenantId = request.TenantId,
            BranchId = request.BranchId,
            ProjectId = request.ProjectId
        });
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetUserRoles), new { userId }, new { message = "Role assigned successfully." });
    }

    [HttpDelete("users/{userId:long}/roles/{assignmentId:long}")]
    [Authorize(Policy = "Role.Assign")]
    public async Task<IActionResult> RemoveRole(long userId, long assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await db.UserRoleAssignments
            .Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, cancellationToken);
        if (assignment is null) return NotFound();
        if (assignment.Role.Code == PlatformRbacSeeder.SuperAdminCode)
            return Conflict(new { message = "The protected Platform Super Admin assignment cannot be removed through this endpoint." });

        db.UserRoleAssignments.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record AssignRoleRequest(long RoleId, long? TenantId, long? BranchId, long? ProjectId);
public sealed record CreateRoleRequest(string Code, string Name, string? Description, string? BaseRedirectUrl);
public sealed record SetRolePermissionsRequest(IReadOnlyList<long> PermissionIds);
