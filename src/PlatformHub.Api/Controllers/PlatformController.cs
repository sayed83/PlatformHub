using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Application.Auth;
using PlatformHub.Application.Platform;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/platform")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformController(IPlatformService platform) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Policy = "Platform.Dashboard.View")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        Ok(await platform.GetSummaryAsync(cancellationToken));

    [HttpGet("tenants")]
    [Authorize(Policy = "Tenant.View")]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken) =>
        Ok(await platform.GetTenantsAsync(cancellationToken));

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

    private async Task<long?> GetVerifiedPlatformAdminIdAsync(CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirst(AppClaimTypes.UserId)?.Value;
        if (!long.TryParse(userIdRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return null;
        return await platform.IsPlatformAdminAsync(userId, cancellationToken) ? userId : null;
    }
}
