using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Application.Auth;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Controllers;

/// <summary>
/// One-time endpoint used only to provision the very first Platform Admin.
/// Configure PlatformBootstrap:SetupKey using User Secrets/environment variables.
/// Remove or disable this endpoint after initial provisioning in a deployed environment.
/// </summary>
[ApiController]
[Route("api/v1/platform-bootstrap")]
public sealed class PlatformBootstrapController(
    PlatformDbContext db,
    IPasswordHasher<AppUser> passwordHasher,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("first-admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateFirstAdmin(
        [FromBody] FirstPlatformAdminRequest request,
        CancellationToken cancellationToken)
    {
        // Do not run this initialization endpoint in production. Use a private deployment process instead.
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var configuredKey = configuration["PlatformBootstrap:SetupKey"];
        var providedKey = Request.Headers["X-Platform-Setup-Key"].ToString();

        if (string.IsNullOrWhiteSpace(configuredKey) || configuredKey.Length < 32)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Bootstrap is not configured. Set PlatformBootstrap:SetupKey to a random secret of at least 32 characters."
            });
        }

        if (!FixedTimeEquals(configuredKey, providedKey))
        {
            return Unauthorized(new { message = "Invalid setup key." });
        }

        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 12)
        {
            return BadRequest(new
            {
                message = "Username and display name are required; password must be at least 12 characters."
            });
        }

        var userName = request.UserName.Trim();

        // Serializable isolation helps prevent two simultaneous setup requests from both succeeding.
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        if (await db.Users.AnyAsync(x => x.IsPlatformAdmin, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Platform Admin has already been initialized." });
        }

        if (await db.Users.AnyAsync(x => x.UserName == userName, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Username already exists." });
        }

        var user = new AppUser
        {
            UserName = userName,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            IsPlatformAdmin = true,
            CreatedUtc = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Could not create the first administrator. Verify the username and retry." });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            user.Id,
            user.UserName,
            user.DisplayName,
            user.IsPlatformAdmin,
            user.IsActive
        });
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
