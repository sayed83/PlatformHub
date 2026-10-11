using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Application.Auth;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Auth;

public sealed class AuthService(PlatformDbContext db, IPasswordHasher<AppUser> hasher) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.UserName == request.UserName && x.IsActive, cancellationToken);

        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
            return null;

        var wantsAdmin = string.Equals(request.Panel, "platform-admin", StringComparison.OrdinalIgnoreCase);
        if (wantsAdmin != user.IsPlatformAdmin)
            return null;

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
