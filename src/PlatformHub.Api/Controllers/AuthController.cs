using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PlatformHub.Application.Auth;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthService auth,
    IConfiguration configuration,
    PlatformDbContext db,
    ILogger<AuthController> logger) : ControllerBase
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(14);

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required." });

        var user = await auth.LoginAsync(request, cancellationToken);
        if (user is null)
            return Unauthorized(new { message = "Invalid credentials or this account cannot access this login panel." });

        var refreshToken = CreateRefreshToken();
        var session = new RefreshSession
        {
            UserId = user.UserId,
            TokenHash = HashToken(refreshToken),
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.Add(RefreshLifetime)
        };
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = CreateAccessToken(user, session.Id);
        logger.LogInformation("Authentication succeeded for UserId={UserId}; SessionId={SessionId}",
            user.UserId, session.Id);
        return Ok(new { accessToken, refreshToken, expiresIn = 900, refreshExpiresIn = (int)RefreshLifetime.TotalSeconds, user });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 200)
            return Unauthorized(new { message = "Refresh token is invalid or expired." });

        var tokenHash = HashToken(request.RefreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (session is null)
            return Unauthorized(new { message = "Refresh token is invalid or expired." });

        var now = DateTime.UtcNow;
        var userEntity = await db.Users.SingleOrDefaultAsync(x => x.Id == session.UserId, cancellationToken);
        if (session.RevokedUtc.HasValue)
        {
            // Reuse of a rotated token indicates possible theft: revoke every active session for this user.
            await db.RefreshSessions.Where(x => x.UserId == session.UserId && x.RevokedUtc == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedUtc, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning("Revoked all refresh sessions after token reuse. UserId={UserId}", session.UserId);
            return Unauthorized(new { message = "Refresh token reuse detected. Sign in again." });
        }

        if (session.ExpiresUtc <= now || userEntity is null || !userEntity.IsActive)
        {
            session.RevokedUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Unauthorized(new { message = "Refresh token is invalid or expired." });
        }

        var user = await BuildLoginResponseAsync(userEntity, cancellationToken);
        var nextRefreshToken = CreateRefreshToken();
        var nextHash = HashToken(nextRefreshToken);
        session.RevokedUtc = now;
        session.ReplacedByTokenHash = nextHash;
        var nextSession = new RefreshSession
        {
            UserId = userEntity.Id,
            TokenHash = nextHash,
            CreatedUtc = now,
            ExpiresUtc = now.Add(RefreshLifetime)
        };
        db.RefreshSessions.Add(nextSession);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var accessToken = CreateAccessToken(user, nextSession.Id);
        logger.LogInformation("Refresh token rotated. UserId={UserId}; PreviousSessionId={PreviousSessionId}; SessionId={SessionId}",
            userEntity.Id, session.Id, nextSession.Id);
        return Ok(new { accessToken, refreshToken = nextRefreshToken, expiresIn = 900, refreshExpiresIn = (int)RefreshLifetime.TotalSeconds, user });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken) && request.RefreshToken.Length <= 200)
        {
            var hash = HashToken(request.RefreshToken);
            var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
            if (session is not null && !session.RevokedUtc.HasValue)
            {
                session.RevokedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Session revoked on logout. UserId={UserId}; SessionId={SessionId}",
                    session.UserId, session.Id);
            }
        }

        // Idempotent by design: logout succeeds even if the token was already expired/revoked.
        return Ok(new { message = "Session revoked." });
    }

    private async Task<LoginResponse> BuildLoginResponseAsync(AppUser user, CancellationToken ct)
    {
        var assignments = await db.UserRoleAssignments.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Role.IsActive)
            .Select(x => new { x.Role.Code, x.Role.BaseRedirectUrl, x.TenantId, x.BranchId, x.ProjectId })
            .ToListAsync(ct);
        var roles = assignments.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roleRedirect = assignments.Select(x => x.BaseRedirectUrl)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var permissions = await (
            from assignment in db.UserRoleAssignments.AsNoTracking()
            join rolePermission in db.RolePermissions.AsNoTracking() on assignment.RoleId equals rolePermission.RoleId
            join permission in db.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
            where assignment.UserId == user.Id && assignment.Role.IsActive
            select permission.Code)
            .Distinct().ToListAsync(ct);
        var scopes = assignments.Select(x => new RoleScopeDto(x.TenantId, x.BranchId, x.ProjectId)).Distinct().ToArray();
        return new LoginResponse(user.Id, user.UserName, user.DisplayName, user.IsPlatformAdmin,
            user.BaseRedirectUrl, roleRedirect, roles, permissions, scopes);
    }

    private string CreateAccessToken(LoginResponse user, long sessionId)
    {
        var jwt = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, user.UserId.ToString()),
            new(AppClaimTypes.UserName, user.UserName),
            new(AppClaimTypes.DisplayName, user.DisplayName),
            new(AppClaimTypes.PlatformAdmin, user.IsPlatformAdmin ? "true" : "false"),
            new(AppClaimTypes.SessionId, sessionId.ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(AppClaimTypes.Role, role)));
        claims.AddRange(user.Permissions.Select(permission => new Claim(AppClaimTypes.Permission, permission)));
        foreach (var scope in user.Scopes)
        {
            if (scope.TenantId.HasValue) claims.Add(new Claim(AppClaimTypes.TenantId, scope.TenantId.Value.ToString()));
            if (scope.BranchId.HasValue) claims.Add(new Claim(AppClaimTypes.BranchId, scope.BranchId.Value.ToString()));
            if (scope.ProjectId.HasValue) claims.Add(new Claim(AppClaimTypes.ProjectId, scope.ProjectId.Value.ToString()));
        }

        var token = new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateRefreshToken() => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
