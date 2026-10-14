using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    // The root URL is a role-aware entry point. Platform administrators belong
    // in the Control Center; tenant/branch/project users belong in their workspace.
    // Avoid showing the old placeholder page after users click "Go to home".
    [HttpGet]
    public IActionResult Index()
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Dashboard", new { area = "PlatformAdmin" });

        return RedirectToAction("Index", "Workspace", new { area = "", scopeKey = (string?)null });
    }
}
