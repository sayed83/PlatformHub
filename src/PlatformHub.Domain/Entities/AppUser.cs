namespace PlatformHub.Domain.Entities;

public sealed class AppUser
{
    public long Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }
    public string? BaseRedirectUrl { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
