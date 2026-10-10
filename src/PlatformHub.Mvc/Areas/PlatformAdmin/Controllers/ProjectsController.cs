using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Areas.PlatformAdmin.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class ProjectsController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(long? tenantId, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true")
            return Forbid();

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token))
            return Challenge();

        var model = await LoadModelAsync(token, tenantId, ct);
        if (model is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Platform API is unavailable. Please verify that the API is running and try again.");

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectManagementViewModel model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true")
            return Forbid();

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token))
            return Challenge();

        if (ModelState.IsValid)
        {
            var created = await api.PostAsync<ProjectRowViewModel>(
                "api/v1/platform/projects",
                new { model.TenantId, model.Code, model.Name, model.BaseUrl },
                ct,
                token);

            if (created is not null)
            {
                TempData["SuccessMessage"] = $"Project '{created.Name}' was created successfully.";
                return RedirectToAction(nameof(Index), new { tenantId = model.TenantId });
            }

            ModelState.AddModelError(string.Empty,
                "Project could not be created. Check that the tenant is active and the project code is unique within that tenant.");
        }

        var current = await LoadModelAsync(token, model.TenantId, ct);
        if (current is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Unable to reload project data from the Platform API.");

        current.TenantId = model.TenantId;
        current.Code = model.Code;
        current.Name = model.Name;
        current.BaseUrl = model.BaseUrl;
        return View("Index", current);
    }

    private async Task<ProjectManagementViewModel?> LoadModelAsync(
        string token, long? tenantId, CancellationToken ct)
    {
        var tenants = await api.GetAsync<List<TenantViewModel>>("api/v1/platform/tenants", token, ct);
        if (tenants is null)
            return null;

        var query = tenantId.HasValue ? $"?tenantId={tenantId.Value}" : string.Empty;
        var projects = await api.GetAsync<List<ProjectRowViewModel>>(
            $"api/v1/platform/projects{query}", token, ct);

        if (projects is null)
            return null;

        return new ProjectManagementViewModel
        {
            Tenants = tenants,
            Projects = projects,
            TenantId = tenantId ?? 0
        };
    }
}
