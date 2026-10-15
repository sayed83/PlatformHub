using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

public static class PlatformRbacSeeder
{
    public const string SuperAdminCode = "PLATFORM_SUPER_ADMIN";

    private static readonly (string Code, string Name, string Module)[] PermissionCatalog =
    [
        ("Platform.Dashboard.View", "View platform dashboard", "Platform"),
        ("Tenant.View", "View tenants", "Tenants"),
        ("Tenant.Create", "Create tenants", "Tenants"),
        ("Tenant.Update", "Update tenants", "Tenants"),
        ("Tenant.Deactivate", "Deactivate tenants", "Tenants"),
        ("Branch.View", "View branches", "Branches"),
        ("Branch.Create", "Create branches", "Branches"),
        ("Branch.Update", "Update branches", "Branches"),
        ("Branch.Deactivate", "Deactivate branches", "Branches"),
        ("Project.View", "View projects", "Projects"),
        ("Project.Create", "Create projects", "Projects"),
        ("Project.Update", "Update projects", "Projects"),
        ("Project.Deactivate", "Deactivate projects", "Projects"),
        ("User.View", "View users", "Users"),
        ("User.Create", "Create users", "Users"),
        ("User.Update", "Update users", "Users"),
        ("User.Deactivate", "Deactivate users", "Users"),
        ("Role.View", "View roles", "Access"),
        ("Role.Create", "Create roles", "Access"),
        ("Role.Update", "Update roles", "Access"),
        ("Role.Deactivate", "Activate or deactivate roles", "Access"),
        ("Role.Assign", "Assign roles to users", "Access"),
        ("Permission.View", "View permission catalog", "Access"),
        ("MenuAccess.View", "View role and user project-menu access", "Access"),
        ("MenuAccess.Update", "Change role and user project-menu access", "Access"),
        ("AuditLog.View", "View audit logs", "Operations"),
        ("Platform.Settings.Manage", "Manage platform settings", "Operations"),
        ("Inventory.Item.View", "View inventory items", "Inventory"),
        ("Inventory.Item.Create", "Create inventory items", "Inventory"),
        ("Inventory.Item.Update", "Update inventory items", "Inventory"),
        ("Inventory.Item.Deactivate", "Deactivate inventory items", "Inventory")
    ];

    private static readonly (string Code, string Name, string Description, string? Redirect)[] RoleCatalog =
    [
        (SuperAdminCode, "Platform Super Admin", "Protected built-in role with full platform access.", "/PlatformAdmin/Dashboard"),
        ("PLATFORM_ADMIN", "Platform Admin", "Platform operations limited by assigned permissions.", "/PlatformAdmin/Dashboard"),
        ("TENANT_ADMIN", "Tenant Admin", "Administration within assigned tenant scope.", "/Workspace"),
        ("BRANCH_ADMIN", "Branch Admin", "Administration within assigned branch scope.", "/Workspace"),
        ("PROJECT_USER", "Project User", "Access within assigned project scope.", "/Workspace")
    ];

    public static async Task SeedAsync(PlatformDbContext db, CancellationToken cancellationToken = default)
    {
        // These are the stable code-defined system/API permissions. Project-menu View
        // permissions are synchronized separately from dbo.MenuItems by
        // ProjectMenuPermissionSynchronizer and do not need an entry here.
        foreach (var item in PermissionCatalog)
        {
            var permission = await db.Permissions
                .SingleOrDefaultAsync(x => x.Code == item.Code, cancellationToken);
            if (permission is null)
            {
                db.Permissions.Add(new PlatformPermission
                {
                    Code = item.Code,
                    Name = item.Name,
                    Module = item.Module,
                    IsSystemPermission = true
                });
            }
            else
            {
                permission.Name = item.Name;
                permission.Module = item.Module;
                permission.IsSystemPermission = true;
            }
        }

        foreach (var item in RoleCatalog)
        {
            // Built-in roles must exist as global roles. Older databases may contain an
            // inactive or partially configured built-in role; synchronize its seed metadata
            // so configuration-driven default-user seeding does not fail on stale role state.
            var role = await db.Roles.SingleOrDefaultAsync(
                x => x.Code == item.Code && x.TenantId == null, cancellationToken);

            if (role is null)
            {
                role = new PlatformRole
                {
                    Code = item.Code,
                    IsSystemRole = true
                };
                db.Roles.Add(role);
            }

            role.Name = item.Name;
            role.Description = item.Description;
            role.BaseRedirectUrl = item.Redirect;
            role.IsSystemRole = true;
            role.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        var superAdmin = await db.Roles.SingleAsync(x => x.Code == SuperAdminCode, cancellationToken);
        var permissionIds = await db.Permissions.Where(x => x.IsSystemPermission).Select(x => x.Id).ToListAsync(cancellationToken);
        var existingRolePermissionIds = await db.RolePermissions
            .Where(x => x.RoleId == superAdmin.Id)
            .Select(x => x.PermissionId)
            .ToListAsync(cancellationToken);
        foreach (var permissionId in permissionIds.Except(existingRolePermissionIds))
        {
            db.RolePermissions.Add(new PlatformRolePermission
            {
                RoleId = superAdmin.Id,
                PermissionId = permissionId
            });
        }

        // Bootstrap the first and only platform-admin account as Super Admin on a fresh install.
        // Once a global Super Admin assignment exists, never auto-promote any other admin account.
        var hasGlobalSuperAdmin = await db.UserRoleAssignments.AnyAsync(
            x => x.RoleId == superAdmin.Id && x.TenantId == null && x.BranchId == null && x.ProjectId == null,
            cancellationToken);
        var platformAdminIds = await db.Users
            .Where(x => x.IsPlatformAdmin && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (!hasGlobalSuperAdmin && platformAdminIds.Count == 1)
        {
            db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = platformAdminIds[0],
                RoleId = superAdmin.Id,
                TenantId = null,
                BranchId = null,
                ProjectId = null
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Give built-in non-super-admin roles useful least-privilege permission sets.
        // Custom roles remain administrator-configured in the Role & Permissions UI.
        var rolePermissionCodes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["PLATFORM_ADMIN"] =
            [
                "Platform.Dashboard.View",
                "Tenant.View", "Tenant.Create", "Tenant.Update", "Tenant.Deactivate",
                "Branch.View", "Branch.Create", "Branch.Update", "Branch.Deactivate",
                "Project.View", "Project.Create", "Project.Update", "Project.Deactivate",
                "User.View", "User.Create", "User.Update", "User.Deactivate",
                "Role.View", "Role.Create", "Role.Update", "Role.Assign", "Permission.View",
                "MenuAccess.View", "MenuAccess.Update",
                "AuditLog.View", "Platform.Settings.Manage",
                "Inventory.Item.View", "Inventory.Item.Create", "Inventory.Item.Update", "Inventory.Item.Deactivate"
            ],
            ["TENANT_ADMIN"] =
            [
                "Platform.Dashboard.View",
                "Tenant.View",
                "Branch.View", "Branch.Create", "Branch.Update",
                "Project.View", "Project.Create", "Project.Update",
                "User.View", "User.Create", "User.Update",
                "Role.View", "Role.Create", "Role.Update", "Role.Deactivate", "Role.Assign", "Permission.View",
                "Inventory.Item.View", "Inventory.Item.Create", "Inventory.Item.Update", "Inventory.Item.Deactivate"
            ],
            ["BRANCH_ADMIN"] =
            [
                "Platform.Dashboard.View",
                "Tenant.View", "Branch.View", "Branch.Update",
                "Project.View", "User.View", "User.Create", "User.Update",
                "Role.View", "Permission.View",
                "Inventory.Item.View", "Inventory.Item.Create", "Inventory.Item.Update"
            ],
            ["PROJECT_USER"] = ["Project.View", "Inventory.Item.View"]
        };

        foreach (var (roleCode, permissionCodes) in rolePermissionCodes)
        {
            var role = await db.Roles.SingleAsync(x => x.Code == roleCode, cancellationToken);
            var ids = await db.Permissions
                .Where(x => x.IsSystemPermission && permissionCodes.Contains(x.Code))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var existingIds = await db.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.PermissionId)
                .ToListAsync(cancellationToken);
            foreach (var permissionId in ids.Except(existingIds))
            {
                db.RolePermissions.Add(new PlatformRolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionId
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
