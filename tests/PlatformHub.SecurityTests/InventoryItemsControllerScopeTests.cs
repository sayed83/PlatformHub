using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Api.Controllers;
using PlatformHub.Api.Platform;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class InventoryItemsControllerScopeTests
{
    [Fact]
    public async Task List_returns_only_items_in_the_resolved_tenant_branch_and_project()
    {
        await using var db = CreateDb();
        db.InventoryItemModels.AddRange(
            Item(1, 10, 101, 1001, "TEN10-OWN"),
            Item(2, 20, 101, 1001, "TEN20-OTHER"),
            Item(3, 10, 102, 1001, "BRANCH-OTHER"),
            Item(4, 10, 101, 2002, "PROJECT-OTHER"),
            Item(5, 10, 101, 1001, "TEN10-OWN-2", active: false));
        await db.SaveChangesAsync();

        var controller = new InventoryItemModelsController(db, new ActiveTenantContext(10, 101, 1001));
        var result = await controller.List(null, 1, 50, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<InventoryItemDto>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("TEN10-OWN", items[0].Sku);
    }

    [Fact]
    public async Task Get_returns_not_found_for_an_item_owned_by_another_tenant()
    {
        await using var db = CreateDb();
        db.InventoryItemModels.Add(Item(99, 20, 101, 1001, "OTHER-TENANT"));
        await db.SaveChangesAsync();

        var controller = new InventoryItemModelsController(db, new ActiveTenantContext(10, 101, 1001));
        var result = await controller.Get(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_uses_server_resolved_scope_instead_of_client_scope_fields()
    {
        await using var db = CreateDb();
        var controller = new InventoryItemModelsController(db, new ActiveTenantContext(10, 101, 1001));

        var result = await controller.Create(new SaveInventoryItemRequest("SKU-1", "Widget", "pcs"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var item = Assert.IsType<InventoryItemDto>(created.Value);
        var stored = await db.InventoryItemModels.SingleAsync();
        Assert.Equal(10, stored.TenantId);
        Assert.Equal(101, stored.BranchId);
        Assert.Equal(1001, stored.ProjectId);
        Assert.Equal("SKU-1", item.Sku);
    }

    [Fact]
    public async Task Unresolved_workspace_cannot_create_inventory_item()
    {
        await using var db = CreateDb();
        var controller = new InventoryItemModelsController(db, new ActiveTenantContext());

        var result = await controller.Create(new SaveInventoryItemRequest("SKU-1", "Widget", "pcs"),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(await db.InventoryItemModels.ToListAsync());
    }

    private static InventoryItemModel Item(long id, long tenant, long? branch, long? project,
        string sku, bool active = true) => new()
    {
        Id = id,
        TenantId = tenant,
        BranchId = branch,
        ProjectId = project,
        Sku = sku,
        Name = sku,
        Unit = "pcs",
        IsActive = active
    };

    private static InventoryDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inventory-scope-{Guid.NewGuid():N}")
            .Options);
}
