using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Api.Controllers;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class PlatformAccessControllerScopeTests
{
    [Fact]
    public async Task User_update_rejects_profile_changes_without_UserUpdate_permission()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var result = await CreateController(db).UpdateUser(42,
            new UpdatePlatformUserRequest("Changed Name", true, null), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Tenant User", (await db.Users.SingleAsync(x => x.Id == 42)).DisplayName);
    }

    [Fact]
    public async Task User_update_rejects_status_changes_without_UserDeactivate_permission()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var result = await CreateController(db, "User.Update").UpdateUser(42,
            new UpdatePlatformUserRequest("Tenant User", false, null), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.True((await db.Users.SingleAsync(x => x.Id == 42)).IsActive);
    }

    [Fact]
    public async Task Role_assignment_rejects_inactive_tenant()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == 10);
        tenant.IsActive = false;
        await db.SaveChangesAsync();

        var result = await CreateController(db).AssignRole(42,
            new AssignRoleRequest(5, 10, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.UserRoleAssignments.ToListAsync());
    }

    [Fact]
    public async Task Role_assignment_rejects_inactive_branch()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Branches.Add(new Branch
        {
            Id = 101, TenantId = 10, Code = "BR-101", Name = "Inactive Branch", IsActive = false
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).AssignRole(42,
            new AssignRoleRequest(5, 10, 101, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.UserRoleAssignments.ToListAsync());
    }

    [Fact]
    public async Task Role_assignment_rejects_shared_project_without_active_tenant_subscription()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Projects.Add(new Project
        {
            Id = 501, TenantId = null, Code = "SHARED", Name = "Shared Project", IsActive = true
        });
        db.TenantProjects.Add(new TenantProject
        {
            Id = 601, TenantId = 10, ProjectId = 501, IsEnabled = false
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).AssignRole(42,
            new AssignRoleRequest(5, 10, null, 501), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.UserRoleAssignments.ToListAsync());
    }

    [Fact]
    public async Task Role_assignment_rejects_branch_and_project_from_different_tenants()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Tenants.Add(new Tenant { Id = 20, Code = "TEN-20", Name = "Tenant Two", IsActive = true });
        db.Branches.Add(new Branch
        {
            Id = 101, TenantId = 10, Code = "BR-101", Name = "Tenant One Branch", IsActive = true
        });
        db.Projects.Add(new Project
        {
            Id = 501, TenantId = 20, Code = "TEN-20-PROJECT", Name = "Tenant Two Project", IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).AssignRole(42,
            new AssignRoleRequest(5, 10, 101, 501), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.UserRoleAssignments.ToListAsync());
    }

    private static PlatformAccessController CreateController(PlatformDbContext db, params string[] permissions)
    {
        var claims = new List<Claim> { new("ph_user_id", "1") };
        claims.AddRange(permissions.Select(permission => new Claim("ph_permission", permission)));
        var controller = new PlatformAccessController(db, new PasswordHasher<AppUser>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"))
            }
        };
        return controller;
    }

    private static async Task SeedBaseAsync(PlatformDbContext db)
    {
        db.Users.Add(new AppUser
        {
            Id = 42, UserName = "tenant-user", DisplayName = "Tenant User",
            PasswordHash = "test-hash", IsActive = true
        });
        db.Tenants.Add(new Tenant { Id = 10, Code = "TEN-10", Name = "Tenant Ten", IsActive = true });
        db.Roles.Add(new PlatformRole { Id = 5, Code = "TENANT_USER", Name = "Tenant User", IsActive = true });
        await db.SaveChangesAsync();
    }

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"assignment-scope-{Guid.NewGuid():N}")
            .Options);
}
