using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
