using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PlatformHub.Api.Authorization;
using PlatformHub.Api.Platform;
using PlatformHub.Infrastructure.Platform;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class RequireScopedPermissionAttributeTests
{
    [Fact]
    public async Task Unresolved_workspace_is_denied_without_calling_access_service()
    {
        var access = new RecordingScopedAccessService { Result = true };
        var filter = CreateFilter(new ActiveTenantContext(), access, query: null);

        await new RequireScopedPermissionAttribute("Sales.View").OnAuthorizationAsync(filter);

        Assert.IsType<ForbidResult>(filter.Result);
        Assert.Equal(0, access.CallCount);
    }

    [Fact]
    public async Task Client_cannot_override_active_project()
    {
        var access = new RecordingScopedAccessService { Result = true };
        var filter = CreateFilter(new ActiveTenantContext(10, 101, 1001), access, "?projectId=2002");

        await new RequireScopedPermissionAttribute("Sales.View").OnAuthorizationAsync(filter);

        Assert.IsType<ForbidResult>(filter.Result);
        Assert.Equal(0, access.CallCount);
    }

    [Fact]
    public async Task Matching_scope_checks_database_permission_using_resolved_ids()
    {
        var access = new RecordingScopedAccessService { Result = false };
        var filter = CreateFilter(new ActiveTenantContext(10, 101, 1001), access, "?branchId=101&projectId=1001");

        await new RequireScopedPermissionAttribute("Sales.View").OnAuthorizationAsync(filter);

        Assert.IsType<ForbidResult>(filter.Result);
        Assert.Equal(1, access.CallCount);
        Assert.Equal(42, access.UserId);
        Assert.Equal("Sales.View", access.PermissionCode);
        Assert.Equal(10, access.TenantId);
        Assert.Equal(101, access.BranchId);
        Assert.Equal(1001, access.ProjectId);
    }

    [Fact]
    public async Task Matching_scope_and_permission_are_allowed()
    {
        var access = new RecordingScopedAccessService { Result = true };
        var filter = CreateFilter(new ActiveTenantContext(10, 101, 1001), access, query: null);

        await new RequireScopedPermissionAttribute("Sales.View").OnAuthorizationAsync(filter);

        Assert.Null(filter.Result);
        Assert.Equal(1, access.CallCount);
    }

    private static AuthorizationFilterContext CreateFilter(
        IActiveTenantContext activeScope,
        RecordingScopedAccessService access,
        string? query)
    {
        var services = new ServiceCollection();
        services.AddSingleton(activeScope);
        services.AddSingleton<IActiveTenantContext>(activeScope);
        services.AddSingleton<IScopedAccessService>(access);
        var provider = services.BuildServiceProvider();

        var http = new DefaultHttpContext
        {
            RequestServices = provider,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("ph_user_id", "42") }, authenticationType: "test"))
        };
        if (!string.IsNullOrWhiteSpace(query))
            http.Request.QueryString = new QueryString(query);

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private sealed class RecordingScopedAccessService : IScopedAccessService
    {
        public bool Result { get; init; }
        public int CallCount { get; private set; }
        public long UserId { get; private set; }
        public string PermissionCode { get; private set; } = "";
        public long TenantId { get; private set; }
        public long? BranchId { get; private set; }
        public long? ProjectId { get; private set; }

        public Task<bool> CanAccessAsync(
            long userId,
            string permissionCode,
            long tenantId,
            long? branchId = null,
            long? projectId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            UserId = userId;
            PermissionCode = permissionCode;
            TenantId = tenantId;
            BranchId = branchId;
            ProjectId = projectId;
            return Task.FromResult(Result);
        }
    }
}
