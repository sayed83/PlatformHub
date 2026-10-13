using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class ScopedAccessServiceTests
{
    [Fact]
    public async Task Branch_assignment_does_not_grant_access_to_another_tenant()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        var allowed = await service.CanAccessAsync(10, "Project.View", 2, branchId: 201);

        Assert.False(allowed);
    }

    [Fact]
    public async Task Branch_assignment_allows_only_the_assigned_branch_in_its_tenant()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.True(await service.CanAccessAsync(10, "Project.View", 1, branchId: 101));
        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, branchId: 102));
    }

    [Fact]
    public async Task Assignment_without_permission_is_denied()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.False(await service.CanAccessAsync(10, "User.Create", 1, branchId: 101));
    }

    [Fact]
    public async Task Inactive_user_is_denied()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        var user = await db.Users.SingleAsync(x => x.Id == 10);
        user.IsActive = false;
        await db.SaveChangesAsync();
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, branchId: 101));
    }

    [Fact]
    public async Task Project_assignment_allows_access_only_for_tenant_with_active_subscription()
    {
        await using var db = CreateDb();
        await SeedReusableProjectAssignmentAsync(db, tenantId: 1, subscriptionEnabled: true);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.True(await service.CanAccessAsync(10, "Project.View", 1, projectId: 501));
        Assert.False(await service.CanAccessAsync(10, "Project.View", 2, projectId: 501));
    }

    [Fact]
    public async Task Project_assignment_is_denied_when_tenant_subscription_is_disabled()
    {
        await using var db = CreateDb();
        await SeedReusableProjectAssignmentAsync(db, tenantId: 1, subscriptionEnabled: false);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, projectId: 501));
    }

    [Fact]
    public async Task Tenantless_assignment_to_reusable_project_does_not_grant_tenant_access()
    {
        await using var db = CreateDb();
        await SeedReusableProjectAssignmentAsync(db, tenantId: null, subscriptionEnabled: true);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        // A shared project subscription is not a substitute for an explicit tenant-scoped assignment.
        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, projectId: 501));
    }


    [Fact]
    public async Task Branch_id_from_another_tenant_is_denied_even_with_valid_tenant_assignment()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 101, UserId = 10, RoleId = 5, TenantId = 1
        });
        await db.SaveChangesAsync();
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, branchId: 201));
    }

    [Fact]
    public async Task Non_positive_scope_ids_are_denied()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, branchId: 0));
        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, projectId: -1));
    }

    [Fact]
    public async Task Tenant_wide_assignment_does_not_override_a_project_assignment_mismatch()
    {
        await using var db = CreateDb();
        await SeedReusableProjectAssignmentAsync(db, tenantId: 1, subscriptionEnabled: true);
        db.Projects.Add(new Project
        {
            Id = 502, TenantId = 2, Code = "OTHER-APP", Name = "Other Tenant App", IsActive = true
        });
        await db.SaveChangesAsync();
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, projectId: 502));
    }

    [Fact]
    public async Task Inactive_tenant_is_denied_even_when_user_has_role_assignment()
    {
        await using var db = CreateDb();
        await SeedReusableProjectAssignmentAsync(db, tenantId: 1, subscriptionEnabled: true);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == 1);
        tenant.IsActive = false;
        await db.SaveChangesAsync();

        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);
        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, projectId: 501));
    }

    [Fact]
    public async Task Inactive_project_is_denied_even_when_tenant_subscription_exists()
    {
        await using var db = CreateDb();
        await SeedReusableProjectAssignmentAsync(db, tenantId: 1, subscriptionEnabled: true);
        var project = await db.Projects.SingleAsync(x => x.Id == 501);
        project.IsActive = false;
        await db.SaveChangesAsync();

        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);
        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, projectId: 501));
    }

    [Fact]
    public async Task Missing_permission_is_denied_even_with_valid_tenant_project_assignment()
    {
        await using var db = CreateDb();
        await SeedReusableProjectAssignmentAsync(db, tenantId: 1, subscriptionEnabled: true);
        var service = new ScopedAccessService(db, NullLogger<ScopedAccessService>.Instance);

        Assert.False(await service.CanAccessAsync(10, "User.Delete", 1, projectId: 501));
    }

    private static async Task SeedReusableProjectAssignmentAsync(
        PlatformDbContext db,
        long? tenantId,
        bool subscriptionEnabled)
    {
        db.Users.Add(new AppUser
        {
            Id = 10, UserName = "project-user", DisplayName = "Project User",
            PasswordHash = "test-hash", IsActive = true
        });
        db.Tenants.AddRange(
            new Tenant { Id = 1, Code = "TENANT-1", Name = "Tenant One", IsActive = true },
            new Tenant { Id = 2, Code = "TENANT-2", Name = "Tenant Two", IsActive = true });
        db.Projects.Add(new Project
        {
            Id = 501, TenantId = null, Code = "SHARED-APP", Name = "Shared Application", IsActive = true
        });
        db.TenantProjects.Add(new TenantProject
        {
            Id = 601, TenantId = 1, ProjectId = 501, IsEnabled = subscriptionEnabled
        });
        db.Roles.Add(new PlatformRole { Id = 5, Code = "PROJECT_USER", Name = "Project User", IsActive = true });
        db.Permissions.Add(new PlatformPermission
        {
            Id = 1, Code = "Project.View", Name = "View Projects", Module = "Projects"
        });
        db.RolePermissions.Add(new PlatformRolePermission { RoleId = 5, PermissionId = 1 });
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 100, UserId = 10, RoleId = 5, TenantId = tenantId, ProjectId = 501
        });
        await db.SaveChangesAsync();
    }

    private static PlatformDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"scope-tests-{Guid.NewGuid():N}")
            .Options;
        return new PlatformDbContext(options);
    }

    private static async Task SeedBranchAssignmentAsync(PlatformDbContext db)
    {
        db.Users.Add(new AppUser { Id = 10, UserName = "branch-user", DisplayName = "Branch User", PasswordHash = "test-hash", IsActive = true });
        db.Tenants.AddRange(
            new Tenant { Id = 1, Code = "TENANT-1", Name = "Tenant One", IsActive = true },
            new Tenant { Id = 2, Code = "TENANT-2", Name = "Tenant Two", IsActive = true });
        db.Branches.AddRange(
            new Branch { Id = 101, TenantId = 1, Code = "BR-1", Name = "Branch One", IsActive = true },
            new Branch { Id = 102, TenantId = 1, Code = "BR-2", Name = "Branch Two", IsActive = true },
            new Branch { Id = 201, TenantId = 2, Code = "BR-3", Name = "Branch Other Tenant", IsActive = true });
        db.Roles.Add(new PlatformRole { Id = 5, Code = "BRANCH_ADMIN", Name = "Branch Admin", IsActive = true });
        db.Permissions.AddRange(
            new PlatformPermission { Id = 1, Code = "Project.View", Name = "View Projects", Module = "Projects" },
            new PlatformPermission { Id = 2, Code = "User.Create", Name = "Create Users", Module = "Users" });
        db.RolePermissions.Add(new PlatformRolePermission { RoleId = 5, PermissionId = 1 });
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 100, UserId = 10, RoleId = 5, BranchId = 101
        });
        await db.SaveChangesAsync();
    }
}
