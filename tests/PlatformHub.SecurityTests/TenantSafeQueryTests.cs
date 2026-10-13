using Microsoft.EntityFrameworkCore;
using PlatformHub.Api.Platform;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class TenantSafeQueryTests
{
    [Fact]
    public async Task Active_workspace_query_filters_tenant_branch_and_project()
    {
        await using var db = new TenantTestDbContext(new DbContextOptionsBuilder<TenantTestDbContext>()
            .UseInMemoryDatabase($"tenant-query-{Guid.NewGuid():N}").Options);

        db.Rows.AddRange(
            new TestScopedRow { Id = 1, TenantId = 10, BranchId = 101, ProjectId = 1001, Name = "Allowed" },
            new TestScopedRow { Id = 2, TenantId = 10, BranchId = 102, ProjectId = 1001, Name = "Other branch" },
            new TestScopedRow { Id = 3, TenantId = 10, BranchId = 101, ProjectId = 1002, Name = "Other project" },
            new TestScopedRow { Id = 4, TenantId = 20, BranchId = 201, ProjectId = 2001, Name = "Other tenant" });
        await db.SaveChangesAsync();

        var scope = new ActiveTenantContext(10, 101, 1001);
        var result = await db.Rows
            .WhereInActiveWorkspace(scope, nameof(TestScopedRow.TenantId),
                nameof(TestScopedRow.BranchId), nameof(TestScopedRow.ProjectId))
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Equal(new long[] { 1 }, result);
    }

    [Fact]
    public async Task Active_branch_scope_returns_no_rows_when_branch_filter_is_omitted()
    {
        await using var db = new TenantTestDbContext(new DbContextOptionsBuilder<TenantTestDbContext>()
            .UseInMemoryDatabase($"tenant-query-missing-branch-{Guid.NewGuid():N}").Options);
        db.Rows.AddRange(
            new TestScopedRow { Id = 1, TenantId = 10, BranchId = 101, ProjectId = 1001, Name = "Selected branch" },
            new TestScopedRow { Id = 2, TenantId = 10, BranchId = 102, ProjectId = 1001, Name = "Other branch" });
        await db.SaveChangesAsync();

        var result = await db.Rows
            .WhereInActiveWorkspace(new ActiveTenantContext(10, 101, 1001),
                nameof(TestScopedRow.TenantId), projectIdProperty: nameof(TestScopedRow.ProjectId))
            .ToListAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task Active_project_scope_returns_no_rows_when_project_filter_is_omitted()
    {
        await using var db = new TenantTestDbContext(new DbContextOptionsBuilder<TenantTestDbContext>()
            .UseInMemoryDatabase($"tenant-query-missing-project-{Guid.NewGuid():N}").Options);
        db.Rows.AddRange(
            new TestScopedRow { Id = 1, TenantId = 10, BranchId = 101, ProjectId = 1001, Name = "Selected project" },
            new TestScopedRow { Id = 2, TenantId = 10, BranchId = 101, ProjectId = 1002, Name = "Other project" });
        await db.SaveChangesAsync();

        var result = await db.Rows
            .WhereInActiveWorkspace(new ActiveTenantContext(10, 101, 1001),
                nameof(TestScopedRow.TenantId), branchIdProperty: nameof(TestScopedRow.BranchId))
            .ToListAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task Unresolved_workspace_returns_no_tenant_rows()
    {
        await using var db = new TenantTestDbContext(new DbContextOptionsBuilder<TenantTestDbContext>()
            .UseInMemoryDatabase($"tenant-query-empty-{Guid.NewGuid():N}").Options);
        db.Rows.Add(new TestScopedRow
            { Id = 1, TenantId = 10, BranchId = 101, ProjectId = 1001, Name = "Must not leak" });
        await db.SaveChangesAsync();

        var result = await db.Rows
            .WhereInActiveTenant(new ActiveTenantContext(), nameof(TestScopedRow.TenantId))
            .ToListAsync();

        Assert.Empty(result);
    }

    public sealed class TestScopedRow
    {
        public long Id { get; set; }
        public long TenantId { get; set; }
        public long BranchId { get; set; }
        public long ProjectId { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class TenantTestDbContext(DbContextOptions<TenantTestDbContext> options) : DbContext(options)
    {
        public DbSet<TestScopedRow> Rows => Set<TestScopedRow>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestScopedRow>().HasKey(x => x.Id);
        }
    }
}
