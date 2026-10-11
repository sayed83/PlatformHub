using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;

namespace PlatformHub.Api.Controllers;

/// <summary>
/// Example tenant-scoped workspace endpoint. Business endpoints should call
/// IScopedAccessService with their own permission and resource scope before returning data.
/// </summary>
[ApiController]
[Route("api/v1/workspace")]
[Authorize]
public sealed class WorkspaceController(
    PlatformDbContext db,
    IScopedAccessService scopedAccess) : ControllerBase
{
    [HttpGet("context")]
    public async Task<IActionResult> GetContext(
        [FromQuery] long tenantId,
        [FromQuery] long? branchId,
        [FromQuery] long? projectId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirst(AppClaimTypes.UserId)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            return Unauthorized();

        if (tenantId <= 0)
            return BadRequest(new { message = "A valid tenantId is required." });

        var allowed = await scopedAccess.CanAccessAsync(
            userId, "Tenant.View", tenantId, branchId, projectId, cancellationToken);
        if (!allowed)
            return Forbid();

        var tenant = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId && x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (tenant is null) return NotFound(new { message = "Active tenant not found." });

        object? branch = null;
        if (branchId.HasValue)
        {
            branch = await db.Branches.AsNoTracking()
                .Where(x => x.Id == branchId.Value && x.TenantId == tenantId && x.IsActive)
                .Select(x => new { x.Id, x.Code, x.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (branch is null) return NotFound(new { message = "Active branch not found in this tenant." });
        }

        object? project = null;
        if (projectId.HasValue)
        {
            project = await db.Projects.AsNoTracking()
                .Where(x => x.Id == projectId.Value && x.TenantId == tenantId && x.IsActive)
                .Select(x => new { x.Id, x.Code, x.Name })
                .SingleOrDefaultAsync(cancellationToken);
            if (project is null) return NotFound(new { message = "Active project not found in this tenant." });
        }

        return Ok(new
        {
            tenant,
            branch,
            project,
            message = "Scope validated. Apply IScopedAccessService to each tenant-facing API operation."
        });
    }
}
