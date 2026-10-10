namespace PlatformHub.Application.Auth;

public sealed record LoginRequest(string UserName, string Password, string Panel);
public sealed record LoginResponse(long UserId, string UserName, string DisplayName, bool IsPlatformAdmin, string? BaseRedirectUrl);
public sealed record TenantDto(long Id, string Code, string Name, bool IsActive);
public sealed record CreateTenantRequest(string Code, string Name);
