using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Areas.PlatformAdmin.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class TenantsController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token))
            return Challenge();

        var tenantResult = await api.GetWithStatusAsync<List<TenantViewModel>>(
            "api/v1/platform/tenants", token, ct);
        if (tenantResult.Value is null)
        {
            // Do not disguise an API outage as an empty tenant directory.
            // An empty HTTP response lets UseStatusCodePagesWithReExecute render /Error/{statusCode}.
            var statusCode = tenantResult.StatusCode is >= 400 and <= 599
                ? tenantResult.StatusCode.Value
                : StatusCodes.Status503ServiceUnavailable;
            TempData["ErrorMessage"] = tenantResult.Error
                ?? "The Platform API did not return tenant data. Check the API logs and Api:BaseUrl setting.";
            return StatusCode(statusCode);
        }
        var tenants = tenantResult.Value;

        // Tenant viewers get a separate read-only directory, not the Platform Admin
        // management dashboard. This avoids presenting actions they cannot perform.
        var isPlatformAdmin = User.FindFirst("ph_platform_admin")?.Value == "true";
        if (!isPlatformAdmin)
        {
            if (!User.HasClaim("ph_permission", "Tenant.View"))
                return Forbid();

            return View("ReadOnly", new TenantManagementViewModel
            {
                Tenants = tenants
            });
        }

        // Load the project catalogue for the directory, then tenant-specific assignments
        // so the counts/details reflect TenantProjects rather than legacy Projects.TenantId.
        var projects = await api.GetAsync<List<ProjectRowViewModel>>("api/v1/platform/projects", token, ct) ?? new();
        var tenantProjects = new List<ProjectRowViewModel>();
        var eligible = new HashSet<long>();
        foreach (var tenant in tenants)
        {
            var assigned = await api.GetAsync<List<ProjectRowViewModel>>(
                $"api/v1/platform/projects?tenantId={tenant.Id}", token, ct) ?? new();
            tenantProjects.AddRange(assigned);

            var branches = await api.GetAsync<List<BranchRowViewModel>>($"api/v1/platform/branches?tenantId={tenant.Id}", token, ct);
            if ((branches?.Count ?? 0) == 0 && assigned.Count == 0)
                eligible.Add(tenant.Id);
        }
        // Keep the global catalogue plus any tenant-specific projections for tenant detail cards.
        return View(new TenantManagementViewModel
        {
            Tenants = tenants,
            Projects = projects.Concat(tenantProjects).DistinctBy(p => (p.Id, p.TenantId)).ToList(),
            CanHardDeleteTenantIds = eligible
        });
    }


    [HttpGet]
    public async Task<IActionResult> Projects(long id, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        var data = await api.GetAsync<TenantProjectAssignmentResponse>(
            $"api/v1/platform/tenants/{id}/projects", token, ct);
        if (data is null)
        {
            TempData["ErrorMessage"] = "Unable to load tenant project assignments. Check the Platform API and permissions.";
            return RedirectToAction(nameof(Index));
        }

        return View("Projects", new TenantProjectAssignmentViewModel
        {
            TenantId = data.Tenant.Id,
            TenantCode = data.Tenant.Code,
            TenantName = data.Tenant.Name,
            SelectedProjectIds = data.AssignedProjectIds,
            Projects = data.Projects
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignProjects(long id, List<long> projectIds, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        var result = await api.PutAsync<AssignTenantProjectsResult>(
            $"api/v1/platform/tenants/{id}/projects",
            new { projectIds = projectIds.Distinct().ToArray() }, ct, token);

        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
            ? "Project assignments could not be saved. Check the API logs and selected projects."
            : $"Project assignments saved successfully ({result.AssignedCount} project(s)).";
        return RedirectToAction(nameof(Projects), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Dependencies(long id, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var details = await api.GetAsync<TenantDependencyViewModel>($"api/v1/platform/access/tenants/{id}/dependencies", token, ct);
        if (details is null)
        {
            TempData["ErrorMessage"] = "Unable to load tenant dependencies. Check your Tenant.View permission and API logs.";
            return RedirectToAction(nameof(Index));
        }
        return View("Dependencies", details);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, string code, string name, CancellationToken ct)
    {
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/tenants/{id}/update", new { code, name }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null ? (error ?? "Tenant update failed.") : "Tenant updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, bool hardDelete, CancellationToken ct)
    {
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/tenants/{id}/delete", new { hardDelete }, ct, token);
        if (result is null && hardDelete)
        {
            TempData["ErrorMessage"] = error ?? "Tenant hard delete failed. Review its dependencies.";
            return RedirectToAction(nameof(Dependencies), new { id });
        }
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
            ? (error ?? "Tenant deactivation failed.")
            : (hardDelete ? "Tenant permanently deleted." : "Tenant deactivated successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
    {
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/tenants/{id}/activate", new { }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
            ? (error ?? "Tenant activation failed.")
            : "Tenant activated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TenantManagementViewModel model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true")
            return Forbid();

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token))
            return Challenge();

        if (!ModelState.IsValid)
        {
            var current = await api.GetAsync<List<TenantViewModel>>("api/v1/platform/tenants", token, ct);
            model = new TenantManagementViewModel
            {
                Code = model.Code,
                Name = model.Name,
                Tenants = current ?? new List<TenantViewModel>()
            };
            return View("Index", model);
        }

        var created = await api.PostAsync<TenantViewModel>(
            "api/v1/platform/tenants",
            new { model.Code, model.Name },
            ct,
            token);

        if (created is null)
        {
            ModelState.AddModelError(string.Empty,
                "Tenant could not be created. The code may already exist, or the API rejected the request.");
            var current = await api.GetAsync<List<TenantViewModel>>("api/v1/platform/tenants", token, ct);
            return View("Index", new TenantManagementViewModel
            {
                Code = model.Code,
                Name = model.Name,
                Tenants = current ?? new List<TenantViewModel>()
            });
        }

        TempData["SuccessMessage"] = $"Tenant '{created.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }
}

public sealed class AssignTenantProjectsResult { public string Message { get; set; } = string.Empty; public long TenantId { get; set; } public int AssignedCount { get; set; } }
