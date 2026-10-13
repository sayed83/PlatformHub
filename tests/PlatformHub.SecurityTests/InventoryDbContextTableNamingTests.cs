using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class InventoryDbContextTableNamingTests
{
    private static InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PlatformHubModelTests;Trusted_Connection=True;")
            .Options;
        return new InventoryDbContext(options);
    }

    [Fact]
    public void AccountHead_MapsInventoryFieldsToAccHeads_AndPreservesTenantScope()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(AccountHead));

        Assert.NotNull(entity);
        Assert.Equal("AccHeads", entity!.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());

        Assert.NotNull(entity.FindProperty(nameof(AccountHead.TenantId)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.BranchId)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.ProjectId)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.AccHeadName)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.AccHeadCode)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.AccType)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.AccCategory)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.ParentAccHeadId)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.DefaultTaxRate)));
        Assert.NotNull(entity.FindProperty(nameof(AccountHead.BudgetAmount)));

        var codeIndex = Assert.Single(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "BranchId", "ProjectId", "AccHeadCode" }));
        Assert.True(codeIndex.IsUnique);
    }

    [Fact]
    public void SystemAccHeadSetting_MapsToPluralTableAndUsesTenantInsteadOfCompany()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(SystemAccHeadSetting));

        Assert.NotNull(entity);
        Assert.Equal("SystemAccHeadSettings", entity!.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());
        Assert.NotNull(entity.FindProperty(nameof(SystemAccHeadSetting.TenantId)));
        Assert.NotNull(entity.FindProperty(nameof(SystemAccHeadSetting.BranchId)));
        Assert.NotNull(entity.FindProperty(nameof(SystemAccHeadSetting.CreatedBy)));
        Assert.Null(entity.FindProperty("ComId"));
    }

    [Fact]
    public void ExistingProductTableName_RemainsUnchanged()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(InventoryItem));

        Assert.NotNull(entity);
        Assert.Equal("Products", entity!.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());
    }
}
