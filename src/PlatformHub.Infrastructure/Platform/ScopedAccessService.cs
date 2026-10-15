using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

/// <summary>
/// Database-backed scope authorization. Never rely only on JWT scope claims: role assignments
/// and permissions may change while a token is still valid. Denials are recorded for security auditing.
/// </summary>
public interface IScopedAccessService
{
    Task<bool> CanAccessAsync(
        long userId,
        string permissionCode,
        long tenantId,
        long? branchId = null,
        long? projectId = null,
        CancellationToken cancellationToken = default);
}

public sealed class ScopedAccessService(
    PlatformDbContext db,
    ILogger<ScopedAccessService> logger) : IScopedAccessService
{
    public async Task<bool> CanAccessAsync(
        long userId,
        string permissionCode,
        long tenantId,
        long? branchId = null,
        long? projectId = null,
        CancellationToken cancellationToken = default)
    {
        bool Deny(string reason)
        {
            logger.LogWarning(
                "Scoped access denied. UserId={UserId}, Permission={PermissionCode}, TenantId={TenantId}, BranchId={BranchId}, ProjectId={ProjectId}, Reason={Reason}",
                userId, permissionCode, tenantId, branchId, projectId, reason);
            return false;
        }

        if (userId <= 0 || string.IsNullOrWhiteSpace(permissionCode) || tenantId <= 0)
            return Deny("Invalid request scope.");

        if (branchId is <= 0 || projectId is <= 0)
            return Deny("Branch or project ID is invalid.");

        var user = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new { x.Id, x.IsPlatformAdmin })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return Deny("User is missing or inactive.");

        var tenantExists = await db.Tenants.AsNoTracking()
            .AnyAsync(x => x.Id == tenantId && x.IsActive, cancellationToken);
        if (!tenantExists) return Deny("Tenant is missing or inactive.");

        if (branchId.HasValue)
        {
            var branchValid = await db.Branches.AsNoTracking()
                .AnyAsync(x => x.Id == branchId.Value && x.TenantId == tenantId && x.IsActive, cancellationToken);
            if (!branchValid) return Deny("Branch does not belong to the requested active tenant.");
        }

        if (projectId.HasValue)
        {
            var projectValid = await db.Projects.AsNoTracking()
                .AnyAsync(x => x.Id == projectId.Value && x.IsActive &&
                    (x.TenantId == tenantId ||
                     db.TenantProjects.Any(tp => tp.TenantId == tenantId &&
                         tp.ProjectId == x.Id && tp.IsEnabled)), cancellationToken);
            if (!projectValid) return Deny("Project is not active or is not assigned to the requested tenant.");
        }

        // Only the protected global Platform Super Admin assignment bypasses ordinary scoped
        // permission grants. A regular Platform Admin account must still hold the requested
        // permission; IsPlatformAdmin is an account classification, not an authorization grant.
        var isSuperAdmin = await db.UserRoleAssignments.AsNoTracking().AnyAsync(
            a => a.UserId == userId && user.IsPlatformAdmin
                && a.TenantId == null && a.BranchId == null && a.ProjectId == null
                && a.Role.IsActive && a.Role.Code == PlatformRbacSeeder.SuperAdminCode,
            cancellationToken);
        if (isSuperAdmin) return true;

        var matchingAssignment = from assignment in db.UserRoleAssignments.AsNoTracking()
                                 join rolePermission in db.RolePermissions.AsNoTracking()
                                     on assignment.RoleId equals rolePermission.RoleId
                                 join permission in db.Permissions.AsNoTracking()
                                     on rolePermission.PermissionId equals permission.Id
                                 where assignment.UserId == userId
                                     && assignment.Role.IsActive
                                     && permission.Code == permissionCode
                                 select assignment;

        // An assignment must be anchored to the requested tenant either directly or
        // through the exact active branch/project. A null TenantId is NOT a wildcard.
        matchingAssignment = matchingAssignment.Where(a =>
            a.TenantId == tenantId
            || (a.TenantId == null && branchId.HasValue
                && a.BranchId == branchId.Value
                && db.Branches.Any(b => b.Id == branchId.Value
                    && b.TenantId == tenantId && b.IsActive))
            );

        matchingAssignment = matchingAssignment.Where(a =>
            a.BranchId == null || (branchId.HasValue && a.BranchId == branchId.Value));

        matchingAssignment = matchingAssignment.Where(a =>
            a.ProjectId == null || (projectId.HasValue && a.ProjectId == projectId.Value));

        var allowed = await matchingAssignment.AnyAsync(cancellationToken);
        if (!allowed) return Deny("No active role assignment grants this permission at the requested scope.");
        return true;
    }
}
