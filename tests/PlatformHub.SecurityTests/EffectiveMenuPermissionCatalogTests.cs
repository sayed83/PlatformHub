using Microsoft.EntityFrameworkCore;
using PlatformHub.Api.Services;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class EffectiveMenuPermissionCatalogTests
{
    [Fact]
    public async Task Catalog_permission_grants_menu_and_explicit_user_deny_overrides_it()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"effective-menu-catalog-{Guid.NewGuid():N}")
            .Options);
        db.Tenants.Add(new Tenant { Id = 1, Code = "TENANT1", Name = "Tenant One", IsActive = true });
        db.Projects.Add(new Project { Id = 12, TenantId = 1, Code = "INVENTORY", Name = "Inventory", IsActive = true });
        db.Users.Add(new AppUser
        {
            Id = 10, UserName = "tenant-user", DisplayName = "Tenant User",
            PasswordHash = "test-hash", IsActive = true
        });
        db.Roles.Add(new PlatformRole { Id = 5, TenantId = 1, Code = "INVENTORY_VIEWER", Name = "Inventory Viewer", IsActive = true });
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 1, UserId = 10, RoleId = 5, TenantId = 1, ProjectId = 12
        });
        db.MenuItems.Add(new MenuItem
        {
            Id = 88, ProjectId = 12, Code = "PRODUCTS", Name = "Products", IsActive = true
        });
        await db.SaveChangesAsync();

        await ProjectMenuPermissionSynchronizer.SyncAllAsync(db);
        var permissionCode = ProjectMenuPermissionSynchronizer.BuildViewPermissionCode(12, 88);
        var permission = await db.Permissions.SingleAsync(x => x.Code == permissionCode);
        db.RolePermissions.Add(new PlatformRolePermission { RoleId = 5, PermissionId = permission.Id });
        await db.SaveChangesAsync();

        var service = new EffectiveMenuAccessService(db);
        var visibleMenus = await service.GetForUserAsync(10, 12, 1);
        Assert.Contains(visibleMenus, menu => menu.Id == 88);
        Assert.True(await service.HasAccessAsync(10, 12, 1, "PRODUCTS"));

        db.UserWiseMenuAccesses.Add(new UserWiseMenuAccess
        {
            Id = 1, ProjectId = 12, UserId = 10, MenuItemId = 88, IsAllowed = false
        });
        await db.SaveChangesAsync();

        visibleMenus = await service.GetForUserAsync(10, 12, 1);
        Assert.DoesNotContain(visibleMenus, menu => menu.Id == 88);
        Assert.False(await service.HasAccessAsync(10, 12, 1, "PRODUCTS"));
    }
}
