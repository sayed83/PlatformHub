namespace PlatformHub.Mvc.Services;

/// <summary>Adds the server-authenticated active workspace to eligible API calls.</summary>
public sealed class ActiveTenantScopeHeaderHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext;
        var path = request.RequestUri?.AbsolutePath ?? request.RequestUri?.OriginalString ?? string.Empty;

        // Scope discovery and authentication must work before a workspace is selected.
        var excluded = path.Contains("api/v1/auth/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("api/v1/platform-bootstrap/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("api/v1/platform/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("api/v1/workspace/scopes", StringComparison.OrdinalIgnoreCase);

        if (!excluded && context?.User.Identity?.IsAuthenticated == true)
        {
            AddClaimHeader(request, "ph_active_tenant_id", "X-Platform-TenantId");
            AddClaimHeader(request, "ph_active_branch_id", "X-Platform-BranchId");
            AddClaimHeader(request, "ph_active_project_id", "X-Platform-ProjectId");
        }

        return base.SendAsync(request, cancellationToken);
    }

    private void AddClaimHeader(HttpRequestMessage request, string claimType, string headerName)
    {
        var value = accessor.HttpContext?.User.FindFirst(claimType)?.Value;
        if (!string.IsNullOrWhiteSpace(value))
            request.Headers.TryAddWithoutValidation(headerName, value);
    }
}
