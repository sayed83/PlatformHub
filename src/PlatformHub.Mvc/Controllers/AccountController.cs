using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Controllers;

public sealed class AccountController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await api.PostAsync<ApiLoginResponse>("api/v1/auth/login",
            new { model.UserName, model.Password, Panel = "tenant" }, ct);
        if (result is null || result.User.IsPlatformAdmin)
        {
            ModelState.AddModelError("", "Login failed. Check your credentials and account access.");
            return View(model);
        }

        await SignIn(result);
        // Normal users must never be redirected into the Platform Admin area by a role's
        // configurable BaseRedirectUrl. Platform administration requires the separate
        // platform-admin account flag and its own login panel.
        return LocalRedirect("/Workspace");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = User.FindFirst("ph_api_refresh_token")?.Value;
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await api.PostAsync<object>("api/v1/auth/logout", new { RefreshToken = refreshToken });
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private async Task SignIn(ApiLoginResponse result)
    {
        // Keep the access token in the server-side auth ticket; never expose it to browser JavaScript.
        var claims = new List<Claim>
        {
            new("ph_user_id", result.User.UserId.ToString()),
            new("ph_user_name", result.User.UserName),
            new("ph_display_name", result.User.DisplayName),
            new("ph_platform_admin", "false"),
            new("ph_api_access_token", result.AccessToken),
            new("ph_api_refresh_token", result.RefreshToken)
        };
        claims.AddRange(result.User.Roles.Select(x => new Claim("ph_role", x)));
        claims.AddRange(result.User.Permissions.Select(x => new Claim("ph_permission", x)));
        foreach (var scope in result.User.Scopes)
        {
            if (scope.TenantId.HasValue) claims.Add(new Claim("ph_tenant_id", scope.TenantId.Value.ToString()));
            if (scope.BranchId.HasValue) claims.Add(new Claim("ph_branch_id", scope.BranchId.Value.ToString()));
            if (scope.ProjectId.HasValue) claims.Add(new Claim("ph_project_id", scope.ProjectId.Value.ToString()));
        }
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }
}
