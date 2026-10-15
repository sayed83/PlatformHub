using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/project-menus")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class ProjectMenusController(PlatformDbContext db) : ControllerBase
{
    [HttpGet("projects")]
    [Authorize(Policy = "Project.View")]
    public async Task<IActionResult> Projects(CancellationToken ct) =>
        Ok(await db.Projects.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.TenantId, TenantName = x.Tenant == null ? "Shared catalogue" : x.Tenant.Name, x.Code, x.Name })
            .ToListAsync(ct));


    [HttpGet("next-sort-order")]
    [Authorize(Policy = "Project.View")]
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
    [Authorize(Policy = "Project.View")]
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
    [Authorize(Policy = "Project.View")]
    public async Task<IActionResult> Modules([FromQuery] long projectId, CancellationToken ct)
    {
        if (projectId <= 0) return BadRequest(new { message = "Select a valid project." });
        return Ok(await db.ProjectModules.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.ProjectId, x.Code, x.Name, x.Description, x.Icon, x.SortOrder, x.IsActive })
            .ToListAsync(ct));
    }

    [HttpPost("modules")]
    [Authorize(Policy = "Project.Update")]
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
        await ProjectMenuPermissionSynchronizer.SyncProjectAsync(db, entity.ProjectId, ct);
        return Ok(new { entity.Id, entity.ProjectId, entity.Code, entity.Name, entity.Description, entity.Icon, entity.SortOrder, entity.IsActive });
    }

    [HttpGet("items")]
    [Authorize(Policy = "Project.View")]
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
    [Authorize(Policy = "Project.Update")]
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
        await ProjectMenuPermissionSynchronizer.SyncAsync(db, entity.ProjectId, entity.Id, ct);
        return Ok(new { entity.Id, entity.ProjectId, entity.ModuleId, entity.ParentMenuId, entity.Code, entity.Name, entity.Area, entity.Controller, entity.Action, entity.RouteUrl, entity.Icon, entity.SortOrder, entity.IsExternal, entity.IsActive });
    }

    [HttpGet("modules/{moduleId:long}/hard-delete-details")]
    [Authorize(Policy = "Project.Update")]
    public async Task<IActionResult> ModuleHardDeleteDetails(long moduleId, [FromQuery] long projectId, CancellationToken ct)
    {
        if (!User.HasClaim("ph_role", PlatformRbacSeeder.SuperAdminCode)) return Forbid();
        var module = await db.ProjectModules.AsNoTracking()
            .Where(x => x.Id == moduleId && x.ProjectId == projectId)
            .Select(x => new { x.Id, x.ProjectId, x.Code, x.Name, x.IsActive })
            .SingleOrDefaultAsync(ct);
        if (module is null) return NotFound(new { message = "Module was not found in the selected project." });
        var menus = await db.MenuItems.AsNoTracking().Where(x => x.ProjectId == projectId && x.ModuleId == moduleId)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.IsActive,
                RoleAccessCount = db.RoleWiseMenuAccesses.Count(a => a.MenuItemId == x.Id),
                UserAccessCount = db.UserWiseMenuAccesses.Count(a => a.MenuItemId == x.Id),
                ChildMenuCount = db.MenuItems.Count(m => m.ParentMenuId == x.Id && m.ProjectId == projectId) }).ToListAsync(ct);
        return Ok(new { itemType = "module", module, relatedMenus = menus,
            canHardDelete = menus.Count == 0,
            reason = menus.Count == 0 ? null : "Delete or reassign all menu entries before permanently deleting this module." });
    }

    [HttpGet("items/{menuItemId:long}/hard-delete-details")]
    [Authorize(Policy = "Project.Update")]
    public async Task<IActionResult> MenuHardDeleteDetails(long menuItemId, [FromQuery] long projectId, CancellationToken ct)
    {
        if (!User.HasClaim("ph_role", PlatformRbacSeeder.SuperAdminCode)) return Forbid();
        var menu = await db.MenuItems.AsNoTracking().Where(x => x.Id == menuItemId && x.ProjectId == projectId)
            .Select(x => new { x.Id, x.ProjectId, x.ModuleId, ModuleName = x.Module == null ? "Standalone" : x.Module.Name,
                x.ParentMenuId, ParentMenuName = x.ParentMenu == null ? null : x.ParentMenu.Name,
                x.Code, x.Name, x.RouteUrl, x.IsActive })
            .SingleOrDefaultAsync(ct);
        if (menu is null) return NotFound(new { message = "Menu was not found in the selected project." });
        var children = await db.MenuItems.AsNoTracking().Where(x => x.ProjectId == projectId && x.ParentMenuId == menuItemId)
            .Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).ToListAsync(ct);
        var roleAccess = await db.RoleWiseMenuAccesses.AsNoTracking().Where(x => x.MenuItemId == menuItemId)
            .Select(x => new { x.Id, RoleName = x.Role.Name, x.IsAllowed, x.ProjectId }).ToListAsync(ct);
        var userAccess = await db.UserWiseMenuAccesses.AsNoTracking().Where(x => x.MenuItemId == menuItemId)
            .Select(x => new { x.Id, UserName = x.User.UserName, x.IsAllowed, x.ProjectId }).ToListAsync(ct);
        var seeded = new[] { "WORKSPACE_OVERVIEW", "TENANT_LIST", "BRANCH_LIST", "PROJECT_LIST", "USER_LIST",
            "ROLE_LIST", "PERMISSION_CATALOG", "MENU_PERMISSION", "MENU_PERMISSIONS", "WORKSPACE_MENU_PERMISSIONS", "INVENTORY_ITEMS" }
            .Contains(menu.Code, StringComparer.OrdinalIgnoreCase);
        var canHardDelete = !seeded && children.Count == 0 && roleAccess.Count == 0 && userAccess.Count == 0;
        return Ok(new { itemType = "menu", menu, childMenus = children, roleAccess, userAccess, isSeededSystemMenu = seeded,
            canHardDelete, reason = seeded ? "This is a seeded system menu; use soft delete." :
                children.Count > 0 ? "Reassign or delete child menus first." :
                roleAccess.Count > 0 || userAccess.Count > 0 ? "This menu has role/user access history. Use soft delete to preserve it." : null });
    }

    [HttpPost("modules/{moduleId:long}/delete")]
    [Authorize(Policy = "Project.Deactivate")]
    public async Task<IActionResult> DeleteModule(long moduleId, [FromBody] DeleteModuleRequest request, CancellationToken ct)
    {
        if (request.DeleteType == "hard" && !User.HasClaim("ph_role", PlatformRbacSeeder.SuperAdminCode)) return Forbid();
        if (moduleId <= 0 || request.ProjectId <= 0 || request.DeleteType is not ("soft" or "hard"))
            return BadRequest(new { message = "A valid project, module and delete type are required." });

        var module = await db.ProjectModules.SingleOrDefaultAsync(x => x.Id == moduleId && x.ProjectId == request.ProjectId, ct);
        if (module is null) return NotFound(new { message = "Module was not found in the selected project." });

        if (request.DeleteType == "soft")
        {
            module.IsActive = false;
            var menuItems = await db.MenuItems.Where(x => x.ProjectId == request.ProjectId && x.ModuleId == moduleId).ToListAsync(ct);
            foreach (var item in menuItems) item.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "Module and its menu entries were deactivated. Records and access history were retained." });
        }

        var hasMenus = await db.MenuItems.AnyAsync(x => x.ProjectId == request.ProjectId && x.ModuleId == moduleId, ct);
        if (hasMenus)
            return Conflict(new { message = "Hard delete is blocked because this module still contains menu entries. Remove or hard-delete the menu entries first, then retry." });

        db.ProjectModules.Remove(module);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Module permanently deleted." });
    }

    [HttpPost("items/{menuItemId:long}/delete")]
    [Authorize(Policy = "Project.Deactivate")]
    public async Task<IActionResult> DeleteItem(long menuItemId, [FromBody] DeleteMenuItemRequest request, CancellationToken ct)
    {
        if (request.DeleteType == "hard" && !User.HasClaim("ph_role", PlatformRbacSeeder.SuperAdminCode)) return Forbid();
        if (menuItemId <= 0 || request.ProjectId <= 0 || request.DeleteType is not ("soft" or "hard"))
            return BadRequest(new { message = "A valid project, menu and delete type are required." });

        var menu = await db.MenuItems.SingleOrDefaultAsync(x => x.Id == menuItemId && x.ProjectId == request.ProjectId, ct);
        if (menu is null) return NotFound(new { message = "Menu was not found in the selected project." });

        if (request.DeleteType == "soft")
        {
            menu.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "Menu deactivated. The record and permission history were retained." });
        }

        var seededCodes = new[] { "WORKSPACE_OVERVIEW", "TENANT_LIST", "BRANCH_LIST", "PROJECT_LIST",
            "USER_LIST", "ROLE_LIST", "PERMISSION_CATALOG", "WORKSPACE_MENU_PERMISSIONS", "INVENTORY_ITEMS" };
        if (seededCodes.Contains(menu.Code, StringComparer.OrdinalIgnoreCase))
            return Conflict(new { message = "This is a system menu. Use Soft delete to deactivate it without removing the catalog record." });

        var hasChildren = await db.MenuItems.AnyAsync(x => x.ProjectId == request.ProjectId && x.ParentMenuId == menu.Id, ct);
        var hasRoleAccess = await db.RoleWiseMenuAccesses.AnyAsync(x => x.MenuItemId == menu.Id, ct);
        var hasUserAccess = await db.UserWiseMenuAccesses.AnyAsync(x => x.MenuItemId == menu.Id, ct);
        if (hasChildren || hasRoleAccess || hasUserAccess)
            return Conflict(new { message = "Hard delete is blocked because this menu has child menus or role/user access records. Use Soft delete to preserve related data." });

        db.MenuItems.Remove(menu);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Menu permanently deleted." });
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

public sealed record DeleteMenuItemRequest(long ProjectId, string DeleteType);

public sealed record DeleteModuleRequest(long ProjectId, string DeleteType);
