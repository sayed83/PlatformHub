using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class DashboardController(PlatformApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        var summary = await api.GetAsync<object>("api/v1/platform/summary", token, ct);
        if (summary is null) return Forbid();
        ViewBag.DisplayName = User.FindFirst("ph_display_name")?.Value ?? "Administrator";
        return View(summary);
    }
}
