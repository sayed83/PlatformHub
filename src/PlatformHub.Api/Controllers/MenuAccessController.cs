using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/menu-access")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class MenuAccessController(PlatformDbContext db) : ControllerBase
{
    [HttpGet("projects")]
    public async Task<IActionResult> Projects(CancellationToken ct) =>
        Ok(await db.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, TenantName = x.Tenant.Name }).ToListAsync(ct));

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups([FromQuery] long projectId, CancellationToken ct)
    {
        if (projectId <= 0) return BadRequest(new { message = "Select a project." });
        var projectExists = await db.Projects.AnyAsync(x => x.Id == projectId, ct);
        if (!projectExists) return NotFound(new { message = "Project not found." });
        var roles = await db.Roles.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive &&
            db.UserRoleAssignments.Any(a => a.UserId == u.Id && (a.ProjectId == projectId ||
                (a.ProjectId == null && a.TenantId == db.Projects.Where(p => p.Id == projectId).Select(p => (long?)p.TenantId).FirstOrDefault()))))
            .OrderBy(x => x.DisplayName).Select(x => new { x.Id, x.UserName, x.DisplayName }).ToListAsync(ct);
        var menus = await db.MenuItems.AsNoTracking().Where(x => x.ProjectId == projectId && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.ModuleId, ModuleName = x.Module == null ? null : x.Module.Name, x.ParentMenuId, x.SortOrder })
            .ToListAsync(ct);
        return Ok(new { roles, users, menus });
    }

    [HttpGet("role")]
    public async Task<IActionResult> GetRoleAccess([FromQuery] long projectId, [FromQuery] long roleId, CancellationToken ct)
    {
        if (projectId <= 0 || roleId <= 0) return BadRequest(new { message = "Project and role are required." });
        if (!await db.Roles.AnyAsync(x => x.Id == roleId, ct)) return NotFound(new { message = "Role not found." });
        var menuIds = await db.MenuItems.AsNoTracking().Where(x => x.ProjectId == projectId && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        var allowed = await db.RoleWiseMenuAccesses.AsNoTracking().Where(x => x.ProjectId == projectId && x.RoleId == roleId && menuIds.Contains(x.MenuItemId))
            .Select(x => new { x.MenuItemId, x.IsAllowed }).ToDictionaryAsync(x => x.MenuItemId, x => x.IsAllowed, ct);
        return Ok(menuIds.Select(id => new { menuItemId = id, isAllowed = allowed.GetValueOrDefault(id) }));
    }

    [HttpPost("role")]
    public async Task<IActionResult> SaveRoleAccess([FromBody] SaveRoleMenuAccessRequest request, CancellationToken ct)
    {
        if (request.ProjectId <= 0 || request.RoleId <= 0) return BadRequest(new { message = "Project and role are required." });
        if (!await db.Projects.AnyAsync(x => x.Id == request.ProjectId, ct) || !await db.Roles.AnyAsync(x => x.Id == request.RoleId, ct))
            return BadRequest(new { message = "Project or role does not exist." });
        var menuIds = await db.MenuItems.Where(x => x.ProjectId == request.ProjectId && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        if (request.Items.Select(x => x.MenuItemId).Distinct().Count() != request.Items.Count ||
            request.Items.Any(x => !menuIds.Contains(x.MenuItemId)) || request.Items.Count != menuIds.Count)
            return BadRequest(new { message = "Submit exactly one access decision for each active menu in this project." });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.RoleWiseMenuAccesses.Where(x => x.ProjectId == request.ProjectId && x.RoleId == request.RoleId).ToListAsync(ct);
        db.RoleWiseMenuAccesses.RemoveRange(existing);
        db.RoleWiseMenuAccesses.AddRange(request.Items.Select(x => new RoleWiseMenuAccess
        {
            ProjectId = request.ProjectId, RoleId = request.RoleId, MenuItemId = x.MenuItemId, IsAllowed = x.IsAllowed, UpdatedUtc = DateTime.UtcNow
        }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { message = "Role menu access saved.", count = request.Items.Count });
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUserAccess([FromQuery] long projectId, [FromQuery] long userId, CancellationToken ct)
    {
        if (projectId <= 0 || userId <= 0) return BadRequest(new { message = "Project and user are required." });
        var menuIds = await db.MenuItems.AsNoTracking().Where(x => x.ProjectId == projectId && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        var values = await db.UserWiseMenuAccesses.AsNoTracking().Where(x => x.ProjectId == projectId && x.UserId == userId && menuIds.Contains(x.MenuItemId))
            .Select(x => new { x.MenuItemId, x.IsAllowed }).ToDictionaryAsync(x => x.MenuItemId, x => x.IsAllowed, ct);
        return Ok(menuIds.Select(id => new { menuItemId = id, decision = values.ContainsKey(id) ? (values[id] ? "allow" : "deny") : "inherit" }));
    }

    [HttpPost("user")]
    public async Task<IActionResult> SaveUserAccess([FromBody] SaveUserMenuAccessRequest request, CancellationToken ct)
    {
        if (request.ProjectId <= 0 || request.UserId <= 0) return BadRequest(new { message = "Project and user are required." });
        if (!await db.Projects.AnyAsync(x => x.Id == request.ProjectId, ct) || !await db.Users.AnyAsync(x => x.Id == request.UserId, ct))
            return BadRequest(new { message = "Project or user does not exist." });
        var menuIds = await db.MenuItems.Where(x => x.ProjectId == request.ProjectId && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        if (request.Items.Select(x => x.MenuItemId).Distinct().Count() != request.Items.Count ||
            request.Items.Any(x => !menuIds.Contains(x.MenuItemId)) || request.Items.Count != menuIds.Count ||
            request.Items.Any(x => x.Decision is not ("inherit" or "allow" or "deny")))
            return BadRequest(new { message = "Submit exactly one valid decision for each active menu in this project." });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.UserWiseMenuAccesses.Where(x => x.ProjectId == request.ProjectId && x.UserId == request.UserId).ToListAsync(ct);
        db.UserWiseMenuAccesses.RemoveRange(existing);
        db.UserWiseMenuAccesses.AddRange(request.Items.Where(x => x.Decision != "inherit").Select(x => new UserWiseMenuAccess
        {
            ProjectId = request.ProjectId, UserId = request.UserId, MenuItemId = x.MenuItemId,
            IsAllowed = x.Decision == "allow", UpdatedUtc = DateTime.UtcNow
        }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { message = "User menu overrides saved. Inherit uses the user's role access.", count = request.Items.Count });
    }
}

public sealed record RoleMenuDecision(long MenuItemId, bool IsAllowed);
public sealed record SaveRoleMenuAccessRequest(long ProjectId, long RoleId, List<RoleMenuDecision> Items);
public sealed record UserMenuDecision(long MenuItemId, string Decision);
public sealed record SaveUserMenuAccessRequest(long ProjectId, long UserId, List<UserMenuDecision> Items);
