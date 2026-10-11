using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Services;

public sealed record EffectiveMenuDto(
    long Id, string Code, string Name, long? ModuleId, string? ModuleName,
    long? ParentMenuId, string? Area, string? Controller, string? Action,
    string? RouteUrl, string? Icon, int SortOrder, bool IsExternal);

public sealed class EffectiveMenuAccessService(PlatformDbContext db)
{
    public async Task<IReadOnlyList<EffectiveMenuDto>> GetForUserAsync(
        long userId, long projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId && p.IsActive)
            .Select(p => new { p.Id, p.TenantId })
            .SingleOrDefaultAsync(ct);
        if (project is null) return [];

        // Project and tenant assignments apply to this project's menu set.
        // Branch-scoped assignments are included through the branch's tenant.
        var roleIds = await db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId &&
                (a.ProjectId == projectId ||
                 (a.ProjectId == null && a.BranchId == null && a.TenantId == project.TenantId) ||
                 (a.ProjectId == null && a.BranchId != null &&
                    db.Branches.Any(b => b.Id == a.BranchId && b.TenantId == project.TenantId))))
            .Select(a => a.RoleId)
            .Distinct()
            .ToListAsync(ct);

        if (roleIds.Count == 0) return [];

        var menus = await db.MenuItems.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.IsActive)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Name)
            .Select(m => new EffectiveMenuDto(m.Id, m.Code, m.Name, m.ModuleId,
                m.Module == null ? null : m.Module.Name, m.ParentMenuId, m.Area,
                m.Controller, m.Action, m.RouteUrl, m.Icon, m.SortOrder, m.IsExternal))
            .ToListAsync(ct);

        var menuIds = menus.Select(m => m.Id).ToArray();
        var roleDecisions = await db.RoleWiseMenuAccesses.AsNoTracking()
            .Where(a => a.ProjectId == projectId && roleIds.Contains(a.RoleId) && menuIds.Contains(a.MenuItemId) && a.IsAllowed)
            .Select(a => a.MenuItemId).Distinct().ToListAsync(ct);
        var userOverrides = await db.UserWiseMenuAccesses.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.UserId == userId && menuIds.Contains(a.MenuItemId))
            .Select(a => new { a.MenuItemId, a.IsAllowed }).ToDictionaryAsync(a => a.MenuItemId, a => a.IsAllowed, ct);

        // Explicit user decisions override the combined role result.
        var allowed = menus.Where(m => userOverrides.TryGetValue(m.Id, out var userAllowed)
                ? userAllowed
                : roleDecisions.Contains(m.Id))
            .Select(m => m.Id).ToHashSet();

        // Keep ancestor nodes visible only when they contain an allowed menu.
        var byId = menus.ToDictionary(m => m.Id);
        foreach (var menuId in allowed.ToArray())
        {
            var parentId = byId[menuId].ParentMenuId;
            var visited = new HashSet<long>();
            while (parentId.HasValue && visited.Add(parentId.Value) && byId.TryGetValue(parentId.Value, out var parent))
            {
                allowed.Add(parent.Id);
                parentId = parent.ParentMenuId;
            }
        }

        return menus.Where(m => allowed.Contains(m.Id)).ToArray();
    }

    public async Task<bool> HasAccessAsync(long userId, long projectId, string menuCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(menuCode)) return false;
        var menus = await GetForUserAsync(userId, projectId, ct);
        return menus.Any(m => string.Equals(m.Code, menuCode, StringComparison.OrdinalIgnoreCase));
    }
}
