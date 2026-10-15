using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class ProjectMenuPermissionSynchronizerTests
{
    [Fact]
    public async Task Sync_creates_menu_view_permission_but_assigns_only_global_api_permissions_to_super_admin()
    {
        await using var db = CreateDb();
        db.Projects.Add(new Project
        {
            Id = 12, Code = "INVENTORY", Name = "Inventory", IsActive = true
        });
        db.MenuItems.Add(new MenuItem
        {
            Id = 88, ProjectId = 12, Code = "PRODUCTS", Name = "Products", IsActive = true
        });
        db.Permissions.Add(new PlatformPermission
        {
            Id = 1, Code = "Inventory.Item.View", Name = "View inventory items",
            Module = "Inventory", IsSystemPermission = true
        });
        db.Roles.Add(new PlatformRole
        {
            Id = 1, Code = PlatformRbacSeeder.SuperAdminCode, Name = "Platform Super Admin",
            IsSystemRole = true, IsActive = true
        });
        await db.SaveChangesAsync();

        await ProjectMenuPermissionSynchronizer.SyncAllAsync(db);

        var code = ProjectMenuPermissionSynchronizer.BuildViewPermissionCode(12, 88);
        var permission = await db.Permissions.SingleAsync(x => x.Code == code);
        Assert.Equal("View Products", permission.Name);
        Assert.Equal("Inventory / General", permission.Module);
        Assert.False(permission.IsSystemPermission);
        Assert.False(await db.RolePermissions.AnyAsync(x => x.RoleId == 1 && x.PermissionId == permission.Id));
        Assert.True(await db.RolePermissions.AnyAsync(x => x.RoleId == 1 && x.PermissionId == 1));
    }

    [Fact]
    public async Task Sync_updates_display_metadata_and_removes_stale_global_menu_assignments()
    {
        await using var db = CreateDb();
        db.Projects.Add(new Project { Id = 12, Code = "INVENTORY", Name = "Inventory", IsActive = true });
        db.MenuItems.Add(new MenuItem { Id = 88, ProjectId = 12, Code = "PRODUCTS", Name = "Products", IsActive = true });
        db.Roles.Add(new PlatformRole { Id = 1, Code = "CUSTOM_ROLE", Name = "Custom", IsActive = true });
        await db.SaveChangesAsync();

        await ProjectMenuPermissionSynchronizer.SyncAllAsync(db);
        var code = ProjectMenuPermissionSynchronizer.BuildViewPermissionCode(12, 88);
        var permission = await db.Permissions.SingleAsync(x => x.Code == code);
        db.RolePermissions.Add(new PlatformRolePermission { RoleId = 1, PermissionId = permission.Id });
        var menu = await db.MenuItems.SingleAsync(x => x.Id == 88);
        menu.Name = "Product Catalogue";
        menu.Code = "PRODUCT_CATALOGUE";
        await db.SaveChangesAsync();

        await ProjectMenuPermissionSynchronizer.SyncAsync(db, 12, 88);

        var updated = await db.Permissions.SingleAsync(x => x.Code == code);
        Assert.Equal("View Product Catalogue", updated.Name);
        Assert.False(await db.RolePermissions.AnyAsync(x => x.RoleId == 1 && x.PermissionId == updated.Id));
        Assert.True(await db.RoleWiseMenuAccesses.AnyAsync(x => x.RoleId == 1 && x.ProjectId == 12 && x.MenuItemId == 88 && x.IsAllowed));
        Assert.Equal(1, await db.Permissions.CountAsync(x => x.Code == code));
    }

    private static PlatformDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"menu-permissions-{Guid.NewGuid():N}")
            .Options;
        return new PlatformDbContext(options);
    }
}
