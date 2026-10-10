using Microsoft.EntityFrameworkCore;
using PlatformHub.Application.Auth;
using PlatformHub.Application.Platform;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

public sealed class PlatformService(PlatformDbContext db) : IPlatformService
{
    public async Task<PlatformSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var totalUsers = await db.Users.CountAsync(cancellationToken);
        var activeUsers = await db.Users.CountAsync(x => x.IsActive, cancellationToken);
        var totalTenants = await db.Tenants.CountAsync(cancellationToken);
        var activeTenants = await db.Tenants.CountAsync(x => x.IsActive, cancellationToken);
        var totalProjects = await db.Projects.CountAsync(cancellationToken);
        var activeProjects = await db.Projects.CountAsync(x => x.IsActive, cancellationToken);

        return new PlatformSummaryDto(
            totalUsers, activeUsers, totalTenants, activeTenants, totalProjects, activeProjects);
    }

    public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken) =>
        await db.Tenants.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new TenantDto(x.Id, x.Code, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant code and name are required.");

        if (code.Length > 50 || name.Length > 200)
            throw new ArgumentException("Tenant code cannot exceed 50 characters and name cannot exceed 200 characters.");

        if (await db.Tenants.AnyAsync(x => x.Code == code, cancellationToken))
            throw new InvalidOperationException("Tenant code already exists.");

        var tenant = new Tenant { Code = code, Name = name };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return new TenantDto(tenant.Id, tenant.Code, tenant.Name, tenant.IsActive);
    }

    public Task<bool> IsPlatformAdminAsync(long userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.IsPlatformAdmin, cancellationToken);
}
