using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class MenuManagementController(PlatformApiClient api) : Controller
{
    public IActionResult Index() =>
        User.FindFirst("ph_platform_admin")?.Value == "true" ? View() : Forbid();

    [HttpGet]
    public async Task<IActionResult> Projects(CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await api.GetWithStatusAsync<System.Text.Json.JsonElement>("api/v1/project-menus/projects", token, ct);
        if (result.StatusCode == 401) return Unauthorized(new { message = "Platform API session expired. Please sign in again." });
        if (result.Value is null) return StatusCode(result.StatusCode ?? 502, new { message = result.Error ?? "Could not load projects from Platform API." });
        return new JsonResult(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Data(string kind, long projectId, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var path = kind == "modules" ? $"api/v1/project-menus/modules?projectId={projectId}"
            : $"api/v1/project-menus/items?projectId={projectId}";
        var result = await api.GetWithStatusAsync<System.Text.Json.JsonElement>(path, token, ct);
        if (result.StatusCode == 401) return Unauthorized(new { message = "Platform API session expired. Please sign in again." });
        if (result.Value is null) return StatusCode(result.StatusCode ?? 502, new { message = result.Error ?? "Could not load data from Platform API." });
        return new JsonResult(result.Value);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveModule([FromBody] SaveModuleRequest model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var (result, error) = await api.PostWithErrorAsync<object>("api/v1/project-menus/modules", model, ct, token);
        return result is null ? BadRequest(new { message = error ?? "Unable to save module." }) : Json(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMenu([FromBody] SaveMenuRequest model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var (result, error) = await api.PostWithErrorAsync<object>("api/v1/project-menus/items", model, ct, token);
        return result is null ? BadRequest(new { message = error ?? "Unable to save menu." }) : Json(result);
    }

    public sealed record SaveModuleRequest(long? Id, long ProjectId, string Code, string Name, string? Description, string? Icon, int SortOrder, bool IsActive);
    public sealed record SaveMenuRequest(long? Id, long ProjectId, long? ModuleId, long? ParentMenuId, string Code, string Name, string? Area, string? Controller, string? Action, string? RouteUrl, string? Icon, int SortOrder, bool IsExternal, bool IsActive);
}
