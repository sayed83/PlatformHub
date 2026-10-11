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
        ("Role.Assign", "Assign roles to users", "Access"),
        ("Permission.View", "View permission catalog", "Access"),
        ("AuditLog.View", "View audit logs", "Operations"),
        ("Platform.Settings.Manage", "Manage platform settings", "Operations")
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
        foreach (var item in PermissionCatalog)
        {
            if (!await db.Permissions.AnyAsync(x => x.Code == item.Code, cancellationToken))
            {
                db.Permissions.Add(new PlatformPermission
                {
                    Code = item.Code,
                    Name = item.Name,
                    Module = item.Module,
                    IsSystemPermission = true
                });
            }
        }

        foreach (var item in RoleCatalog)
        {
            if (!await db.Roles.AnyAsync(x => x.Code == item.Code, cancellationToken))
            {
                db.Roles.Add(new PlatformRole
                {
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    BaseRedirectUrl = item.Redirect,
                    IsSystemRole = true,
                    IsActive = true
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var superAdmin = await db.Roles.SingleAsync(x => x.Code == SuperAdminCode, cancellationToken);
        var permissionIds = await db.Permissions.Select(x => x.Id).ToListAsync(cancellationToken);
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

        // Preserve existing administrator accounts and migrate them into the built-in role.
        var platformAdminIds = await db.Users
            .Where(x => x.IsPlatformAdmin && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var userId in platformAdminIds)
        {
            var hasAssignment = await db.UserRoleAssignments.AnyAsync(
                x => x.UserId == userId && x.RoleId == superAdmin.Id
                     && x.TenantId == null && x.BranchId == null && x.ProjectId == null,
                cancellationToken);
            if (!hasAssignment)
            {
                db.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    UserId = userId,
                    RoleId = superAdmin.Id,
                    TenantId = null,
                    BranchId = null,
                    ProjectId = null
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
