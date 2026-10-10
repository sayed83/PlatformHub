using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PlatformHub.Application.Auth;

namespace PlatformHub.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth, IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required." });

        var user = await auth.LoginAsync(request, cancellationToken);
        if (user is null) return Unauthorized(new { message = "Invalid credentials or this account cannot access this login panel." });

        var jwt = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(AppClaimTypes.UserId, user.UserId.ToString()),
            new Claim(AppClaimTypes.UserName, user.UserName),
            new Claim(AppClaimTypes.DisplayName, user.DisplayName),
            new Claim(AppClaimTypes.PlatformAdmin, user.IsPlatformAdmin ? "true" : "false")
        };
        var token = new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresIn = 900, user });
    }
}
