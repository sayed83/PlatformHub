using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PlatformHub.Application.Auth;
using PlatformHub.Application.Platform;
using PlatformHub.Domain.Entities;
using PlatformHub.Infrastructure.Auth;
using PlatformHub.Infrastructure.Persistence;
using PlatformHub.Infrastructure.Platform;
using PlatformHub.Api.Services;
using PlatformHub.Api.Platform;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PlatformDb"),
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo")));
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PlatformDb"),
        sql => sql.MigrationsHistoryTable("__InventoryMigrationsHistory", "dbo")));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPlatformService, PlatformService>();
builder.Services.AddScoped<IDapperStoredProcedureExecutor, DapperStoredProcedureExecutor>();
builder.Services.AddScoped<IScopedAccessService, ScopedAccessService>();
builder.Services.AddScoped<ActiveTenantContext>();
builder.Services.AddScoped<IActiveTenantContext>(sp => sp.GetRequiredService<ActiveTenantContext>());
builder.Services.AddScoped<EffectiveMenuAccessService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Too many requests",
            status = StatusCodes.Status429TooManyRequests,
            traceId = context.HttpContext.TraceIdentifier
        }, cancellationToken);
    };

    options.AddPolicy("auth-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("auth-refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("platform-bootstrap", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be configured through User Secrets or environment/secret storage and contain at least 32 UTF-8 bytes.");
}

var jwtIssuer = jwt["Issuer"];
var jwtAudience = jwt["Audience"];
if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Jwt:Issuer and Jwt:Audience must both be configured with non-empty values.");
}

builder.Services.AddAuthentication("PlatformBearer")
    .AddJwtBearer("PlatformBearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AppClaimTypes.UserId
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdText = context.Principal?.FindFirst(AppClaimTypes.UserId)?.Value;
                var sessionIdText = context.Principal?.FindFirst(AppClaimTypes.SessionId)?.Value;
                if (!long.TryParse(userIdText, out var userId) || !long.TryParse(sessionIdText, out var sessionId))
                {
                    context.Fail("Token has no valid session.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<PlatformDbContext>();
                var now = DateTime.UtcNow;
                var activeSession = await db.RefreshSessions.AsNoTracking().AnyAsync(
                    x => x.Id == sessionId && x.UserId == userId && x.RevokedUtc == null && x.ExpiresUtc > now,
                    context.HttpContext.RequestAborted);
                var activeUser = activeSession && await db.Users.AsNoTracking().AnyAsync(
                    x => x.Id == userId && x.IsActive, context.HttpContext.RequestAborted);
                if (!activeUser)
                    context.Fail("Token session has been revoked or the account is inactive.");
            }
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
    options.AddPolicy("Role.Deactivate", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Role.Deactivate"));
    options.AddPolicy("Role.Assign", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Role.Assign"));
    options.AddPolicy("Role.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Role.View"));
    options.AddPolicy("Permission.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "Permission.View"));
    options.AddPolicy("MenuAccess.View", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "MenuAccess.View"));
    options.AddPolicy("MenuAccess.Update", policy => policy.RequireAuthenticatedUser()
        .RequireClaim(AppClaimTypes.Permission, "MenuAccess.Update"));
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
    await ProjectNavigationSeeder.SeedAsync(db);
    await ProjectMenuPermissionSynchronizer.SyncAllAsync(db);
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    await PlatformRoleUserSeeder.SeedAsync(db, app.Configuration, passwordHasher, loggerFactory);
}

// Propagate the MVC correlation ID so API logs and MVC error pages share one reference.
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var isSafe = !string.IsNullOrWhiteSpace(supplied)
        && supplied.Length <= 64
        && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    var correlationId = isSafe ? supplied! : context.TraceIdentifier;

    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("PlatformHub.Api.Requests");

    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["RequestMethod"] = context.Request.Method,
        ["RequestPath"] = context.Request.Path.Value ?? "/"
    }))
    {
        await next();
    }
});

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("PlatformHub.Api.Errors");
    logger.LogError(exception, "Unhandled API exception. CorrelationId={CorrelationId}, Path={Path}",
        context.TraceIdentifier, context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new
    {
        type = "https://httpstatuses.com/500",
        title = "An unexpected server error occurred.",
        status = StatusCodes.Status500InternalServerError,
        traceId = context.TraceIdentifier
    });
}));

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ActiveTenantContextMiddleware>();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }))
    .AllowAnonymous()
    .WithTags("Health");

app.MapGet("/health/ready", async (PlatformDbContext db, CancellationToken ct) =>
{
    try
    {
        if (await db.Database.CanConnectAsync(ct))
            return (IResult)Results.Ok(new { status = "Ready" });
    }
    catch (Exception)
    {
        // Do not expose connection strings or provider errors through the health endpoint.
    }

    return (IResult)Results.Json(new { status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);
})
    .AllowAnonymous()
    .WithTags("Health");

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
    public const string SessionId = "ph_session_id";
}
