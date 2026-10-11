using Microsoft.EntityFrameworkCore;
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
        var service = new ScopedAccessService(db);

        var allowed = await service.CanAccessAsync(10, "Project.View", 2, branchId: 201);

        Assert.False(allowed);
    }

    [Fact]
    public async Task Branch_assignment_allows_only_the_assigned_branch_in_its_tenant()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        var service = new ScopedAccessService(db);

        Assert.True(await service.CanAccessAsync(10, "Project.View", 1, branchId: 101));
        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, branchId: 102));
    }

    [Fact]
    public async Task Assignment_without_permission_is_denied()
    {
        await using var db = CreateDb();
        await SeedBranchAssignmentAsync(db);
        var service = new ScopedAccessService(db);

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
        var service = new ScopedAccessService(db);

        Assert.False(await service.CanAccessAsync(10, "Project.View", 1, branchId: 101));
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
