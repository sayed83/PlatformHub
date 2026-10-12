using PlatformHub.Application.Auth;

namespace PlatformHub.Application.Platform;

public interface IPlatformService
{
    Task<PlatformSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken);
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken);
    Task<TenantDto> UpdateTenantAsync(long id, string code, string name, CancellationToken cancellationToken);
    Task DeleteTenantAsync(long id, bool hardDelete, CancellationToken cancellationToken);
    Task ActivateTenantAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(long? tenantId, CancellationToken cancellationToken);
    Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(long tenantId, CancellationToken cancellationToken);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken);
    Task<BranchDto> UpdateBranchAsync(long id, string code, string name, string? address, CancellationToken cancellationToken);
    Task DeleteBranchAsync(long id, bool hardDelete, CancellationToken cancellationToken);
    Task ActivateBranchAsync(long id, CancellationToken cancellationToken);
    Task<bool> IsPlatformAdminAsync(long userId, CancellationToken cancellationToken);
}
