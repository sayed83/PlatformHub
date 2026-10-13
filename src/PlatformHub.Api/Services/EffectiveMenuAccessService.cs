using Microsoft.EntityFrameworkCore;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Services;

public sealed record EffectiveMenuDto(
    long Id, string Code, string Name, long? ModuleId, string? ModuleName,
    long? ParentMenuId, string? Area, string? Controller, string? Action,
    string? RouteUrl, string? Icon, int SortOrder, bool IsExternal);

public sealed class EffectiveMenuAccessService(PlatformDbContext db)
{
    public async Task<IReadOnlyList<EffectiveMenuDto>> GetForUserAsync(
        long userId, long projectId, long tenantId, CancellationToken ct = default)
    {
        var roleIds = await GetApplicableRoleIdsAsync(userId, projectId, tenantId, ct);
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

        // Ancestors are included for navigation display only. HasAccessAsync below
        // deliberately checks the direct menu decision, not this expanded list.
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

    public async Task<bool> HasAccessAsync(long userId, long projectId, long tenantId, string menuCode, CancellationToken ct = default)
    {
        if (userId <= 0 || projectId <= 0 || tenantId <= 0 || string.IsNullOrWhiteSpace(menuCode))
            return false;

        var roleIds = await GetApplicableRoleIdsAsync(userId, projectId, tenantId, ct);
        if (roleIds.Count == 0)
            return false;

        var menu = await db.MenuItems.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.IsActive && m.Code == menuCode)
            .Select(m => new { m.Id })
            .SingleOrDefaultAsync(ct);
        if (menu is null)
            return false;

        var userOverride = await db.UserWiseMenuAccesses.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.UserId == userId && a.MenuItemId == menu.Id)
            .Select(a => (bool?)a.IsAllowed)
            .SingleOrDefaultAsync(ct);
        if (userOverride.HasValue)
            return userOverride.Value;

        return await db.RoleWiseMenuAccesses.AsNoTracking().AnyAsync(
            a => a.ProjectId == projectId && roleIds.Contains(a.RoleId) &&
                 a.MenuItemId == menu.Id && a.IsAllowed, ct);
    }

    private async Task<List<long>> GetApplicableRoleIdsAsync(
        long userId, long projectId, long tenantId, CancellationToken ct)
    {
        if (userId <= 0 || projectId <= 0 || tenantId <= 0)
            return [];

        var activeUser = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, ct);
        if (!activeUser)
            return [];

        var tenantActive = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.IsActive, ct);
        if (!tenantActive)
            return [];

        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId && p.IsActive)
            .Select(p => new { p.Id, p.TenantId })
            .SingleOrDefaultAsync(ct);
        if (project is null)
            return [];

        // Reusable projects are available only when the requested tenant has an active
        // subscription. Tenant-owned projects retain legacy compatibility.
        var tenantHasProject = project.TenantId == tenantId ||
            await db.TenantProjects.AsNoTracking().AnyAsync(tp =>
                tp.TenantId == tenantId && tp.ProjectId == projectId &&
                tp.IsEnabled && tp.Tenant.IsActive, ct);
        if (!tenantHasProject)
            return [];

        return await db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Role.IsActive &&
                (
                    (a.TenantId == tenantId &&
                        (a.ProjectId == null || a.ProjectId == projectId) &&
                        (a.BranchId == null || db.Branches.Any(b =>
                            b.Id == a.BranchId && b.TenantId == tenantId && b.IsActive)))
                    ||
                    (a.TenantId == null && a.ProjectId == null && a.BranchId != null &&
                        db.Branches.Any(b => b.Id == a.BranchId &&
                            b.TenantId == tenantId && b.IsActive))
                ))
            .Select(a => a.RoleId)
            .Distinct()
            .ToListAsync(ct);
    }
}
