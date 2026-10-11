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

        var matchingAssignment = db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Role.IsActive
                && a.Role.RolePermissions.Any(rp => rp.Permission.Code == permissionCode));

        // Every non-null scope on an assignment must match the requested resource.
        // A tenant-only assignment covers all branches/projects under that tenant;
        // a branch/project assignment is restricted to that exact branch/project.
        matchingAssignment = matchingAssignment.Where(a =>
            a.TenantId == null || a.TenantId == tenantId);

        matchingAssignment = matchingAssignment.Where(a =>
            a.BranchId == null || (branchId.HasValue && a.BranchId == branchId.Value));

        matchingAssignment = matchingAssignment.Where(a =>
            a.ProjectId == null || (projectId.HasValue && a.ProjectId == projectId.Value));

        return await matchingAssignment.AnyAsync(cancellationToken);
    }
}
