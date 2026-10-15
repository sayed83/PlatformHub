using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PlatformHub.Application.Auth;
using PlatformHub.Application.Platform;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Auth;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;
using PlatformHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var connectionString = builder.Configuration.GetConnectionString("PlatformDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:PlatformDb is required. Configure it using User Secrets locally or ConnectionStrings__PlatformDb in the hosting environment.");

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo")));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPlatformService, PlatformService>();
builder.Services.AddScoped<IDapperStoredProcedureExecutor, DapperStoredProcedureExecutor>();
builder.Services.AddScoped<IScopedAccessService, ScopedAccessService>();
builder.Services.AddScoped<EffectiveMenuAccessService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey is required and must be at least 32 UTF-8 bytes. Configure it with User Secrets locally or Jwt__SigningKey in the hosting environment.");

if (string.IsNullOrWhiteSpace(jwt["Issuer"]) || string.IsNullOrWhiteSpace(jwt["Audience"]))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must both be configured.");

builder.Services.AddAuthentication("PlatformBearer")
    .AddJwtBearer("PlatformBearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AppClaimTypes.UserId
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.PlatformAdmin, "true"));
    options.AddPolicy("PlatformSuperAdmin", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Role, PlatformRbacSeeder.SuperAdminCode));
    options.AddPolicy("User.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "User.View"));
    options.AddPolicy("User.Create", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "User.Create"));
    options.AddPolicy("User.Update", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "User.Update"));
    options.AddPolicy("User.Deactivate", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "User.Deactivate"));
    options.AddPolicy("Role.Create", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Role.Create"));
    options.AddPolicy("Role.Update", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Role.Update"));
    options.AddPolicy("Role.Assign", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Role.Assign"));
    options.AddPolicy("Role.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Role.View"));
    options.AddPolicy("Permission.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Permission.View"));
    options.AddPolicy("Platform.Dashboard.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Platform.Dashboard.View"));
    options.AddPolicy("Tenant.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Tenant.View"));
    options.AddPolicy("Tenant.Update", policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.Permission, "Tenant.Update"));
    options.AddPolicy("Tenant.Deactivate", policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.Permission, "Tenant.Deactivate"));
    options.AddPolicy("Tenant.Create", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Tenant.Create"));
    options.AddPolicy("Branch.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Branch.View"));
    options.AddPolicy("Branch.Update", policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.Permission, "Branch.Update"));
    options.AddPolicy("Branch.Deactivate", policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.Permission, "Branch.Deactivate"));
    options.AddPolicy("Branch.Create", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Branch.Create"));
    options.AddPolicy("Project.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Project.View"));
    options.AddPolicy("Project.Update", policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.Permission, "Project.Update"));
    options.AddPolicy("Project.Deactivate", policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.Permission, "Project.Deactivate"));
    options.AddPolicy("Project.Create", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Project.Create"));
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<PlatformDbContext>();
    var passwordHasher = services.GetRequiredService<IPasswordHasher<AppUser>>();

    await PlatformAdminSeeder.SeedAsync(db, app.Configuration, passwordHasher);
    await PlatformRbacSeeder.SeedAsync(db);
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    await PlatformRoleUserSeeder.SeedAsync(db, app.Configuration, passwordHasher, loggerFactory);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.Run();

public static class AppClaimTypes
{
    public const string UserId = "ph_user_id";
    public const string UserName = "ph_user_name";
    public const string DisplayName = "ph_display_name";
    public const string PlatformAdmin = "ph_platform_admin";
    public const string Role = "ph_role";
    public const string Permission = "ph_permission";
    public const string TenantId = "ph_tenant_id";
    public const string BranchId = "ph_branch_id";
    public const string ProjectId = "ph_project_id";
}
