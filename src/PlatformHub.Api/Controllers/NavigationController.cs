using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Api.Services;
using PlatformHub.Api.Platform;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/navigation")]
[Authorize]
public sealed class NavigationController(EffectiveMenuAccessService menuAccess, IActiveTenantContext activeTenant) : ControllerBase
{
    [HttpGet("menus")]
    public async Task<IActionResult> Menus([FromQuery] long projectId, CancellationToken ct)
    {
        if (projectId <= 0) return BadRequest(new { message = "A valid projectId is required." });
        if (!long.TryParse(User.FindFirst("ph_user_id")?.Value, out var userId) || userId <= 0)
            return Forbid();
        if (!activeTenant.IsResolved || activeTenant.TenantId is not > 0 ||
            (activeTenant.ProjectId.HasValue && activeTenant.ProjectId.Value != projectId))
            return Forbid();

        var menus = await menuAccess.GetForUserAsync(userId, projectId, activeTenant.TenantId.Value, ct);
        return Ok(menus);
    }

    [HttpGet("can-access")]
    public async Task<IActionResult> CanAccess([FromQuery] long projectId, [FromQuery] string menuCode, CancellationToken ct)
    {
        if (projectId <= 0 || string.IsNullOrWhiteSpace(menuCode))
            return BadRequest(new { message = "Project and menu code are required." });
        if (!long.TryParse(User.FindFirst("ph_user_id")?.Value, out var userId) || userId <= 0)
            return Forbid();
        if (!activeTenant.IsResolved || activeTenant.TenantId is not > 0 ||
            (activeTenant.ProjectId.HasValue && activeTenant.ProjectId.Value != projectId))
            return Forbid();

        return Ok(new { allowed = await menuAccess.HasAccessAsync(userId, projectId, activeTenant.TenantId.Value, menuCode, ct) });
    }
}
