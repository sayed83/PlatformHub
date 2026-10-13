using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class InventoryDbContextTableNamingTests
{
    [Fact]
    public void AccountHead_MapsToPluralNewTable_AndPreservesTenantScope()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlatformHubModelTests;Trusted_Connection=True;")
            .Options;

        using var db = new InventoryDbContext(options);
        var entity = db.Model.FindEntityType(typeof(AccountHead));

        Assert.NotNull(entity);
        Assert.Equal("AccountHeads", entity!.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());

        Assert.NotNull(entity.FindProperty(nameof(AccountHead.TenantId)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.BranchId)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.ProjectId)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.ParentAccountId)));

        var codeIndex = Assert.Single(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "BranchId", "ProjectId", "Code" }));
        Assert.True(codeIndex.IsUnique);
    }

    [Fact]
    public void ExistingProductTableName_RemainsUnchanged()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlatformHubModelTests;Trusted_Connection=True;")
            .Options;

        using var db = new InventoryDbContext(options);
        var entity = db.Model.FindEntityType(typeof(InventoryItem));

        Assert.NotNull(entity);
        Assert.Equal("Products", entity!.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());
    }
}
