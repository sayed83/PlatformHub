using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class MenuManagementController(PlatformApiClient api) : Controller
{
    public IActionResult Index() =>
        User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN") ? View() : Forbid();

    [HttpGet]
    public async Task<IActionResult> Projects(CancellationToken ct)
    {
        if (!User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN")) return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await api.GetWithStatusAsync<System.Text.Json.JsonElement?>("api/v1/project-menus/projects", token, ct);
        if (result.StatusCode == 401) return Unauthorized(new { message = "Platform API session expired. Please sign in again." });
        if (result.Value is null) return StatusCode(result.StatusCode ?? 502, new { message = result.Error ?? "Could not load projects from Platform API." });
        return new JsonResult(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Data(string kind, long projectId, long? moduleId, bool standaloneOnly, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();

        var path = kind == "modules"
            ? $"api/v1/project-menus/modules?projectId={projectId}"
            : $"api/v1/project-menus/items?projectId={projectId}" +
              (standaloneOnly ? "&standaloneOnly=true" : moduleId.HasValue ? $"&moduleId={moduleId.Value}" : string.Empty);

        var result = await api.GetWithStatusAsync<System.Text.Json.JsonElement?>(path, token, ct);
        if (result.StatusCode == 401) return Unauthorized(new { message = "Platform API session expired. Please sign in again." });
        if (result.Value is null) return StatusCode(result.StatusCode ?? 502, new { message = result.Error ?? "Could not load data from Platform API." });
        return new JsonResult(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> NextSortOrder(long projectId, long? moduleId, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var path = $"api/v1/project-menus/next-sort-order?projectId={projectId}&moduleId={moduleId?.ToString() ?? ""}";
        var result = await api.GetWithStatusAsync<System.Text.Json.JsonElement?>(path, token, ct);
        if (result.StatusCode == 401) return Unauthorized(new { message = "Platform API session expired. Please sign in again." });
        if (result.Value is null) return StatusCode(result.StatusCode ?? 502, new { message = result.Error ?? "Could not calculate menu sort order." });
        return new JsonResult(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Summary(long projectId, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await api.GetWithStatusAsync<System.Text.Json.JsonElement?>($"api/v1/project-menus/summary?projectId={projectId}", token, ct);
        if (result.StatusCode == 401) return Unauthorized(new { message = "Platform API session expired. Please sign in again." });
        if (result.Value is null) return StatusCode(result.StatusCode ?? 502, new { message = result.Error ?? "Could not load project summary." });
        return new JsonResult(result.Value);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveModule([FromBody] SaveModuleRequest model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var (result, error) = await api.PostWithErrorAsync<object>("api/v1/project-menus/modules", model, ct, token);
        if (result is not null) return Json(result);
        if (IsApiUnauthorized(error))
            return Unauthorized(new { message = "Your Platform API session has expired. Please sign in again." });
        return BadRequest(new { message = error ?? "Unable to save module." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMenu([FromBody] SaveMenuRequest model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var (result, error) = await api.PostWithErrorAsync<object>("api/v1/project-menus/items", model, ct, token);
        if (result is not null) return Json(result);
        if (IsApiUnauthorized(error))
            return Unauthorized(new { message = "Your Platform API session has expired. Please sign in again." });
        return BadRequest(new { message = error ?? "Unable to save menu." });
    }

    [HttpGet]
    public async Task<IActionResult> HardDeleteDetails(string itemType, long itemId, long projectId, string? returnUrl, CancellationToken ct)
    {
        if (!User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN")) return Forbid();
        if (itemType is not ("module" or "menu") || itemId <= 0 || projectId <= 0) return BadRequest();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var endpoint = itemType == "module"
            ? $"api/v1/project-menus/modules/{itemId}/hard-delete-details?projectId={projectId}"
            : $"api/v1/project-menus/items/{itemId}/hard-delete-details?projectId={projectId}";
        var result = await api.GetWithStatusAsync<System.Text.Json.JsonElement?>(endpoint, token, ct);
        if (result.StatusCode == 401) return Unauthorized();
        if (result.Value is null) return StatusCode(result.StatusCode ?? 502, result.Error ?? "Unable to load related records.");
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action(nameof(Index));
        ViewData["ItemType"] = itemType;
        ViewData["ItemId"] = itemId;
        ViewData["ProjectId"] = projectId;
        return View("HardDeleteDetails", result.Value.Value);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmHardDelete(string itemType, long itemId, long projectId, string? returnUrl, CancellationToken ct)
    {
        if (!User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN")) return Forbid();
        if (itemType is not ("module" or "menu") || itemId <= 0 || projectId <= 0) return BadRequest();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var endpoint = itemType == "module"
            ? $"api/v1/project-menus/modules/{itemId}/delete"
            : $"api/v1/project-menus/items/{itemId}/delete";
        var (data, error) = await api.PostWithErrorAsync<System.Text.Json.JsonElement>(
            endpoint, new { projectId, deleteType = "hard" }, ct, token);
        if (data is not null)
        {
            TempData["SuccessMessage"] = "The " + itemType + " was permanently deleted.";
            return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!);
        }
        TempData["ErrorMessage"] = error ?? "Hard delete failed. Related records may still exist.";
        return RedirectToAction(nameof(HardDeleteDetails), new { itemType, itemId, projectId, returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteModule(long moduleId, long projectId, string deleteType, CancellationToken ct)
    {
        if (!User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN")) return Forbid();
        if (moduleId <= 0 || projectId <= 0 || deleteType is not ("soft" or "hard"))
            return BadRequest(new { message = "A valid project, module and delete type are required." });
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var (data, error) = await api.PostWithErrorAsync<System.Text.Json.JsonElement?>(
            $"api/v1/project-menus/modules/{moduleId}/delete",
            new { projectId, deleteType }, ct, token);
        if (data is not null) return Json(data);
        if (IsApiUnauthorized(error)) return Unauthorized(new { message = "Your Platform API session has expired. Please sign in again." });
        return BadRequest(new { message = error ?? "Unable to delete module." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMenu(long menuItemId, long projectId, string deleteType, CancellationToken ct)
    {
        if (!User.HasClaim("ph_role", "PLATFORM_SUPER_ADMIN")) return Forbid();
        if (menuItemId <= 0 || projectId <= 0 || deleteType is not ("soft" or "hard"))
            return BadRequest(new { message = "A valid project, menu and delete type are required." });
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var (data, error) = await api.PostWithErrorAsync<System.Text.Json.JsonElement?>(
            $"api/v1/project-menus/items/{menuItemId}/delete",
            new { projectId, deleteType }, ct, token);
        if (data is not null) return Json(data);
        if (IsApiUnauthorized(error)) return Unauthorized(new { message = "Your Platform API session has expired. Please sign in again." });
        return BadRequest(new { message = error ?? "Unable to delete menu." });
    }

    private static bool IsApiUnauthorized(string? error) =>
        error?.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase) == true ||
        error?.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase) == true;

    public sealed record SaveModuleRequest(long? Id, long ProjectId, string Code, string Name, string? Description, string? Icon, int SortOrder, bool IsActive);
    public sealed record SaveMenuRequest(long? Id, long ProjectId, long? ModuleId, long? ParentMenuId, string Code, string Name, string? Area, string? Controller, string? Action, string? RouteUrl, string? Icon, int SortOrder, bool IsExternal, bool IsActive);
}
