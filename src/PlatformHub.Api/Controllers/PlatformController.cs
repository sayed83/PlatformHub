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
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        Ok(await platform.GetSummaryAsync(cancellationToken));

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken) =>
        Ok(await platform.GetTenantsAsync(cancellationToken));

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var userId = await GetVerifiedPlatformAdminIdAsync(cancellationToken);
        if (userId is null) return Forbid();

        try
        {
            var created = await platform.CreateTenantAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetTenants), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects([FromQuery] long? tenantId, CancellationToken cancellationToken)
    {
        if (await GetVerifiedPlatformAdminIdAsync(cancellationToken) is null) return Forbid();
        return Ok(await platform.GetProjectsAsync(tenantId, cancellationToken));
    }

    [HttpPost("projects")]
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

    private async Task<long?> GetVerifiedPlatformAdminIdAsync(CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirst(AppClaimTypes.UserId)?.Value;
        if (!long.TryParse(userIdRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return null;

        return await platform.IsPlatformAdminAsync(userId, cancellationToken) ? userId : null;
    }
}
