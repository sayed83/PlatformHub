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
        return new PlatformSummaryDto(totalUsers, activeUsers, totalTenants, activeTenants, totalProjects, activeProjects);
    }

    public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken) =>
        await db.Tenants.AsNoTracking().OrderBy(x => x.Name)
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


    public async Task<TenantDto> UpdateTenantAsync(long id, string code, string name, CancellationToken cancellationToken)
    {
        code = code?.Trim().ToUpperInvariant() ?? "";
        name = name?.Trim() ?? "";
        if (id <= 0 || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A valid tenant ID, code, and name are required.");
        if (code.Length > 50 || name.Length > 200)
            throw new ArgumentException("Tenant code cannot exceed 50 characters and name cannot exceed 200 characters.");
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ArgumentException("Tenant was not found.");
        if (await db.Tenants.AnyAsync(x => x.Id != id && x.Code == code, cancellationToken))
            throw new InvalidOperationException("Tenant code already exists.");
        tenant.Code = code;
        tenant.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return new TenantDto(tenant.Id, tenant.Code, tenant.Name, tenant.IsActive);
    }

    public async Task DeleteTenantAsync(long id, bool hardDelete, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ArgumentException("Tenant was not found.");
        if (!hardDelete)
        {
            tenant.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        var hasBranches = await db.Branches.AnyAsync(x => x.TenantId == id, cancellationToken);
        var hasProjects = await db.Projects.AnyAsync(x => x.TenantId == id, cancellationToken);
        var hasAssignments = await db.UserRoleAssignments.AnyAsync(x => x.TenantId == id, cancellationToken);
        if (hasBranches || hasProjects || hasAssignments)
            throw new InvalidOperationException("Hard delete is unavailable because this tenant has branches, projects, or role assignments. Deactivate it instead.");
        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(long? tenantId, CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking().Include(x => x.Tenant).AsQueryable();
        if (tenantId.HasValue) query = query.Where(x => x.TenantId == tenantId.Value);
        return await query.OrderBy(x => x.Tenant.Name).ThenBy(x => x.Name)
            .Select(x => new ProjectDto(x.Id, x.TenantId, x.Tenant.Name, x.Code, x.Name, x.BaseUrl, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();
        if (request.TenantId <= 0 || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant, project code, and project name are required.");
        if (code.Length > 50 || name.Length > 200 || (baseUrl?.Length ?? 0) > 500)
            throw new ArgumentException("Project code must be at most 50 characters, name 200, and URL 500.");
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == request.TenantId, cancellationToken);
        if (tenant is null) throw new ArgumentException("The selected tenant does not exist.");
        if (!tenant.IsActive) throw new InvalidOperationException("Projects cannot be created for an inactive tenant.");
        if (await db.Projects.AnyAsync(x => x.TenantId == request.TenantId && x.Code == code, cancellationToken))
            throw new InvalidOperationException("This project code already exists for the selected tenant.");
        var project = new Project { TenantId = tenant.Id, Tenant = tenant, Code = code, Name = name, BaseUrl = baseUrl };
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectDto(project.Id, tenant.Id, tenant.Name, project.Code, project.Name, project.BaseUrl, project.IsActive);
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(long tenantId, CancellationToken cancellationToken)
    {
        if (tenantId <= 0) throw new ArgumentException("A valid tenant ID is required.");
        return await db.Branches.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new BranchDto(x.Id, x.TenantId, x.Tenant.Code, x.Tenant.Name, x.Code, x.Name, x.Address, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (request.TenantId <= 0 || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant, branch code, and branch name are required.");
        if (code.Length > 50 || name.Length > 200 || (address?.Length ?? 0) > 500)
            throw new ArgumentException("Branch code must be at most 50 characters, name 200, and address 500.");
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == request.TenantId, cancellationToken);
        if (tenant is null) throw new ArgumentException("The selected tenant does not exist.");
        if (!tenant.IsActive) throw new InvalidOperationException("Branches cannot be created for an inactive tenant.");
        if (await db.Branches.AnyAsync(x => x.TenantId == request.TenantId && x.Code == code, cancellationToken))
            throw new InvalidOperationException("This branch code already exists for the selected tenant.");
        var branch = new Branch
        {
            TenantId = tenant.Id, Tenant = tenant, Code = code, Name = name, Address = address
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);
        return new BranchDto(branch.Id, tenant.Id, tenant.Code, tenant.Name, branch.Code, branch.Name, branch.Address, branch.IsActive);
    }


    public async Task<BranchDto> UpdateBranchAsync(long id, string code, string name, string? address, CancellationToken cancellationToken)
    {
        code = code?.Trim().ToUpperInvariant() ?? "";
        name = name?.Trim() ?? "";
        address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        if (id <= 0 || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A valid branch ID, code, and name are required.");
        if (code.Length > 50 || name.Length > 200 || (address?.Length ?? 0) > 500)
            throw new ArgumentException("Branch code must be at most 50 characters, name 200, and address 500.");
        var branch = await db.Branches.Include(x => x.Tenant).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ArgumentException("Branch was not found.");
        if (await db.Branches.AnyAsync(x => x.Id != id && x.TenantId == branch.TenantId && x.Code == code, cancellationToken))
            throw new InvalidOperationException("This branch code already exists for the selected tenant.");
        branch.Code = code;
        branch.Name = name;
        branch.Address = address;
        await db.SaveChangesAsync(cancellationToken);
        return new BranchDto(branch.Id, branch.TenantId, branch.Tenant.Code, branch.Tenant.Name, branch.Code, branch.Name, branch.Address, branch.IsActive);
    }

    public async Task DeleteBranchAsync(long id, bool hardDelete, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ArgumentException("Branch was not found.");
        if (!hardDelete)
        {
            branch.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        var hasAssignments = await db.UserRoleAssignments.AnyAsync(x => x.BranchId == id, cancellationToken);
        if (hasAssignments)
            throw new InvalidOperationException("Hard delete is unavailable because this branch has role assignments. Deactivate it instead.");
        db.Branches.Remove(branch);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsPlatformAdminAsync(long userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.IsPlatformAdmin, cancellationToken);
}
