using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Api.Services;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/navigation")]
[Authorize]
public sealed class NavigationController(EffectiveMenuAccessService menuAccess) : ControllerBase
{
    [HttpGet("menus")]
    public async Task<IActionResult> Menus([FromQuery] long projectId, CancellationToken ct)
    {
        if (projectId <= 0) return BadRequest(new { message = "A valid projectId is required." });
        if (!long.TryParse(User.FindFirst("ph_user_id")?.Value, out var userId) || userId <= 0)
            return Forbid();

        var menus = await menuAccess.GetForUserAsync(userId, projectId, ct);
        return Ok(menus);
    }

    [HttpGet("can-access")]
    public async Task<IActionResult> CanAccess([FromQuery] long projectId, [FromQuery] string menuCode, CancellationToken ct)
    {
        if (projectId <= 0 || string.IsNullOrWhiteSpace(menuCode))
            return BadRequest(new { message = "Project and menu code are required." });
        if (!long.TryParse(User.FindFirst("ph_user_id")?.Value, out var userId) || userId <= 0)
            return Forbid();

        return Ok(new { allowed = await menuAccess.HasAccessAsync(userId, projectId, menuCode, ct) });
    }
}
