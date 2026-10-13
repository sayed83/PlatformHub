using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Api.Authorization;
using PlatformHub.Api.Platform;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/inventory/items")]
public sealed class InventoryItemsController(
    InventoryDbContext db,
    IActiveTenantContext scope) : ControllerBase
{
    [HttpGet]
    [RequireScopedPermission("Inventory.Item.View")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!TryGetScope(out var tenantId, out var branchId, out var projectId))
            return Forbid();

        page = Math.Clamp(page, 1, 10_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ScopedQuery(tenantId, branchId, projectId).Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Sku.Contains(term) || x.Name.Contains(term));
        }

        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new InventoryItemDto(x.Id, x.Sku, x.Name, x.Unit, x.IsActive, x.CreatedUtc, x.UpdatedUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:long}")]
    [RequireScopedPermission("Inventory.Item.View")]
    public async Task<ActionResult<InventoryItemDto>> Get(long id, CancellationToken ct)
    {
        if (!TryGetScope(out var tenantId, out var branchId, out var projectId))
            return Forbid();

        var item = await ScopedQuery(tenantId, branchId, projectId)
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new InventoryItemDto(x.Id, x.Sku, x.Name, x.Unit, x.IsActive, x.CreatedUtc, x.UpdatedUtc))
            .SingleOrDefaultAsync(ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequireScopedPermission("Inventory.Item.Create")]
    public async Task<ActionResult<InventoryItemDto>> Create(
        [FromBody] SaveInventoryItemRequest request, CancellationToken ct)
    {
        if (!TryGetScope(out var tenantId, out var branchId, out var projectId))
            return Forbid();
        if (!TryValidate(request, out var sku, out var name, out var unit, out var error))
            return BadRequest(new { message = error });

        var duplicate = await ScopedQuery(tenantId, branchId, projectId)
            .AnyAsync(x => x.Sku == sku, ct);
        if (duplicate)
            return Conflict(new { message = "An item with this SKU already exists in the active workspace." });

        var now = DateTime.UtcNow;
        var item = new InventoryItem
        {
            TenantId = tenantId,
            BranchId = branchId,
            ProjectId = projectId,
            Sku = sku,
            Name = name,
            Unit = unit,
            CreatedUtc = now,
            UpdatedUtc = now,
            IsActive = true
        };
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id },
            new InventoryItemDto(item.Id, item.Sku, item.Name, item.Unit, item.IsActive, item.CreatedUtc, item.UpdatedUtc));
    }

    [HttpPut("{id:long}")]
    [RequireScopedPermission("Inventory.Item.Update")]
    public async Task<ActionResult<InventoryItemDto>> Update(
        long id, [FromBody] SaveInventoryItemRequest request, CancellationToken ct)
    {
        if (!TryGetScope(out var tenantId, out var branchId, out var projectId))
            return Forbid();
        if (!TryValidate(request, out var sku, out var name, out var unit, out var error))
            return BadRequest(new { message = error });

        var item = await ScopedQuery(tenantId, branchId, projectId)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (item is null) return NotFound();

        var duplicate = await ScopedQuery(tenantId, branchId, projectId)
            .AnyAsync(x => x.Id != id && x.Sku == sku, ct);
        if (duplicate)
            return Conflict(new { message = "An item with this SKU already exists in the active workspace." });

        item.Sku = sku;
        item.Name = name;
        item.Unit = unit;
        item.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new InventoryItemDto(item.Id, item.Sku, item.Name, item.Unit, item.IsActive, item.CreatedUtc, item.UpdatedUtc));
    }

    [HttpDelete("{id:long}")]
    [RequireScopedPermission("Inventory.Item.Deactivate")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        if (!TryGetScope(out var tenantId, out var branchId, out var projectId))
            return Forbid();
        var item = await ScopedQuery(tenantId, branchId, projectId)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (item is null) return NotFound();

        // Soft deactivate to preserve references for future stock movements and audit.
        item.IsActive = false;
        item.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private IQueryable<InventoryItem> ScopedQuery(long tenantId, long? branchId, long? projectId)
    {
        var query = db.InventoryItems.Where(x => x.TenantId == tenantId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        else query = query.Where(x => x.BranchId == null);
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        else query = query.Where(x => x.ProjectId == null);
        return query;
    }

    private bool TryGetScope(out long tenantId, out long? branchId, out long? projectId)
    {
        tenantId = scope.TenantId ?? 0;
        branchId = scope.BranchId;
        projectId = scope.ProjectId;
        return scope.IsResolved && tenantId > 0;
    }

    private static bool TryValidate(SaveInventoryItemRequest request, out string sku, out string name,
        out string unit, out string? error)
    {
        sku = request.Sku?.Trim() ?? string.Empty;
        name = request.Name?.Trim() ?? string.Empty;
        unit = string.IsNullOrWhiteSpace(request.Unit) ? "pcs" : request.Unit.Trim();
        error = null;
        if (sku.Length is < 1 or > 80) error = "SKU is required and must be at most 80 characters.";
        else if (name.Length is < 1 or > 200) error = "Name is required and must be at most 200 characters.";
        else if (unit.Length is < 1 or > 30) error = "Unit must be at most 30 characters.";
        return error is null;
    }
}

public sealed record SaveInventoryItemRequest(string? Sku, string? Name, string? Unit);
public sealed record InventoryItemDto(long Id, string Sku, string Name, string Unit, bool IsActive,
    DateTime CreatedUtc, DateTime UpdatedUtc);
