using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Areas.PlatformAdmin.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class DashboardController(PlatformApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true")
            return Forbid();

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token))
            return Challenge();

        var summary = await api.GetAsync<PlatformSummaryViewModel>("api/v1/platform/summary", token, ct);
        var tenants = await api.GetAsync<List<TenantViewModel>>("api/v1/platform/tenants", token, ct);

        if (summary is null || tenants is null)
        {
            ModelState.AddModelError(string.Empty,
                "Unable to load platform data. Please sign in again or check API availability.");
            return View(new DashboardViewModel
            {
                DisplayName = User.FindFirst("ph_display_name")?.Value ?? "Administrator",
                Summary = summary ?? new PlatformSummaryViewModel(),
                Tenants = tenants ?? new List<TenantViewModel>()
            });
        }

        return View(new DashboardViewModel
        {
            DisplayName = User.FindFirst("ph_display_name")?.Value ?? "Administrator",
            Summary = summary,
            Tenants = tenants.Take(5).ToList()
        });
    }
}
