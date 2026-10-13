using Microsoft.EntityFrameworkCore;
using PlatformHub.Api.Services;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class EffectiveMenuAccessServiceTests
{
    [Fact]
    public async Task Parent_included_for_navigation_does_not_grant_direct_access()
    {
        await using var db = await CreateDbAsync();
        var service = new EffectiveMenuAccessService(db);

        var menus = await service.GetForUserAsync(10, 100, 1);
        Assert.Contains(menus, x => x.Code == "PARENT");
        Assert.Contains(menus, x => x.Code == "CHILD");
        Assert.False(await service.HasAccessAsync(10, 100, 1, "PARENT"));
        Assert.True(await service.HasAccessAsync(10, 100, 1, "CHILD"));
    }

    [Fact]
    public async Task Explicit_user_deny_overrides_role_allow()
    {
        await using var db = await CreateDbAsync();
        db.UserWiseMenuAccesses.Add(new UserWiseMenuAccess
        {
            ProjectId = 100, UserId = 10, MenuItemId = 2, IsAllowed = false
        });
        await db.SaveChangesAsync();

        var service = new EffectiveMenuAccessService(db);
        Assert.False(await service.HasAccessAsync(10, 100, 1, "CHILD"));
    }

    [Fact]
    public async Task Inactive_user_cannot_receive_or_access_project_menus()
    {
        await using var db = await CreateDbAsync();
        var user = await db.Users.SingleAsync(x => x.Id == 10);
        user.IsActive = false;
        await db.SaveChangesAsync();

        var service = new EffectiveMenuAccessService(db);
        Assert.Empty(await service.GetForUserAsync(10, 100, 1));
        Assert.False(await service.HasAccessAsync(10, 100, 1, "CHILD"));
    }

    [Fact]
    public async Task Project_scoped_assignment_does_not_grant_access_to_another_project()
    {
        await using var db = await CreateDbAsync();
        db.Tenants.Add(new Tenant { Id = 2, Code = "T2", Name = "Tenant Two", IsActive = true });
        db.Projects.Add(new Project { Id = 200, TenantId = 2, Code = "P2", Name = "Other Project", IsActive = true });
        db.MenuItems.Add(new MenuItem { Id = 3, ProjectId = 200, Code = "OTHER", Name = "Other", IsActive = true });
        db.RoleWiseMenuAccesses.Add(new RoleWiseMenuAccess
        {
            ProjectId = 200, RoleId = 5, MenuItemId = 3, IsAllowed = true
        });
        await db.SaveChangesAsync();

        var service = new EffectiveMenuAccessService(db);
        Assert.Empty(await service.GetForUserAsync(10, 200, 1));
        Assert.False(await service.HasAccessAsync(10, 200, 1, "OTHER"));
    }

    [Fact]
    public async Task Tenantless_assignment_to_shared_project_does_not_grant_menu_access()
    {
        await using var db = await CreateDbAsync();
        db.Projects.Add(new Project
        {
            Id = 300, TenantId = null, Code = "SHARED", Name = "Shared Project", IsActive = true
        });
        db.TenantProjects.Add(new TenantProject
        {
            Id = 301, TenantId = 1, ProjectId = 300, IsEnabled = true
        });
        db.MenuItems.Add(new MenuItem
        {
            Id = 30, ProjectId = 300, Code = "SHARED_MENU", Name = "Shared Menu", IsActive = true
        });
        db.RoleWiseMenuAccesses.Add(new RoleWiseMenuAccess
        {
            ProjectId = 300, RoleId = 5, MenuItemId = 30, IsAllowed = true
        });
        // Deliberately tenantless: subscription to a reusable project must not turn this
        // assignment into tenant access.
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 302, UserId = 10, RoleId = 5, ProjectId = 300, TenantId = null
        });
        await db.SaveChangesAsync();

        var service = new EffectiveMenuAccessService(db);
        Assert.Empty(await service.GetForUserAsync(10, 300, 1));
        Assert.False(await service.HasAccessAsync(10, 300, 1, "SHARED_MENU"));
    }

    [Fact]
    public async Task Shared_project_menu_access_is_limited_to_the_selected_tenant()
    {
        await using var db = await CreateDbAsync();
        db.Tenants.Add(new Tenant { Id = 2, Code = "T2", Name = "Tenant Two", IsActive = true });
        db.Projects.Add(new Project
        {
            Id = 400, TenantId = null, Code = "REUSABLE", Name = "Reusable Project", IsActive = true
        });
        db.TenantProjects.AddRange(
            new TenantProject { Id = 401, TenantId = 1, ProjectId = 400, IsEnabled = true },
            new TenantProject { Id = 402, TenantId = 2, ProjectId = 400, IsEnabled = true });
        db.MenuItems.Add(new MenuItem
        {
            Id = 40, ProjectId = 400, Code = "REUSABLE_MENU", Name = "Reusable Menu", IsActive = true
        });
        db.RoleWiseMenuAccesses.Add(new RoleWiseMenuAccess
        {
            ProjectId = 400, RoleId = 5, MenuItemId = 40, IsAllowed = true
        });
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 403, UserId = 10, RoleId = 5, TenantId = 2, ProjectId = 400
        });
        await db.SaveChangesAsync();

        var service = new EffectiveMenuAccessService(db);
        Assert.Empty(await service.GetForUserAsync(10, 400, 1));
        Assert.False(await service.HasAccessAsync(10, 400, 1, "REUSABLE_MENU"));
        Assert.Contains(await service.GetForUserAsync(10, 400, 2), x => x.Code == "REUSABLE_MENU");
        Assert.True(await service.HasAccessAsync(10, 400, 2, "REUSABLE_MENU"));
    }

    private static async Task<PlatformDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"menu-tests-{Guid.NewGuid():N}")
            .Options;
        var db = new PlatformDbContext(options);
        db.Tenants.Add(new Tenant { Id = 1, Code = "T1", Name = "Tenant One", IsActive = true });
        db.Projects.Add(new Project { Id = 100, TenantId = 1, Code = "P1", Name = "Project One", IsActive = true });
        db.Users.Add(new AppUser { Id = 10, UserName = "test-user", DisplayName = "Test User", PasswordHash = "test-hash", IsActive = true });
        db.Roles.Add(new PlatformRole { Id = 5, Code = "TEST_ROLE", Name = "Test Role", IsActive = true });
        db.UserRoleAssignments.Add(new UserRoleAssignment { Id = 50, UserId = 10, RoleId = 5, TenantId = 1, ProjectId = 100 });
        db.MenuItems.AddRange(
            new MenuItem { Id = 1, ProjectId = 100, Code = "PARENT", Name = "Parent", IsActive = true },
            new MenuItem { Id = 2, ProjectId = 100, ParentMenuId = 1, Code = "CHILD", Name = "Child", IsActive = true });
        db.RoleWiseMenuAccesses.Add(new RoleWiseMenuAccess { ProjectId = 100, RoleId = 5, MenuItemId = 1, IsAllowed = false });
        db.RoleWiseMenuAccesses.Add(new RoleWiseMenuAccess { ProjectId = 100, RoleId = 5, MenuItemId = 2, IsAllowed = true });
        await db.SaveChangesAsync();
        return db;
    }
}
