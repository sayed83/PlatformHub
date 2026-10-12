using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/platform/access")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformAccessController(PlatformDbContext db, IPasswordHasher<AppUser> passwordHasher) : ControllerBase
{
    [HttpGet("users")]
    [Authorize(Policy = "User.View")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] bool? active, CancellationToken cancellationToken)
    {
        var isSuperAdmin = IsSuperAdmin();
        // Regular platform admins manage tenant users, never platform-admin accounts.
        var query = db.Users.AsNoTracking().Where(x => isSuperAdmin || !x.IsPlatformAdmin).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.UserName.Contains(term) || x.DisplayName.Contains(term));
        }
        if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);

        var users = await query.OrderBy(x => x.UserName)
            .Select(x => new
            {
                x.Id, x.UserName, x.DisplayName, x.IsActive, x.IsPlatformAdmin,
                x.BaseRedirectUrl, x.CreatedUtc,
                Roles = db.UserRoleAssignments.Where(a => a.UserId == x.Id && a.Role.IsActive)
                    .Select(a => a.Role.Name).Distinct().OrderBy(n => n).ToList(),
                RoleAssignments = db.UserRoleAssignments.Where(a => a.UserId == x.Id)
                    .Select(a => new
                    {
                        a.Id, a.RoleId, RoleCode = a.Role.Code, RoleName = a.Role.Name,
                        a.TenantId, TenantName = a.Tenant == null ? null : a.Tenant.Name,
                        a.BranchId, BranchName = a.Branch == null ? null : a.Branch.Name,
                        a.ProjectId, ProjectName = a.Project == null ? null : a.Project.Name
                    }).ToList()
            }).ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPost("users")]
    [Authorize(Policy = "User.Create")]
    public async Task<IActionResult> CreateUser([FromBody] CreatePlatformUserRequest request, CancellationToken cancellationToken)
    {
        if (request.IsPlatformAdmin && !IsSuperAdmin())
            return Forbid();

        var userName = request.UserName?.Trim();
        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username, display name, and password are required." });
        if (userName.Length > 100 || displayName.Length > 200 || request.Password.Length < 12)
            return BadRequest(new { message = "Username/display name is too long or password is shorter than 12 characters." });
        if (await db.Users.AnyAsync(x => x.UserName == userName, cancellationToken))
            return Conflict(new { message = "Username already exists." });

        var user = new AppUser
        {
            UserName = userName,
            DisplayName = displayName,
            IsActive = true,
            IsPlatformAdmin = request.IsPlatformAdmin,
            BaseRedirectUrl = string.IsNullOrWhiteSpace(request.BaseRedirectUrl) ? null : request.BaseRedirectUrl.Trim(),
            CreatedUtc = DateTime.UtcNow
        };
        if (user.BaseRedirectUrl?.Length > 500)
            return BadRequest(new { message = "Redirect URL cannot exceed 500 characters." });
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        if (request.IsPlatformAdmin)
        {
            var platformAdminRole = await db.Roles.SingleOrDefaultAsync(
                x => x.Code == "PLATFORM_ADMIN" && x.IsActive, cancellationToken);
            if (platformAdminRole is null)
                return Conflict(new { message = "The built-in PLATFORM_ADMIN role is missing or inactive." });

            db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                User = user,
                RoleId = platformAdminRole.Id,
                TenantId = null,
                BranchId = null,
                ProjectId = null
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetUsers), new { }, new
        {
            user.Id, user.UserName, user.DisplayName, user.IsActive, user.IsPlatformAdmin,
            user.BaseRedirectUrl, user.CreatedUtc
        });
    }

    [HttpPut("users/{userId:long}")]
    [Authorize(Policy = "User.Update")]
    public async Task<IActionResult> UpdateUser(long userId, [FromBody] UpdatePlatformUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || (user.IsPlatformAdmin && !IsSuperAdmin())) return NotFound(new { message = "User not found." });
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 200
            || (request.BaseRedirectUrl?.Length ?? 0) > 500)
            return BadRequest(new { message = "Display name is required (max 200 characters); redirect URL max is 500 characters." });

        if (user.IsPlatformAdmin && !request.IsActive)
        {
            var superAdminRoleId = await db.Roles.Where(r => r.Code == PlatformRbacSeeder.SuperAdminCode)
                .Select(r => (long?)r.Id).SingleOrDefaultAsync(cancellationToken);
            var isSuperAdmin = superAdminRoleId.HasValue && await db.UserRoleAssignments.AnyAsync(a =>
                a.UserId == user.Id && a.RoleId == superAdminRoleId.Value
                && a.TenantId == null && a.BranchId == null && a.ProjectId == null, cancellationToken);
            if (isSuperAdmin)
            {
                var activeSuperAdmins = await db.UserRoleAssignments
                    .Where(a => a.RoleId == superAdminRoleId && a.TenantId == null && a.BranchId == null && a.ProjectId == null && a.User.IsActive && a.User.IsPlatformAdmin)
                    .Select(a => a.UserId).Distinct().CountAsync(cancellationToken);
                if (activeSuperAdmins <= 1)
                    return Conflict(new { message = "You cannot deactivate the last active Platform Super Admin." });
            }
        }

        user.DisplayName = request.DisplayName.Trim();
        user.BaseRedirectUrl = string.IsNullOrWhiteSpace(request.BaseRedirectUrl) ? null : request.BaseRedirectUrl.Trim();
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "User updated.", user.Id, user.UserName, user.DisplayName, user.IsActive, user.BaseRedirectUrl });
    }

    [HttpPost("users/{userId:long}/reset-password")]
    [Authorize(Policy = "User.Update")]
    public async Task<IActionResult> ResetPassword(long userId, [FromBody] ResetPlatformUserPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
            return BadRequest(new { message = "Password must contain at least 12 characters." });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || (user.IsPlatformAdmin && !IsSuperAdmin())) return NotFound(new { message = "User not found." });
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Password reset successfully. The user should sign in again." });
    }

    [HttpGet("roles")]
    [Authorize(Policy = "Role.View")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var isSuperAdmin = IsSuperAdmin();
        var roles = await db.Roles.AsNoTracking()
            .Where(x => isSuperAdmin || x.Code != PlatformRbacSeeder.SuperAdminCode)
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
        var isSuperAdmin = User.HasClaim(AppClaimTypes.Role, PlatformRbacSeeder.SuperAdminCode);
        if (role.Code == PlatformRbacSeeder.SuperAdminCode)
            return Conflict(new { message = "The Platform Super Admin role and its full permission set are protected." });
        if (role.IsSystemRole && !isSuperAdmin)
            return Forbid();

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

    private bool IsSuperAdmin() => User.HasClaim(AppClaimTypes.Role, PlatformRbacSeeder.SuperAdminCode);

    [HttpGet("permissions")]
    [Authorize(Policy = "Permission.View")]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        return Ok(await db.Permissions.AsNoTracking()
            .OrderBy(x => x.Module).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Module, x.Description, x.IsSystemPermission })
            .ToListAsync(cancellationToken));
    }

    [HttpGet("branches/{branchId:long}/assignments")]
    [Authorize(Policy = "Role.View")]
    public async Task<IActionResult> GetBranchAssignments(long branchId, CancellationToken cancellationToken)
    {
        var branchExists = await db.Branches.AsNoTracking().AnyAsync(x => x.Id == branchId, cancellationToken);
        if (!branchExists) return NotFound(new { message = "Branch not found." });

        var assignments = await db.UserRoleAssignments.AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Role.Name).ThenBy(x => x.User.DisplayName)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.BranchId,
                UserName = x.User.UserName,
                DisplayName = x.User.DisplayName,
                IsUserActive = x.User.IsActive,
                x.RoleId,
                RoleCode = x.Role.Code,
                RoleName = x.Role.Name,
                IsRoleActive = x.Role.IsActive,
                x.TenantId,
                TenantName = x.Tenant == null ? null : x.Tenant.Name,
                x.ProjectId,
                ProjectName = x.Project == null ? null : x.Project.Name
            }).ToListAsync(cancellationToken);
        return Ok(assignments);
    }

    [HttpGet("tenants/{tenantId:long}/dependencies")]
    [Authorize(Policy = "Tenant.View")]
    public async Task<IActionResult> GetTenantDependencies(long tenantId, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.AsNoTracking().Where(x => x.Id == tenantId)
            .Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).SingleOrDefaultAsync(cancellationToken);
        if (tenant is null) return NotFound(new { message = "Tenant not found." });

        var branches = await db.Branches.AsNoTracking().Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).ToListAsync(cancellationToken);
        var projects = await db.Projects.AsNoTracking().Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).ToListAsync(cancellationToken);
        var assignments = await db.UserRoleAssignments.AsNoTracking()
            .Where(x => x.TenantId == tenantId || (x.BranchId != null && db.Branches.Any(b => b.Id == x.BranchId && b.TenantId == tenantId))
                || (x.ProjectId != null && db.Projects.Any(p => p.Id == x.ProjectId && p.TenantId == tenantId)))
            .OrderBy(x => x.Role.Name).ThenBy(x => x.User.DisplayName)
            .Select(x => new {
                x.Id, x.UserId, UserName = x.User.UserName, DisplayName = x.User.DisplayName, IsUserActive = x.User.IsActive,
                x.RoleId, RoleName = x.Role.Name, RoleCode = x.Role.Code, IsRoleActive = x.Role.IsActive,
                x.TenantId, x.BranchId, BranchName = x.Branch == null ? null : x.Branch.Name,
                x.ProjectId, ProjectName = x.Project == null ? null : x.Project.Name
            }).ToListAsync(cancellationToken);
        return Ok(new { Tenant = tenant, Branches = branches, Projects = projects, RoleAssignments = assignments });
    }

    [HttpGet("users/{userId:long}/roles")]
    [Authorize(Policy = "Role.View")]
    public async Task<IActionResult> GetUserRoles(long userId, CancellationToken cancellationToken)
    {
        var targetUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (targetUser is null || (targetUser.IsPlatformAdmin && !IsSuperAdmin()))
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
        if (user is null || (user.IsPlatformAdmin && !IsSuperAdmin())) return NotFound(new { message = "User not found." });

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
        var targetUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (targetUser is null || (targetUser.IsPlatformAdmin && !IsSuperAdmin())) return NotFound();
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

public sealed record CreatePlatformUserRequest(string UserName, string DisplayName, string Password, string? BaseRedirectUrl, bool IsPlatformAdmin = false);
public sealed record UpdatePlatformUserRequest(string DisplayName, bool IsActive, string? BaseRedirectUrl);
public sealed record ResetPlatformUserPasswordRequest(string NewPassword);
