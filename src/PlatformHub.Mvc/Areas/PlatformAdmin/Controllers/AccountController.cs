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
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await api.PostAsync<ApiLoginResponse>("api/v1/auth/login",
            new { model.UserName, model.Password, Panel = "platform-admin" }, ct);
        if (result is null || !result.User.IsPlatformAdmin)
        {
            ModelState.AddModelError("", "Invalid Platform Admin credentials.");
            return View(model);
        }

        var claims = new[]
        {
            new Claim("ph_user_id", result.User.UserId.ToString()),
            new Claim("ph_user_name", result.User.UserName),
            new Claim("ph_display_name", result.User.DisplayName),
            new Claim("ph_platform_admin", "true"),
            new Claim("ph_api_access_token", result.AccessToken)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
        return RedirectToAction("Index", "Dashboard", new { area = "PlatformAdmin" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
