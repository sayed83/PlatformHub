using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Mvc.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed record ApiLoginResponse(string AccessToken, int ExpiresIn, ApiUser User);
public sealed record ApiUser(long UserId, string UserName, string DisplayName, bool IsPlatformAdmin, string? BaseRedirectUrl, string? RoleRedirectUrl, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, IReadOnlyList<ApiUserScope> Scopes);
public sealed record ApiUserScope(long? TenantId, long? BranchId, long? ProjectId);
