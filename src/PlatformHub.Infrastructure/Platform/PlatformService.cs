using Microsoft.EntityFrameworkCore;
using PlatformHub.Application.Auth;
using PlatformHub.Application.Platform;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

public sealed class PlatformService(PlatformDbContext db) : IPlatformService
{
    public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken) =>
        await db.Tenants.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new TenantDto(x.Id, x.Code, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Tenant code and name are required.");
        if (await db.Tenants.AnyAsync(x => x.Code == code, cancellationToken))
            throw new InvalidOperationException("Tenant code already exists.");

        var tenant = new Tenant { Code = code, Name = request.Name.Trim() };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return new TenantDto(tenant.Id, tenant.Code, tenant.Name, tenant.IsActive);
    }

    public Task<bool> IsPlatformAdminAsync(long userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.IsPlatformAdmin, cancellationToken);
}
