using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/project-menus")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class ProjectMenusController(PlatformDbContext db) : ControllerBase
{
    [HttpGet("projects")]
    public async Task<IActionResult> Projects(CancellationToken ct) =>
        Ok(await db.Projects.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.TenantId, TenantName = x.Tenant.Name, x.Code, x.Name })
            .ToListAsync(ct));


    [HttpGet("next-sort-order")]
    public async Task<IActionResult> NextSortOrder(
        [FromQuery] long projectId,
        [FromQuery] long? moduleId,
        CancellationToken ct)
    {
        if (projectId <= 0)
            return BadRequest(new { message = "Select a valid project." });

        if (!await db.Projects.AnyAsync(x => x.Id == projectId, ct))
            return NotFound(new { message = "Project was not found." });

        if (moduleId.HasValue &&
            !await db.ProjectModules.AnyAsync(x => x.Id == moduleId.Value && x.ProjectId == projectId, ct))
            return BadRequest(new { message = "Selected module does not belong to this project." });

        var lastSortOrder = await db.MenuItems.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.ModuleId == moduleId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct) ?? 0;

        return Ok(new { projectId, moduleId, sortOrder = lastSortOrder + 1 });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] long projectId, CancellationToken ct)
    {
        if (projectId <= 0)
            return BadRequest(new { message = "Select a valid project." });

        var summary = await db.Projects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => new
            {
                ModuleCount = db.ProjectModules.Count(m => m.ProjectId == x.Id),
                MenuCount = db.MenuItems.Count(m => m.ProjectId == x.Id),
                ActiveCount = db.MenuItems.Count(m => m.ProjectId == x.Id && m.IsActive)
            })
            .SingleOrDefaultAsync(ct);

        return summary is null ? NotFound(new { message = "Project was not found." }) : Ok(summary);
    }

    [HttpGet("modules")]
    public async Task<IActionResult> Modules([FromQuery] long projectId, CancellationToken ct)
    {
        if (projectId <= 0) return BadRequest(new { message = "Select a valid project." });
        return Ok(await db.ProjectModules.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.ProjectId, x.Code, x.Name, x.Description, x.Icon, x.SortOrder, x.IsActive })
            .ToListAsync(ct));
    }

    [HttpPost("modules")]
    public async Task<IActionResult> SaveModule([FromBody] SaveModuleRequest request, CancellationToken ct)
    {
        if (request.ProjectId <= 0 || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Project, module code and name are required." });
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (code.Length > 50 || name.Length > 200 || (request.Description?.Length ?? 0) > 500 || (request.Icon?.Length ?? 0) > 100)
            return BadRequest(new { message = "Module code must be at most 50, name 200, description 500 and icon 100 characters." });
        if (!await db.Projects.AnyAsync(x => x.Id == request.ProjectId, ct))
            return BadRequest(new { message = "Selected project does not exist." });
        var duplicate = await db.ProjectModules.AnyAsync(x => x.ProjectId == request.ProjectId && x.Code == code && x.Id != request.Id, ct);
        if (duplicate) return Conflict(new { message = "This module code already exists in the project." });
        ProjectModule entity;
        if (request.Id.HasValue)
        {
            var existing = await db.ProjectModules.SingleOrDefaultAsync(x => x.Id == request.Id && x.ProjectId == request.ProjectId, ct);
            if (existing is null) return NotFound(new { message = "Module not found for this project." });
            entity = existing;
        }
        else { entity = new ProjectModule { ProjectId = request.ProjectId }; db.ProjectModules.Add(entity); }
        entity.Code = code; entity.Name = name; entity.Description = request.Description?.Trim();
        entity.Icon = request.Icon?.Trim(); entity.SortOrder = request.SortOrder; entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(new { entity.Id, entity.ProjectId, entity.Code, entity.Name, entity.Description, entity.Icon, entity.SortOrder, entity.IsActive });
    }

    [HttpGet("items")]
    public async Task<IActionResult> Items(
        [FromQuery] long projectId,
        [FromQuery] long? moduleId,
        [FromQuery] bool standaloneOnly = false,
        CancellationToken ct = default)
    {
        if (projectId <= 0) return BadRequest(new { message = "Select a valid project." });

        var query = db.MenuItems.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (standaloneOnly)
            query = query.Where(x => x.ModuleId == null);
        else if (moduleId.HasValue)
        {
            if (!await db.ProjectModules.AnyAsync(x => x.Id == moduleId.Value && x.ProjectId == projectId, ct))
                return BadRequest(new { message = "Selected module does not belong to this project." });
            query = query.Where(x => x.ModuleId == moduleId.Value);
        }

        return Ok(await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.ProjectId, x.ModuleId, x.ParentMenuId, x.Code, x.Name, x.Area, x.Controller, x.Action, x.RouteUrl, x.Icon, x.SortOrder, x.IsExternal, x.IsActive })
            .ToListAsync(ct));
    }

    [HttpPost("items")]
    public async Task<IActionResult> SaveItem([FromBody] SaveMenuItemRequest request, CancellationToken ct)
    {
        if (request.ProjectId <= 0 || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Project, menu code and name are required." });
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (code.Length > 50 || name.Length > 200 || (request.Area?.Length ?? 0) > 100 ||
            (request.Controller?.Length ?? 0) > 100 || (request.Action?.Length ?? 0) > 100 ||
            (request.RouteUrl?.Length ?? 0) > 500 || (request.Icon?.Length ?? 0) > 100)
            return BadRequest(new { message = "One or more menu fields exceed the allowed length." });
        if (!await db.Projects.AnyAsync(x => x.Id == request.ProjectId, ct))
            return BadRequest(new { message = "Selected project does not exist." });
        if (request.ModuleId.HasValue && !await db.ProjectModules.AnyAsync(x => x.Id == request.ModuleId && x.ProjectId == request.ProjectId, ct))
            return BadRequest(new { message = "Selected module does not belong to this project." });
        if (request.ParentMenuId.HasValue && !await db.MenuItems.AnyAsync(x => x.Id == request.ParentMenuId && x.ProjectId == request.ProjectId && x.Id != request.Id, ct))
            return BadRequest(new { message = "Parent menu must belong to this project and cannot be itself." });
        if (request.ParentMenuId.HasValue && await IsDescendantAsync(request.ParentMenuId.Value, request.Id, request.ProjectId, ct))
            return BadRequest(new { message = "A menu cannot be placed beneath one of its descendants." });
        if (request.IsExternal)
        {
            if (!Uri.TryCreate(request.RouteUrl, UriKind.Absolute, out var externalUri) ||
                (externalUri.Scheme != Uri.UriSchemeHttp && externalUri.Scheme != Uri.UriSchemeHttps))
                return BadRequest(new { message = "External menus require an absolute HTTP or HTTPS URL." });
        }
        var duplicate = await db.MenuItems.AnyAsync(x => x.ProjectId == request.ProjectId && x.Code == code && x.Id != request.Id, ct);
        if (duplicate) return Conflict(new { message = "This menu code already exists in the project." });
        MenuItem entity;
        if (request.Id.HasValue)
        {
            var existing = await db.MenuItems.SingleOrDefaultAsync(x => x.Id == request.Id && x.ProjectId == request.ProjectId, ct);
            if (existing is null) return NotFound(new { message = "Menu not found for this project." });
            entity = existing;
        }
        else { entity = new MenuItem { ProjectId = request.ProjectId }; db.MenuItems.Add(entity); }
        entity.ModuleId = request.ModuleId; entity.ParentMenuId = request.ParentMenuId;
        entity.Code = code; entity.Name = name; entity.Area = request.Area?.Trim();
        entity.Controller = request.Controller?.Trim(); entity.Action = request.Action?.Trim();
        entity.RouteUrl = request.RouteUrl?.Trim(); entity.Icon = request.Icon?.Trim();

        // For new entries, calculate order on the server so stale browser data cannot
        // reset every module's first menu to 1. Existing entries retain their chosen order.
        if (!request.Id.HasValue)
        {
            var lastSortOrder = await db.MenuItems
                .Where(x => x.ProjectId == request.ProjectId && x.ModuleId == request.ModuleId)
                .Select(x => (int?)x.SortOrder)
                .MaxAsync(ct) ?? 0;
            entity.SortOrder = lastSortOrder + 1;
        }
        else
        {
            entity.SortOrder = request.SortOrder;
        }

        entity.IsExternal = request.IsExternal; entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(new { entity.Id, entity.ProjectId, entity.ModuleId, entity.ParentMenuId, entity.Code, entity.Name, entity.Area, entity.Controller, entity.Action, entity.RouteUrl, entity.Icon, entity.SortOrder, entity.IsExternal, entity.IsActive });
    }

    private async Task<bool> IsDescendantAsync(long parentId, long? currentId, long projectId, CancellationToken ct)
    {
        var visited = new HashSet<long>();
        var next = parentId;
        while (visited.Add(next))
        {
            if (currentId.HasValue && next == currentId.Value) return true;
            var parent = await db.MenuItems.AsNoTracking()
                .Where(x => x.Id == next && x.ProjectId == projectId)
                .Select(x => x.ParentMenuId).SingleOrDefaultAsync(ct);
            if (!parent.HasValue) return false;
            next = parent.Value;
        }
        return true;
    }
}

public sealed record SaveModuleRequest(long? Id, long ProjectId, string Code, string Name, string? Description, string? Icon, int SortOrder, bool IsActive);
public sealed record SaveMenuItemRequest(long? Id, long ProjectId, long? ModuleId, long? ParentMenuId, string Code, string Name, string? Area, string? Controller, string? Action, string? RouteUrl, string? Icon, int SortOrder, bool IsExternal, bool IsActive);
