using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformHub.Mvc.Areas.PlatformAdmin.Models;
using PlatformHub.Mvc.Services;

namespace PlatformHub.Mvc.Areas.PlatformAdmin.Controllers;

[Area("PlatformAdmin")]
[Authorize]
public sealed class BranchesController(PlatformApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(long tenantId, long? highlightBranchId, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        if (tenantId <= 0) return BadRequest("A valid tenant is required.");
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();

        var tenants = await api.GetAsync<List<TenantViewModel>>("api/v1/platform/tenants", token, ct);
        var tenant = tenants?.SingleOrDefault(x => x.Id == tenantId);
        if (tenant is null) return NotFound("Tenant was not found.");

        var branches = await api.GetAsync<List<BranchRowViewModel>>(
            $"api/v1/platform/branches?tenantId={tenantId}", token, ct);
        if (branches is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Unable to load branches from the Platform API.");

        var assignments = new List<BranchRoleAssignmentViewModel>();
        foreach (var branch in branches)
        {
            var branchAssignments = await api.GetAsync<List<BranchRoleAssignmentViewModel>>(
                $"api/v1/platform/access/branches/{branch.Id}/assignments", token, ct);
            if (branchAssignments is not null) assignments.AddRange(branchAssignments);
        }

        ViewData["HighlightBranchId"] = highlightBranchId;
        return View(new BranchManagementViewModel
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Branches = branches,
            RoleAssignments = assignments
        });
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, long tenantId, string code, string name, string? address, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/branches/{id}/update", new { code, name, address }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null ? (error ?? "Branch update failed.") : "Branch updated successfully.";
        return RedirectToAction(nameof(Index), new { tenantId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, long tenantId, bool hardDelete, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/branches/{id}/delete", new { hardDelete }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
            ? (error ?? (hardDelete ? "Branch hard delete failed." : "Branch deactivation failed."))
            : (hardDelete ? "Branch permanently deleted." : "Branch deactivated successfully.");
        return RedirectToAction(nameof(Index), new { tenantId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(long id, long tenantId, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        var (result, error) = await api.PostWithErrorAsync<object>($"api/v1/platform/branches/{id}/activate", new { }, ct, token);
        TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
            ? (error ?? "Branch activation failed.")
            : "Branch activated successfully.";
        return RedirectToAction(nameof(Index), new { tenantId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BranchManagementViewModel model, CancellationToken ct)
    {
        if (User.FindFirst("ph_platform_admin")?.Value != "true") return Forbid();
        var token = User.FindFirst("ph_api_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token)) return Challenge();
        if (model.TenantId <= 0) return BadRequest("A valid tenant is required.");

        var tenants = await api.GetAsync<List<TenantViewModel>>("api/v1/platform/tenants", token, ct);
        var tenant = tenants?.SingleOrDefault(x => x.Id == model.TenantId);
        if (tenant is null) return NotFound("Tenant was not found.");

        if (ModelState.IsValid)
        {
            var created = await api.PostAsync<BranchRowViewModel>(
                "api/v1/platform/branches",
                new { model.TenantId, model.Code, model.Name, model.Address },
                ct,
                token);
            if (created is not null)
            {
                TempData["SuccessMessage"] = $"Branch '{created.Name}' created successfully.";
                return RedirectToAction(nameof(Index), new { tenantId = model.TenantId });
            }
            ModelState.AddModelError(string.Empty,
                "Branch could not be created. Verify the tenant is active and the branch code is unique within this tenant.");
        }

        var branches = await api.GetAsync<List<BranchRowViewModel>>(
            $"api/v1/platform/branches?tenantId={model.TenantId}", token, ct) ?? new();
        return View("Index", new BranchManagementViewModel
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Branches = branches,
            Code = model.Code,
            Name = model.Name,
            Address = model.Address
        });
    }
}
