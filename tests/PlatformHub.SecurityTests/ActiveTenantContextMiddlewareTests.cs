using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PlatformHub.Api.Platform;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class ActiveTenantContextMiddlewareTests
{
    [Fact]
    public async Task Project_only_assignment_cannot_resolve_shared_project_for_arbitrary_tenant()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"workspace-middleware-{Guid.NewGuid():N}")
            .Options);

        var user = new AppUser
        {
            Id = 42,
            UserName = "tenantless-project-user",
            DisplayName = "Tenantless Project User",
            PasswordHash = "test-hash",
            IsActive = true
        };
        var tenant = new Tenant { Id = 10, Code = "TEN-10", Name = "Tenant 10", IsActive = true };
        var sharedProject = new Project
        {
            Id = 1001,
            TenantId = null,
            Code = "SHARED-1",
            Name = "Shared Project",
            IsActive = true
        };
        var role = new PlatformRole
        {
            Id = 5,
            Code = "PROJECT_USER",
            Name = "Project User",
            IsActive = true
        };

        db.Users.Add(user);
        db.Tenants.Add(tenant);
        db.Projects.Add(sharedProject);
        db.Roles.Add(role);
        db.TenantProjects.Add(new TenantProject
        {
            Id = 1,
            TenantId = tenant.Id,
            ProjectId = sharedProject.Id,
            IsEnabled = true,
            AssignedUtc = DateTime.UtcNow
        });
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 1,
            UserId = user.Id,
            RoleId = role.Id,
            TenantId = null,
            BranchId = null,
            ProjectId = sharedProject.Id
        });
        await db.SaveChangesAsync();

        var contextResolved = false;
        var middleware = new ActiveTenantContextMiddleware(_ =>
        {
            contextResolved = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("ph_user_id", user.Id.ToString()) },
            authenticationType: "test"));
        http.Request.Headers["X-Platform-TenantId"] = tenant.Id.ToString();
        http.Request.Headers["X-Platform-ProjectId"] = sharedProject.Id.ToString();

        await middleware.InvokeAsync(
            http,
            db,
            new ActiveTenantContext(),
            NullLogger<ActiveTenantContextMiddleware>.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        Assert.False(contextResolved);
    }

    [Fact]
    public async Task Inactive_user_cannot_resolve_workspace_with_existing_role_assignment()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"inactive-workspace-user-{Guid.NewGuid():N}")
            .Options);

        var user = new AppUser
        {
            Id = 42,
            UserName = "inactive-user",
            DisplayName = "Inactive User",
            PasswordHash = "test-hash",
            IsActive = false
        };
        var tenant = new Tenant { Id = 10, Code = "TEN-10", Name = "Tenant 10", IsActive = true };
        var role = new PlatformRole { Id = 5, Code = "TENANT_USER", Name = "Tenant User", IsActive = true };

        db.Users.Add(user);
        db.Tenants.Add(tenant);
        db.Roles.Add(role);
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = 1, UserId = user.Id, RoleId = role.Id, TenantId = tenant.Id
        });
        await db.SaveChangesAsync();

        var nextCalled = false;
        var middleware = new ActiveTenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("ph_user_id", user.Id.ToString()) },
            authenticationType: "test"));
        http.Request.Headers["X-Platform-TenantId"] = tenant.Id.ToString();

        await middleware.InvokeAsync(
            http, db, new ActiveTenantContext(),
            NullLogger<ActiveTenantContextMiddleware>.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        Assert.False(nextCalled);
    }
}
