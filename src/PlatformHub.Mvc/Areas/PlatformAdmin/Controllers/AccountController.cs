using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
public sealed class AccountController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // Preserve only local return URLs to prevent open-redirect vulnerabilities.
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl, CancellationToken ct)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!ModelState.IsValid) return View(model);
        var result = await api.PostAsync<ApiLoginResponse>("api/v1/auth/login",
            new { model.UserName, model.Password, Panel = "platform-admin" }, ct);
        if (result is null || !result.User.IsPlatformAdmin)
        {
            ModelState.AddModelError("", "Invalid Platform Admin credentials.");
            return View(model);
        }

        var claims = BuildClaims(result);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

        // An explicitly requested local page takes priority over configured landing pages.
        // This lets an admin return to Menu Management (or another protected page) after login.
        if (Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl!);

        var redirect = !string.IsNullOrWhiteSpace(result.User.BaseRedirectUrl)
            ? result.User.BaseRedirectUrl
            : result.User.RoleRedirectUrl;
        if (Url.IsLocalUrl(redirect))
            return LocalRedirect(redirect!);

        return RedirectToAction("Index", "Dashboard", new { area = "PlatformAdmin" });
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

    private static List<Claim> BuildClaims(ApiLoginResponse result)
    {
        var claims = new List<Claim>
        {
            new("ph_user_id", result.User.UserId.ToString()),
            new("ph_user_name", result.User.UserName),
            new("ph_display_name", result.User.DisplayName),
            new("ph_platform_admin", "true"),
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
        return claims;
    }
}
