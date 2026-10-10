using System.ComponentModel.DataAnnotations;

namespace PlatformHub.Application.Auth;

public sealed class FirstPlatformAdminRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string Password { get; init; } = string.Empty;
}
