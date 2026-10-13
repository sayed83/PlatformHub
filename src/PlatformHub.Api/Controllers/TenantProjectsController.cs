using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/platform/tenants/{tenantId:long}/projects")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class TenantProjectsController(PlatformDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Tenant.View")]
    public async Task<IActionResult> Get(long tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => new { x.Id, x.Code, x.Name, x.IsActive })
            .SingleOrDefaultAsync(ct);
        if (tenant is null) return NotFound(new { message = "Tenant was not found." });

        var assignedIds = await db.TenantProjects.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsEnabled)
            .Select(x => x.ProjectId).ToListAsync(ct);
        var projects = await db.Projects.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id, x.Code, x.Name, x.BaseUrl, x.IsActive,
                IsAssigned = assignedIds.Contains(x.Id)
            }).ToListAsync(ct);

        return Ok(new { tenant, projects, assignedProjectIds = assignedIds });
    }

    [HttpPut]
    [Authorize(Policy = "Project.Update")]
    public async Task<IActionResult> Assign(long tenantId, [FromBody] AssignTenantProjectsRequest request, CancellationToken ct)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, ct);
        if (tenant is null) return NotFound(new { message = "Tenant was not found." });
        if (!tenant.IsActive) return Conflict(new { message = "Projects cannot be assigned to an inactive tenant." });

        var projectIds = request.ProjectIds ?? new List<long>();
        if (projectIds.Any(id => id <= 0) || projectIds.Distinct().Count() != projectIds.Count)
            return BadRequest(new { message = "Project IDs must be valid and unique." });

        var validIds = await db.Projects.AsNoTracking()
            .Where(x => projectIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id).ToListAsync(ct);
        if (validIds.Count != projectIds.Count)
            return BadRequest(new { message = "One or more selected projects do not exist or are inactive." });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.TenantProjects.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        db.TenantProjects.RemoveRange(current);

        var now = DateTime.UtcNow;
        db.TenantProjects.AddRange(projectIds.Select(projectId => new TenantProject
        {
            TenantId = tenantId,
            ProjectId = projectId,
            IsEnabled = true,
            AssignedUtc = now
        }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new { message = "Tenant project assignments saved.", tenantId, assignedCount = projectIds.Count });
    }
}

public sealed record AssignTenantProjectsRequest(List<long>? ProjectIds);
