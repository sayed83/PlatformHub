using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    // The root URL is the entry point for signed-in users. Keep platform-wide
    // administration separate from the tenant/branch/project workspace.
    public IActionResult Index()
    {
        if (User.FindFirst("ph_platform_admin")?.Value == "true")
            return RedirectToAction("Index", "Dashboard", new { area = "PlatformAdmin" });

        return RedirectToAction("Index", "Workspace");
    }
}