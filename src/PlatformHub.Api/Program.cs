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

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PlatformDb")));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPlatformService, PlatformService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
if (builder.Environment.IsProduction() && signingKey.StartsWith("CHANGE-THIS", StringComparison.Ordinal))
    throw new InvalidOperationException("Configure a strong Jwt:SigningKey in secret storage before production.");

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
});

var app = builder.Build();

// Opt-in first-admin seeding. Credentials must come from User Secrets or environment variables.
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<PlatformDbContext>();
    var passwordHasher = services.GetRequiredService<IPasswordHasher<AppUser>>();

    await PlatformAdminSeeder.SeedAsync(
        db,
        app.Configuration,
        passwordHasher);
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
}
