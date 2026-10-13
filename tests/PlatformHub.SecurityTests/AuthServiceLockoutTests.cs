using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Application.Auth;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Auth;
using PlatformHub.Infrastructure.Persistence;
using Xunit;

namespace PlatformHub.SecurityTests;

public sealed class AuthServiceLockoutTests
{
    [Fact]
    public async Task Fifth_incorrect_password_locks_account_and_correct_password_is_rejected()
    {
        await using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, new PasswordHasher<AppUser>());

        for (var i = 0; i < AuthService.MaxFailedAttempts; i++)
            Assert.Null(await service.LoginAsync(new LoginRequest(user.UserName, "wrong-password", "tenant"), default));

        var persisted = await db.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(AuthService.MaxFailedAttempts, persisted.FailedLoginAttempts);
        Assert.True(persisted.LockoutEndUtc > DateTime.UtcNow);
        Assert.Null(await service.LoginAsync(new LoginRequest(user.UserName, "Correct-password-123!", "tenant"), default));
    }

    [Fact]
    public async Task Successful_login_clears_failure_counter_and_lockout()
    {
        await using var db = CreateDb();
        var user = CreateUser();
        user.FailedLoginAttempts = 4;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, new PasswordHasher<AppUser>());

        var result = await service.LoginAsync(new LoginRequest(user.UserName, "Correct-password-123!", "tenant"), default);

        Assert.NotNull(result);
        var persisted = await db.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(0, persisted.FailedLoginAttempts);
        Assert.Null(persisted.LockoutEndUtc);
    }

    [Fact]
    public async Task Expired_lockout_allows_login_and_resets_attempts()
    {
        await using var db = CreateDb();
        var user = CreateUser();
        user.FailedLoginAttempts = AuthService.MaxFailedAttempts;
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, new PasswordHasher<AppUser>());

        var result = await service.LoginAsync(new LoginRequest(user.UserName, "Correct-password-123!", "tenant"), default);

        Assert.NotNull(result);
        var persisted = await db.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(0, persisted.FailedLoginAttempts);
        Assert.Null(persisted.LockoutEndUtc);
    }

    private static AppUser CreateUser()
    {
        var user = new AppUser { Id = 10, UserName = "lockout-user", DisplayName = "Lockout User", IsActive = true };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, "Correct-password-123!");
        return user;
    }

    private static PlatformDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"auth-lockout-{Guid.NewGuid():N}")
            .Options;
        return new PlatformDbContext(options);
    }
}
