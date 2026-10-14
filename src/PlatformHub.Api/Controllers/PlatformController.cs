using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Application.Auth;
using PlatformHub.Application.Platform;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/platform")]
[Authorize]
public sealed class PlatformController(IPlatformService platform, PlatformDbContext db, IScopedAccessService scopedAccess) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Policy = "Platform.Dashboard.View")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        Ok(await platform.GetSummaryAsync(cancellationToken));

    [HttpGet("tenants")]
    [Authorize(Policy = "Tenant.View")]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Forbid();
        if (await platform.IsPlatformAdminAsync(userId.Value, cancellationToken))
            return Ok(await platform.GetTenantsAsync(cancellationToken));

        var candidateTenantIds = await db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId.Value && a.Role.IsActive)
            .Select(a => a.TenantId ?? (a.BranchId.HasValue
                ? db.Branches.Where(b => b.Id == a.BranchId.Value).Select(b => (long?)b.TenantId).FirstOrDefault()
                : null))
            .Where(id => id.HasValue)
            .Select(id => id!.Value).Distinct().ToListAsync(cancellationToken);

        var allTenants = await platform.GetTenantsAsync(cancellationToken);
        var visibleIds = new HashSet<long>();
        foreach (var tenantId in candidateTenantIds)
            if (await scopedAccess.CanAccessAsync(userId.Value, "Tenant.View", tenantId, cancellationToken: cancellationToken))
                visibleIds.Add(tenantId);
        return Ok(allTenants.Where(t => visibleIds.Contains(t.Id)));
    }

    [HttpPost("tenants")]
    [Authorize(Policy = "Tenant.Create")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try
        {
            var created = await platform.CreateTenantAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetTenants), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }


    [HttpPost("tenants/{id:long}/update")]
    [Authorize(Policy = "Tenant.Update")]
    public async Task<IActionResult> UpdateTenant(long id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageTenantAsync(id, "Tenant.Update", cancellationToken)) return Forbid();
        try { return Ok(await platform.UpdateTenantAsync(id, request.Code, request.Name, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("tenants/{id:long}/delete")]
    [Authorize(Policy = "Tenant.Deactivate")]
    public async Task<IActionResult> DeleteTenant(long id, [FromBody] DeleteRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageTenantAsync(id, "Tenant.Deactivate", cancellationToken)) return Forbid();
        if (request.HardDelete && !await IsPlatformAdminAsync(cancellationToken)) return Forbid();
        try { await platform.DeleteTenantAsync(id, request.HardDelete, cancellationToken); return Ok(new { success = true }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("tenants/{id:long}/activate")]
    [Authorize(Policy = "Tenant.Update")]
    public async Task<IActionResult> ActivateTenant(long id, CancellationToken cancellationToken)
    {
        if (!await CanManageTenantAsync(id, "Tenant.Update", cancellationToken)) return Forbid();
        try { await platform.ActivateTenantAsync(id, cancellationToken); return Ok(new { success = true }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("projects")]
    [Authorize(Policy = "Project.View")]
    public async Task<IActionResult> GetProjects([FromQuery] long? tenantId, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        return Ok(await platform.GetProjectsAsync(tenantId, cancellationToken));
    }

    [HttpPost("projects")]
    [Authorize(Policy = "Project.Create")]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try
        {
            var created = await platform.CreateProjectAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetProjects), new { tenantId = created.TenantId }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("projects/{id:long}/update")]
    [Authorize(Policy = "Project.Update")]
    public async Task<IActionResult> UpdateProject(long id, [FromBody] ProjectUpdateRequest request, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try { return Ok(await platform.UpdateProjectAsync(id, request.TenantId, request.Code, request.Name, request.BaseUrl, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("projects/{id:long}/delete")]
    [Authorize(Policy = "Project.Deactivate")]
    public async Task<IActionResult> DeleteProject(long id, [FromBody] DeleteRequest request, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try { await platform.DeleteProjectAsync(id, request.HardDelete, cancellationToken); return Ok(new { success = true }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("projects/{id:long}/activate")]
    [Authorize(Policy = "Project.Update")]
    public async Task<IActionResult> ActivateProject(long id, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try { await platform.ActivateProjectAsync(id, cancellationToken); return Ok(new { success = true }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("branches")]
    [Authorize(Policy = "Branch.View")]
    public async Task<IActionResult> GetBranches([FromQuery] long tenantId, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try { return Ok(await platform.GetBranchesAsync(tenantId, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("branches")]
    [Authorize(Policy = "Branch.Create")]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try
        {
            var created = await platform.CreateBranchAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetBranches), new { tenantId = created.TenantId }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }


    [HttpPost("branches/{id:long}/update")]
    [Authorize(Policy = "Branch.Update")]
    public async Task<IActionResult> UpdateBranch(long id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try { return Ok(await platform.UpdateBranchAsync(id, request.Code, request.Name, request.Address, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("branches/{id:long}/delete")]
    [Authorize(Policy = "Branch.Deactivate")]
    public async Task<IActionResult> DeleteBranch(long id, [FromBody] DeleteRequest request, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try { await platform.DeleteBranchAsync(id, request.HardDelete, cancellationToken); return Ok(new { success = true }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("branches/{id:long}/activate")]
    [Authorize(Policy = "Branch.Update")]
    public async Task<IActionResult> ActivateBranch(long id, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        try { await platform.ActivateBranchAsync(id, cancellationToken); return Ok(new { success = true }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    public sealed record UpdateTenantRequest(string Code, string Name);
    public sealed record UpdateBranchRequest(string Code, string Name, string? Address);
    public sealed record DeleteRequest(bool HardDelete);

    private long? GetUserId()
    {
        var raw = User.FindFirst(AppClaimTypes.UserId)?.Value;
        return long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private async Task<bool> IsPlatformAdminAsync(CancellationToken ct)
    {
        var id = GetUserId();
        return id.HasValue && await platform.IsPlatformAdminAsync(id.Value, ct);
    }

    private async Task<bool> CanManageTenantAsync(long tenantId, string permission, CancellationToken ct)
    {
        if (await IsPlatformAdminAsync(ct)) return true;
        var id = GetUserId();
        return id.HasValue && await scopedAccess.CanAccessAsync(id.Value, permission, tenantId, cancellationToken: ct);
    }

    private async Task<long?> GetVerifiedPlatformAdminIdAsync(CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirst(AppClaimTypes.UserId)?.Value;
        if (!long.TryParse(userIdRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return null;
        return await platform.IsPlatformAdminAsync(userId, cancellationToken) ? userId : null;
    }
}
