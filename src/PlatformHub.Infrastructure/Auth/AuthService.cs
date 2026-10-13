using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Application.Auth;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Auth;

public sealed class AuthService(PlatformDbContext db, IPasswordHasher<AppUser> hasher) : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.UserName == request.UserName, cancellationToken);

        // Keep the response identical for unknown, inactive, locked, and incorrect-password accounts.
        if (user is null || !user.IsActive)
            return null;

        var now = DateTime.UtcNow;
        if (user.LockoutEndUtc.HasValue)
        {
            if (user.LockoutEndUtc.Value > now)
                return null;

            // Expired lockout: start a fresh attempt window.
            user.LockoutEndUtc = null;
            user.FailedLoginAttempts = 0;
        }

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts = Math.Min(user.FailedLoginAttempts + 1, MaxFailedAttempts);
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LockoutEndUtc = now.Add(LockoutDuration);

            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var wantsAdmin = string.Equals(request.Panel, "platform-admin", StringComparison.OrdinalIgnoreCase);
        if (wantsAdmin != user.IsPlatformAdmin)
        {
            // The password is correct but the account is not allowed on this panel.
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        var assignments = await db.UserRoleAssignments
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Role.IsActive)
            .Select(x => new
            {
                x.Role.Code,
                x.Role.BaseRedirectUrl,
                x.TenantId,
                x.BranchId,
                x.ProjectId
            })
            .ToListAsync(cancellationToken);

        var roleCodes = assignments.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roleRedirectUrl = assignments.Select(x => x.BaseRedirectUrl).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var permissionCodes = await (
            from assignment in db.UserRoleAssignments.AsNoTracking()
            join rolePermission in db.RolePermissions.AsNoTracking() on assignment.RoleId equals rolePermission.RoleId
            join permission in db.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
            where assignment.UserId == user.Id && assignment.Role.IsActive
            select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var scopes = assignments
            .Select(x => new RoleScopeDto(x.TenantId, x.BranchId, x.ProjectId))
            .Distinct()
            .ToArray();

        return new LoginResponse(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.IsPlatformAdmin,
            user.BaseRedirectUrl,
            roleRedirectUrl,
            roleCodes,
            permissionCodes,
            scopes);
    }
}
