using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

/// <summary>
/// Synchronizes project menus into the existing dbo.Permissions catalog.
/// Menu codes and project codes are stable identifiers; display names may change.
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
        var menuIds = await db.MenuItems.AsNoTracking()
            .Select(x => new { x.Id, x.ProjectId })
            .ToListAsync(cancellationToken);

        foreach (var menu in menuIds)
            await UpsertMenuPermissionAsync(db, menu.ProjectId, menu.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
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

        var permissionIds = await db.Permissions.AsNoTracking()
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
