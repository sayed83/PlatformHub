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
    public IActionResult Summary() => Ok(new { service = "PlatformHub", status = "ok" });

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken) =>
        Ok(await platform.GetTenantsAsync(cancellationToken));

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirst(AppClaimTypes.UserId)?.Value;
        if (!long.TryParse(userIdRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Forbid();

        // Do not trust a token claim alone for the platform super-admin invariant.
        if (!await platform.IsPlatformAdminAsync(userId, cancellationToken))
            return Forbid();

        try
        {
            var created = await platform.CreateTenantAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetTenants), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
