using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class PlatformDbContextTableNamingTests
{
    [Fact]
    public void Access_tables_use_plural_names_without_model_suffix()
    {
        using var db = CreateDb();

        Assert.Equal("RoleWiseMenuAccesses",
            db.Model.FindEntityType(typeof(RoleWiseMenuAccess))!.GetTableName());
        Assert.Equal("UserWiseMenuAccesses",
            db.Model.FindEntityType(typeof(UserWiseMenuAccess))!.GetTableName());
    }

    [Fact]
    public void Tenant_and_branch_table_mappings_remain_unchanged()
    {
        using var db = CreateDb();

        Assert.Equal("Tenants", db.Model.FindEntityType(typeof(Tenant))!.GetTableName());
        Assert.Equal("Branches", db.Model.FindEntityType(typeof(Branch))!.GetTableName());
    }

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"platform-table-names-{Guid.NewGuid():N}")
            .Options);
}
