using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

/// <summary>
/// Synchronizes project menus into the existing dbo.Permissions catalog.
/// Permission codes use stable project/menu IDs; display names and menu codes may change.
/// Generated menu permissions are view/navigation permissions. Business CRUD permissions
/// remain explicit system/API permissions and must be enforced on their API endpoints.
/// </summary>
public static class ProjectMenuPermissionSynchronizer
{
    public static string BuildViewPermissionCode(long projectId, long menuItemId) =>
        $"Project.{projectId}.Menu.{menuItemId}.View";

    public static async Task SyncAllAsync(
        PlatformDbContext db,
        CancellationToken cancellationToken = default)
    {
        var menuRows = await db.MenuItems.AsNoTracking()
            .Select(x => new { x.Id, x.ProjectId })
            .ToListAsync(cancellationToken);
        var menuIds = menuRows.Select(x => (Id: x.Id, ProjectId: x.ProjectId)).ToArray();

        foreach (var menu in menuIds)
            await UpsertMenuPermissionAsync(db, menu.ProjectId, menu.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await RemoveMenuPermissionsFromGlobalRoleAssignmentsAsync(db, menuIds, cancellationToken);
        await EnsureSuperAdminHasAllPermissionsAsync(db, cancellationToken);
    }

    public static async Task SyncProjectAsync(
        PlatformDbContext db,
        long projectId,
        CancellationToken cancellationToken = default)
    {
        var menuIds = await db.MenuItems.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var menuId in menuIds)
            await UpsertMenuPermissionAsync(db, projectId, menuId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await RemoveMenuPermissionsFromGlobalRoleAssignmentsAsync(
            db, menuIds.Select(id => (Id: id, ProjectId: projectId)).ToArray(), cancellationToken);
        await EnsureSuperAdminHasAllPermissionsAsync(db, cancellationToken);
    }

    public static async Task SyncAsync(
        PlatformDbContext db,
        long projectId,
        long menuItemId,
        CancellationToken cancellationToken = default)
    {
        await UpsertMenuPermissionAsync(db, projectId, menuItemId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await RemoveMenuPermissionsFromGlobalRoleAssignmentsAsync(
            db, new[] { (Id: menuItemId, ProjectId: projectId) }, cancellationToken);
        await EnsureSuperAdminHasAllPermissionsAsync(db, cancellationToken);
    }

    private static async Task UpsertMenuPermissionAsync(
        PlatformDbContext db,
        long projectId,
        long menuItemId,
        CancellationToken cancellationToken)
    {
        var menu = await db.MenuItems.AsNoTracking()
            .Where(x => x.Id == menuItemId && x.ProjectId == projectId)
            .Select(x => new
            {
                x.Id,
                x.ProjectId,
                x.Name,
                ProjectName = x.Project.Name,
                ModuleName = x.Module == null ? "General" : x.Module.Name
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (menu is null)
            throw new InvalidOperationException("Cannot synchronize permissions for a menu outside the selected project.");

        var code = BuildViewPermissionCode(menu.ProjectId, menu.Id);
        var permission = await db.Permissions.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        var module = Truncate($"{menu.ProjectName} / {menu.ModuleName}", 100);
        var name = Truncate($"View {menu.Name}", 150);
        var description = Truncate($"Project-menu visibility permission for {menu.ProjectName} / {menu.Name}. MenuItemId={menu.Id}; ProjectId={menu.ProjectId}.", 500);

        if (permission is null)
        {
            permission = new PlatformPermission
            {
                Code = code,
                Name = name,
                Module = module,
                Description = description,
                IsSystemPermission = false
            };
            db.Permissions.Add(permission);
        }
        else
        {
            permission.Name = name;
            permission.Module = module;
            permission.Description = description;
            permission.IsSystemPermission = false;
        }
    }

    private static async Task RemoveMenuPermissionsFromGlobalRoleAssignmentsAsync(
        PlatformDbContext db,
        IReadOnlyCollection<(long Id, long ProjectId)> menuIds,
        CancellationToken cancellationToken)
    {
        var menuByCode = menuIds
            .Select(menu => new
            {
                Code = BuildViewPermissionCode(menu.ProjectId, menu.Id),
                menu.ProjectId,
                MenuItemId = menu.Id
            })
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var generatedCodes = menuByCode.Keys.ToArray();
        if (generatedCodes.Length == 0)
            return;

        var generatedPermissions = await db.Permissions.AsNoTracking()
            .Where(p => generatedCodes.Contains(p.Code) && !p.IsSystemPermission)
            .Select(p => new { p.Id, p.Code })
            .ToListAsync(cancellationToken);
        if (generatedPermissions.Count == 0)
            return;

        var permissionById = generatedPermissions.ToDictionary(x => x.Id);
        var generatedPermissionIds = permissionById.Keys.ToArray();
        var staleAssignments = await db.RolePermissions
            .Where(rp => generatedPermissionIds.Contains(rp.PermissionId))
            .Select(rp => new { rp.RoleId, rp.PermissionId, RoleCode = rp.Role.Code })
            .ToListAsync(cancellationToken);

        // Preserve existing non-super-admin menu grants by moving them from the global
        // role-permission join into the existing project-scoped role-menu table.
        // Existing explicit deny rows are never overwritten.
        var grantsToMigrate = staleAssignments
            .Where(x => !string.Equals(x.RoleCode, PlatformRbacSeeder.SuperAdminCode, StringComparison.OrdinalIgnoreCase))
            .Select(x =>
            {
                var permission = permissionById[x.PermissionId];
                var menu = menuByCode[permission.Code];
                return (x.RoleId, menu.ProjectId, menu.MenuItemId);
            })
            .Distinct()
            .ToArray();

        if (grantsToMigrate.Length > 0)
        {
            var roleIds = grantsToMigrate.Select(x => x.RoleId).Distinct().ToArray();
            var projectIds = grantsToMigrate.Select(x => x.ProjectId).Distinct().ToArray();
            var menuItemIds = grantsToMigrate.Select(x => x.MenuItemId).Distinct().ToArray();
            var existingRows = await db.RoleWiseMenuAccesses
                .Where(a => roleIds.Contains(a.RoleId) && projectIds.Contains(a.ProjectId) && menuItemIds.Contains(a.MenuItemId))
                .Select(a => new { a.RoleId, a.ProjectId, a.MenuItemId })
                .ToListAsync(cancellationToken);
            var existingKeys = existingRows
                .Select(x => (x.RoleId, x.ProjectId, x.MenuItemId))
                .ToHashSet();

            foreach (var grant in grantsToMigrate)
            {
                if (existingKeys.Add(grant))
                {
                    db.RoleWiseMenuAccesses.Add(new RoleWiseMenuAccess
                    {
                        RoleId = grant.RoleId,
                        ProjectId = grant.ProjectId,
                        MenuItemId = grant.MenuItemId,
                        IsAllowed = true,
                        UpdatedUtc = DateTime.UtcNow
                    });
                }
            }
        }

        if (staleAssignments.Count > 0)
        {
            var links = await db.RolePermissions
                .Where(rp => generatedPermissionIds.Contains(rp.PermissionId))
                .ToListAsync(cancellationToken);
            db.RolePermissions.RemoveRange(links);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSuperAdminHasAllPermissionsAsync(
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        var superAdmin = await db.Roles.AsNoTracking()
            .Where(x => x.TenantId == null && x.Code == PlatformRbacSeeder.SuperAdminCode)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!superAdmin.HasValue)
            return;

        // The platform super admin receives all global API permissions only.
        // Project-menu visibility is managed through RoleWiseMenuAccess/UserWiseMenuAccess.
        var permissionIds = await db.Permissions.AsNoTracking()
            .Where(x => x.IsSystemPermission)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var assignedIds = await db.RolePermissions.AsNoTracking()
            .Where(x => x.RoleId == superAdmin.Value)
            .Select(x => x.PermissionId)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in permissionIds.Except(assignedIds))
        {
            db.RolePermissions.Add(new PlatformRolePermission
            {
                RoleId = superAdmin.Value,
                PermissionId = permissionId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
