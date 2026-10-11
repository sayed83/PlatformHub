using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

/// <summary>
/// Optional, configuration-driven seed accounts for built-in roles.
/// Passwords must be supplied through User Secrets or environment variables; never hard-coded.
/// Existing users and assignments are preserved.
/// </summary>
public static class PlatformRoleUserSeeder
{
    private static readonly (string RoleCode, string ConfigKey, bool IsPlatformAdmin)[] SeedAccounts =
    [
        ("PLATFORM_ADMIN", "PlatformRoleUserSeeds:PlatformAdmin", true),
        ("TENANT_ADMIN", "PlatformRoleUserSeeds:TenantAdmin", false),
        ("BRANCH_ADMIN", "PlatformRoleUserSeeds:BranchAdmin", false),
        ("PROJECT_USER", "PlatformRoleUserSeeds:ProjectUser", false)
    ];

    public static async Task SeedAsync(
        PlatformDbContext db,
        IConfiguration configuration,
        IPasswordHasher<AppUser> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        foreach (var seed in SeedAccounts)
        {
            var section = configuration.GetSection(seed.ConfigKey);
            var userName = section["UserName"]?.Trim();
            var displayName = section["DisplayName"]?.Trim();
            var password = section["Password"];
            var hasAnyValue = !string.IsNullOrWhiteSpace(userName)
                || !string.IsNullOrWhiteSpace(displayName)
                || !string.IsNullOrWhiteSpace(password);

            // Opt-in per account. This avoids silently creating predictable default credentials.
            if (!hasAnyValue) continue;

            if (string.IsNullOrWhiteSpace(userName)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    $"{seed.ConfigKey} requires UserName, DisplayName, and Password.");

            if (password.Length < 12)
                throw new InvalidOperationException($"{seed.ConfigKey}:Password must be at least 12 characters.");

            var role = await db.Roles.SingleOrDefaultAsync(
                x => x.Code == seed.RoleCode && x.IsActive, cancellationToken);
            if (role is null)
                throw new InvalidOperationException($"Cannot seed {seed.RoleCode}: active role was not found.");

            var user = await db.Users.SingleOrDefaultAsync(x => x.UserName == userName, cancellationToken);
            if (user is null)
            {
                user = new AppUser
                {
                    UserName = userName,
                    DisplayName = displayName,
                    IsActive = true,
                    IsPlatformAdmin = seed.IsPlatformAdmin,
                    CreatedUtc = DateTime.UtcNow
                };
                user.PasswordHash = passwordHasher.HashPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (user.IsPlatformAdmin != seed.IsPlatformAdmin)
            {
                throw new InvalidOperationException(
                    $"Cannot seed {seed.RoleCode}: username '{userName}' already belongs to an account with a different platform-admin status.");
            }

            if (!user.IsActive)
                throw new InvalidOperationException(
                    $"Cannot assign the seed role to inactive user '{userName}'. Activate the account explicitly first.");

            var tenantId = ReadOptionalId(section["TenantId"], $"{seed.ConfigKey}:TenantId");
            var branchId = ReadOptionalId(section["BranchId"], $"{seed.ConfigKey}:BranchId");
            var projectId = ReadOptionalId(section["ProjectId"], $"{seed.ConfigKey}:ProjectId");

            if (seed.RoleCode == "TENANT_ADMIN" && !tenantId.HasValue)
                throw new InvalidOperationException($"{seed.ConfigKey}:TenantId is required for Tenant Admin.");
            if (seed.RoleCode == "BRANCH_ADMIN" && !branchId.HasValue)
                throw new InvalidOperationException($"{seed.ConfigKey}:BranchId is required for Branch Admin.");
            if (seed.RoleCode == "PROJECT_USER" && !projectId.HasValue)
                throw new InvalidOperationException($"{seed.ConfigKey}:ProjectId is required for Project User.");

            if (projectId.HasValue)
            {
                var projectTenantId = await db.Projects.AsNoTracking()
                    .Where(x => x.Id == projectId.Value && x.IsActive)
                    .Select(x => (long?)x.TenantId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!projectTenantId.HasValue)
                    throw new InvalidOperationException($"{seed.ConfigKey}:ProjectId must reference an active project.");
                if (tenantId.HasValue && tenantId.Value != projectTenantId.Value)
                    throw new InvalidOperationException($"{seed.ConfigKey}:ProjectId does not belong to TenantId.");
                tenantId ??= projectTenantId;
            }

            if (branchId.HasValue)
            {
                var branchTenantId = await db.Branches.AsNoTracking()
                    .Where(x => x.Id == branchId.Value && x.IsActive)
                    .Select(x => (long?)x.TenantId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!branchTenantId.HasValue)
                    throw new InvalidOperationException($"{seed.ConfigKey}:BranchId must reference an active branch.");
                if (tenantId.HasValue && tenantId.Value != branchTenantId.Value)
                    throw new InvalidOperationException($"{seed.ConfigKey}:BranchId does not belong to TenantId.");
                tenantId ??= branchTenantId;
            }

            if (tenantId.HasValue && !await db.Tenants.AnyAsync(
                    x => x.Id == tenantId.Value && x.IsActive, cancellationToken))
                throw new InvalidOperationException($"{seed.ConfigKey}:TenantId must reference an active tenant.");

            var alreadyAssigned = await db.UserRoleAssignments.AnyAsync(x =>
                x.UserId == user.Id && x.RoleId == role.Id
                && x.TenantId == tenantId && x.BranchId == branchId && x.ProjectId == projectId,
                cancellationToken);
            if (!alreadyAssigned)
            {
                db.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    TenantId = tenantId,
                    BranchId = branchId,
                    ProjectId = projectId
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static long? ReadOptionalId(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!long.TryParse(value, out var id) || id <= 0)
            throw new InvalidOperationException($"{key} must be a positive integer.");
        return id;
    }
}
