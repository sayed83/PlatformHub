using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Mvc.Models;

public sealed class LoginViewModel
{
    [Required, StringLength(100)] public string UserName { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
}
public sealed record ApiLoginResponse(string AccessToken, int ExpiresIn, ApiUser User);
public sealed record ApiUser(long UserId, string UserName, string DisplayName, bool IsPlatformAdmin, string? BaseRedirectUrl);
