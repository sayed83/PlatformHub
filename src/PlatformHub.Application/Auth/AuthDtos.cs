namespace PlatformHub.Application.Auth;

public sealed record LoginRequest(string UserName, string Password, string Panel);
public sealed record RoleScopeDto(long? TenantId, long? BranchId, long? ProjectId);
public sealed record LoginResponse(
    long UserId,
    string UserName,
    string DisplayName,
    bool IsPlatformAdmin,
    string? BaseRedirectUrl,
    string? RoleRedirectUrl,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<RoleScopeDto> Scopes);
public sealed record TenantDto(long Id, string Code, string Name, bool IsActive);
public sealed record CreateTenantRequest(string Code, string Name);
public sealed record ProjectDto(long Id, long? TenantId, string? TenantName, string Code, string Name, string? BaseUrl, bool IsActive);
public sealed record CreateProjectRequest(long? TenantId, string Code, string Name, string? BaseUrl);
