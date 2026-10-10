using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlatformHub.Domain.Entities;

namespace PlatformHub.Infrastructure.Persistence;

/// <summary>
/// Creates the initial Platform Admin from local User Secrets or environment configuration.
/// Never stores a plaintext password and never overwrites an existing administrator.
/// </summary>
public static class PlatformAdminSeeder
{
    public static async Task SeedAsync(
        PlatformDbContext db,
        IConfiguration configuration,
        IPasswordHasher<AppUser> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        var userName = configuration["PlatformAdminSeed:UserName"];
        var displayName = configuration["PlatformAdminSeed:DisplayName"];
        var password = configuration["PlatformAdminSeed:Password"];

        // Seeding is opt-in. Without seed settings, startup behaves normally.
        if (string.IsNullOrWhiteSpace(userName) &&
            string.IsNullOrWhiteSpace(displayName) &&
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "PlatformAdminSeed requires UserName, DisplayName, and Password configuration.");
        }

        if (password.Length < 12)
        {
            throw new InvalidOperationException(
                "PlatformAdminSeed:Password must contain at least 12 characters.");
        }

        // Existing administrator means initialization has already completed.
        if (await db.Users.AnyAsync(u => u.IsPlatformAdmin, cancellationToken))
        {
            return;
        }

        userName = userName.Trim();
        if (await db.Users.AnyAsync(u => u.UserName == userName, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Cannot seed Platform Admin: username '{userName}' already belongs to a non-admin user.");
        }

        var user = new AppUser
        {
            UserName = userName,
            DisplayName = displayName.Trim(),
            IsActive = true,
            IsPlatformAdmin = true,
            CreatedUtc = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Platform Admin seeding failed. Check database connectivity and unique username constraints.", ex);
        }
    }
}
