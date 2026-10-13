using Microsoft.EntityFrameworkCore;
using PlatformHub.Api.Platform;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class TenantSafeQueryTests
{
    [Fact]
    public async Task Active_workspace_query_filters_tenant_branch_and_project()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"tenant-query-{Guid.NewGuid():N}").Options);

        db.Set<TestScopedRow>().AddRange(
            new TestScopedRow { Id = 1, TenantId = 10, BranchId = 101, ProjectId = 1001, Name = "Allowed" },
            new TestScopedRow { Id = 2, TenantId = 10, BranchId = 102, ProjectId = 1001, Name = "Other branch" },
            new TestScopedRow { Id = 3, TenantId = 10, BranchId = 101, ProjectId = 1002, Name = "Other project" },
            new TestScopedRow { Id = 4, TenantId = 20, BranchId = 201, ProjectId = 2001, Name = "Other tenant" });
        await db.SaveChangesAsync();

        var scope = new ActiveTenantContext(10, 101, 1001);
        var result = await db.Set<TestScopedRow>()
            .WhereInActiveWorkspace(scope, nameof(TestScopedRow.TenantId),
                nameof(TestScopedRow.BranchId), nameof(TestScopedRow.ProjectId))
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Equal(new[] { 1 }, result);
    }

    [Fact]
    public async Task Unresolved_workspace_returns_no_tenant_rows()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"tenant-query-empty-{Guid.NewGuid():N}").Options);
        db.Set<TestScopedRow>().Add(new TestScopedRow
            { Id = 1, TenantId = 10, BranchId = 101, ProjectId = 1001, Name = "Must not leak" });
        await db.SaveChangesAsync();

        var result = await db.Set<TestScopedRow>()
            .WhereInActiveTenant(new ActiveTenantContext(), nameof(TestScopedRow.TenantId))
            .ToListAsync();

        Assert.Empty(result);
    }

    private sealed class TestScopedRow
    {
        public long Id { get; set; }
        public long TenantId { get; set; }
        public long BranchId { get; set; }
        public long ProjectId { get; set; }
        public string Name { get; set; } = "";
    }
}
