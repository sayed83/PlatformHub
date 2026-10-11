using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class MenuAccessController(PlatformApiClient api) : Controller
{
    public IActionResult Index() =>
        User.FindFirst("ph_platform_admin")?.Value == "true" ? View() : Forbid();

    [HttpGet]
    public async Task<IActionResult> Data(string kind, long projectId, long selectionId = 0, CancellationToken ct = default)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = Token();
        if (token is null) return Unauthorized();
        var path = kind switch
        {
            "projects" => "api/v1/menu-access/projects",
            "lookups" => $"api/v1/menu-access/lookups?projectId={projectId}",
            "role" => $"api/v1/menu-access/role?projectId={projectId}&roleId={selectionId}",
            "user" => $"api/v1/menu-access/user?projectId={projectId}&userId={selectionId}",
            _ => null
        };
        if (path is null) return BadRequest(new { message = "Unknown data request." });
        var data = await api.GetAsync<object>(path, token, ct);
        return data is null ? StatusCode(502, new { message = "Platform API request failed." }) : Json(data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRole([FromBody] SaveRoleRequest model, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = Token();
        if (token is null) return Unauthorized();
        var (data, error) = await api.PostWithErrorAsync<object>("api/v1/menu-access/role", model, ct, token);
        return data is null ? BadRequest(new { message = error ?? "Could not save role access." }) : Json(data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser([FromBody] SaveUserRequest model, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();
        var token = Token();
        if (token is null) return Unauthorized();
        var (data, error) = await api.PostWithErrorAsync<object>("api/v1/menu-access/user", model, ct, token);
        return data is null ? BadRequest(new { message = error ?? "Could not save user overrides." }) : Json(data);
    }

    private bool IsPlatformAdmin() => User.FindFirst("ph_platform_admin")?.Value == "true";
    private string? Token() => User.FindFirst("ph_api_access_token")?.Value;
    public sealed record RoleItem(long MenuItemId, bool IsAllowed);
    public sealed record SaveRoleRequest(long ProjectId, long RoleId, List<RoleItem> Items);
    public sealed record UserItem(long MenuItemId, string Decision);
    public sealed record SaveUserRequest(long ProjectId, long UserId, List<UserItem> Items);
}
