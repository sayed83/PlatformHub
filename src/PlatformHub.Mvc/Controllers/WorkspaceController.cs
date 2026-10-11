using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlatformHub.Mvc.Controllers;

[Authorize]
public sealed class WorkspaceController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
