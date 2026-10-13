namespace PlatformHub.Domain.Entities;

public sealed class RefreshSession
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}
