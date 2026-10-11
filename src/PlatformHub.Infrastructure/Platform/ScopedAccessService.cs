using Microsoft.EntityFrameworkCore;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

/// <summary>
/// Database-backed scope authorization. Never rely only on JWT scope claims: role assignments
/// and permissions may change while a token is still valid.
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

public sealed class ScopedAccessService(PlatformDbContext db) : IScopedAccessService
{
    public async Task<bool> CanAccessAsync(
        long userId,
        string permissionCode,
        long tenantId,
        long? branchId = null,
        long? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(permissionCode) || tenantId <= 0)
            return false;

        var user = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new { x.Id, x.IsPlatformAdmin })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return false;

        var tenantExists = await db.Tenants.AsNoTracking()
            .AnyAsync(x => x.Id == tenantId && x.IsActive, cancellationToken);
        if (!tenantExists) return false;

        if (branchId.HasValue)
        {
            var branchValid = await db.Branches.AsNoTracking()
                .AnyAsync(x => x.Id == branchId.Value && x.TenantId == tenantId && x.IsActive, cancellationToken);
            if (!branchValid) return false;
        }

        if (projectId.HasValue)
        {
            var projectValid = await db.Projects.AsNoTracking()
                .AnyAsync(x => x.Id == projectId.Value && x.TenantId == tenantId && x.IsActive, cancellationToken);
            if (!projectValid) return false;
        }

        // Platform administrator status is re-read from the database, not trusted solely from JWT.
        if (user.IsPlatformAdmin) return true;

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
        // This prevents branch-only/project-only assignments from granting tenant-wide access.
        matchingAssignment = matchingAssignment.Where(a =>
            a.TenantId == tenantId
            || (a.TenantId == null && branchId.HasValue
                && a.BranchId == branchId.Value
                && db.Branches.Any(b => b.Id == branchId.Value
                    && b.TenantId == tenantId && b.IsActive))
            || (a.TenantId == null && projectId.HasValue
                && a.ProjectId == projectId.Value
                && db.Projects.Any(p => p.Id == projectId.Value
                    && p.TenantId == tenantId && p.IsActive)));

        matchingAssignment = matchingAssignment.Where(a =>
            a.BranchId == null || (branchId.HasValue && a.BranchId == branchId.Value));

        matchingAssignment = matchingAssignment.Where(a =>
            a.ProjectId == null || (projectId.HasValue && a.ProjectId == projectId.Value));

        return await matchingAssignment.AnyAsync(cancellationToken);
    }
}
