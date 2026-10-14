using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

/// <summary>
/// Idempotently creates the standard project navigation catalog and grants role menu
/// visibility only when the role already has the matching resource View permission.
/// It never overwrites manually configured role or user menu decisions.
/// </summary>
public static class ProjectNavigationSeeder
{
    private sealed record ModuleSeed(string Code, string Name, string Icon, int SortOrder);
    private sealed record MenuSeed(
        string Code, string Name, string ModuleCode, string ViewPermission,
        string? Area, string? Controller, string? Action, string? RouteUrl,
        string Icon, int SortOrder);

    private static readonly ModuleSeed[] Modules =
    [
        new("WORKSPACE", "Workspace", "layout-dashboard", 1),
        new("ORGANIZATION", "Organization", "git-branch", 10),
        new("ACCESS", "Access Control", "shield-check", 20),
        new("INVENTORY", "Inventory", "package", 30)
    ];

    // Routes point to MVC pages. Where a feature currently has only an API and no MVC
    // page, the menu is still catalogued but marked with no route so it can be wired
    // when its UI page is implemented; do not link users directly to JSON API endpoints.
    private static readonly MenuSeed[] Menus =
    [
        new("WORKSPACE_OVERVIEW", "Overview", "WORKSPACE", "Platform.Dashboard.View", null, "Workspace", "Index", "/Workspace", "layout-dashboard", 1),
        new("TENANT_LIST", "Tenants", "ORGANIZATION", "Tenant.View", "PlatformAdmin", "Tenants", "Index", null, "building-2", 10),
        new("BRANCH_LIST", "Branches", "ORGANIZATION", "Branch.View", null, "Branches", "Index", null, "git-branch", 20),
        new("PROJECT_LIST", "Projects", "ORGANIZATION", "Project.View", "PlatformAdmin", "Projects", "Index", null, "panels-top-left", 30),
        new("USER_LIST", "Users", "ACCESS", "User.View", "PlatformAdmin", "Users", "Index", null, "users", 10),
        new("ROLE_LIST", "Roles", "ACCESS", "Role.View", "PlatformAdmin", "Roles", "Index", null, "shield", 20),
        //new("PERMISSION_CATALOG", "Permission Catalog", "ACCESS", "Permission.View", "PlatformAdmin", "Permissions", "Index", null, "key-round", 30),
        new("INVENTORY_ITEMS", "Inventory Items", "INVENTORY", "Inventory.Item.View", null, "Inventory", "Items", null, "package", 10)
    ];

    public static async Task SeedAsync(PlatformDbContext db, CancellationToken ct = default)
    {
        var projects = await db.Projects.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (projects.Count == 0) return;

        foreach (var projectId in projects)
        {
            var moduleRows = await db.ProjectModules
                .Where(m => m.ProjectId == projectId)
                .ToListAsync(ct);
            foreach (var seed in Modules)
            {
                var module = moduleRows.SingleOrDefault(m => m.Code == seed.Code);
                if (module is null)
                {
                    module = new ProjectModule { ProjectId = projectId, Code = seed.Code };
                    db.ProjectModules.Add(module);
                    moduleRows.Add(module);
                }

                module.Name = seed.Name;
                module.Icon = seed.Icon;
                module.SortOrder = seed.SortOrder;
                module.IsActive = true;
            }

            await db.SaveChangesAsync(ct);

            var menuRows = await db.MenuItems
                .Where(m => m.ProjectId == projectId)
                .ToListAsync(ct);

            // This permission editor belongs to Platform Admin, not the workspace sidebar.
            var obsoleteWorkspacePermissionMenu = menuRows.SingleOrDefault(m => m.Code == "WORKSPACE_MENU_PERMISSIONS");
            if (obsoleteWorkspacePermissionMenu is not null)
                obsoleteWorkspacePermissionMenu.IsActive = false;
            var roles = await db.Roles.AsNoTracking()
                .Where(r => r.IsActive && r.Code != PlatformRbacSeeder.SuperAdminCode)
                .Select(r => new { r.Id, r.Code })
                .ToListAsync(ct);

            foreach (var seed in Menus)
            {
                var module = moduleRows.Single(m => m.Code == seed.ModuleCode);
                var menu = menuRows.SingleOrDefault(m => m.Code == seed.Code);
                if (menu is null)
                {
                    menu = new MenuItem { ProjectId = projectId, Code = seed.Code };
                    db.MenuItems.Add(menu);
                    menuRows.Add(menu);
                }

                menu.ModuleId = module.Id;
                menu.Name = seed.Name;
                menu.Area = seed.Area;
                menu.Controller = seed.Controller;
                menu.Action = seed.Action;
                menu.RouteUrl = seed.RouteUrl;
                menu.Icon = seed.Icon;
                menu.SortOrder = seed.SortOrder;
                menu.IsExternal = false;
                // New catalog rows are active by default. Do not reactivate an existing
                // menu that an administrator intentionally soft-deactivated.
            }

            await db.SaveChangesAsync(ct);

            var menuIdsByCode = menuRows
                .Where(m => Menus.Any(seed => seed.Code == m.Code))
                .ToDictionary(m => m.Code, m => m.Id);
            var permissionId = await db.Permissions.AsNoTracking()
                .Where(p => Menus.Select(m => m.ViewPermission).Contains(p.Code) ||
                    p.Code == "Role.Update" || p.Code == "User.Update")
                .Select(p => new { p.Id, p.Code })
                .ToListAsync(ct);
            var permissionIdsByCode = permissionId.ToDictionary(p => p.Code, p => p.Id);

            foreach (var role in roles)
            {
                var rolePermissionIds = await db.RolePermissions.AsNoTracking()
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync(ct);
                var existing = await db.RoleWiseMenuAccesses
                    .Where(a => a.ProjectId == projectId && a.RoleId == role.Id)
                    .Select(a => new { a.MenuItemId, a.IsAllowed })
                    .ToListAsync(ct);

                foreach (var seed in Menus)
                {
                    var hasRequiredPermission = permissionIdsByCode.TryGetValue(seed.ViewPermission, out var requiredPermissionId)
                        && rolePermissionIds.Contains(requiredPermissionId);

                    if (!hasRequiredPermission)
                        continue;

                    var menuId = menuIdsByCode[seed.Code];
                    if (existing.Any(a => a.MenuItemId == menuId))
                        continue; // Preserve explicit administrator decisions.

                    db.RoleWiseMenuAccesses.Add(new RoleWiseMenuAccess
                    {
                        ProjectId = projectId,
                        RoleId = role.Id,
                        MenuItemId = menuId,
                        IsAllowed = true,
                        UpdatedUtc = DateTime.UtcNow
                    });
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
