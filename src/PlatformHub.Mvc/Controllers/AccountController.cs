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

        await SignIn(result, ct);
        return LocalRedirect(Url.IsLocalUrl(result.User.BaseRedirectUrl) ? result.User.BaseRedirectUrl! : "/Home/Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Denied() => Forbid();

    private async Task SignIn(ApiLoginResponse result, CancellationToken ct)
    {
        // Keep access token server-side in auth ticket claims. Never expose it to browser JavaScript.
        var claims = new List<Claim>
        {
            new("ph_user_id", result.User.UserId.ToString()),
            new("ph_user_name", result.User.UserName),
            new("ph_display_name", result.User.DisplayName),
            new("ph_platform_admin", "false"),
            new("ph_api_access_token", result.AccessToken)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }
}
