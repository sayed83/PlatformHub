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
        if (User.FindFirst("ph_platform_admin")?.Value != "true")
            return Forbid();

        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token))
            return Challenge();

        var tenants = await api.GetAsync<List<TenantViewModel>>("api/v1/platform/tenants", token, ct);
        if (tenants is null)
        {
            ModelState.AddModelError(string.Empty, "Unable to load tenants from the Platform API.");
            tenants = new List<TenantViewModel>();
        }

        var projects = await api.GetAsync<List<ProjectRowViewModel>>("api/v1/platform/projects", token, ct) ?? new();
        var eligible = new HashSet<long>();
        foreach (var tenant in tenants)
        {
            var branches = await api.GetAsync<List<BranchRowViewModel>>($"api/v1/platform/branches?tenantId={tenant.Id}", token, ct);
            if ((branches?.Count ?? 0) == 0 && !projects.Any(p => p.TenantId == tenant.Id))
                eligible.Add(tenant.Id);
        }
        return View(new TenantManagementViewModel { Tenants = tenants, CanHardDeleteTenantIds = eligible });
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, string code, string name, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/tenants/{id}/update", new { code, name }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null ? (error ?? "Tenant update failed.") : "Tenant updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, bool hardDelete, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/tenants/{id}/delete", new { hardDelete }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
            ? (error ?? (hardDelete ? "Tenant hard delete failed." : "Tenant deactivation failed."))
            : (hardDelete ? "Tenant permanently deleted." : "Tenant deactivated successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
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
