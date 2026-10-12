namespace PlatformHub.Application.Platform;

public sealed record BranchDto(
    long Id,
    long TenantId,
    string TenantCode,
    string TenantName,
    string Code,
    string Name,
    string? Address,
    bool IsActive);

public sealed record CreateBranchRequest(
    long TenantId,
    string Code,
    string Name,
    string? Address);

public sealed record ProjectUpdateRequest(string Code, string Name, string? BaseUrl);
