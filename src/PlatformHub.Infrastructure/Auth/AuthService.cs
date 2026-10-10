using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Application.Auth;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Auth;

public sealed class AuthService(PlatformDbContext db, IPasswordHasher<PlatformHub.Domain.Entities.AppUser> hasher) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.UserName == request.UserName && x.IsActive, cancellationToken);

        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
            return null;

        // Panel separation is enforced here, not by hiding UI links.
        var wantsAdmin = string.Equals(request.Panel, "platform-admin", StringComparison.OrdinalIgnoreCase);
        if (wantsAdmin != user.IsPlatformAdmin)
            return null;

        return new LoginResponse(user.Id, user.UserName, user.DisplayName, user.IsPlatformAdmin, user.BaseRedirectUrl);
    }
}
