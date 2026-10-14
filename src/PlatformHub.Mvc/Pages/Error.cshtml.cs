using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PlatformHub.Mvc.Pages;

[IgnoreAntiforgeryToken]
public sealed class ErrorModel(ILogger<ErrorModel> logger, IWebHostEnvironment environment) : PageModel
{
    public int StatusCode { get; private set; } = 500;
    public string TraceId { get; private set; } = string.Empty;
    [TempData] public string? ErrorMessage { get; set; }
    public string RequestPath { get; private set; } = string.Empty;
    public string RequestMethod { get; private set; } = string.Empty;
    public string QueryString { get; private set; } = string.Empty;
    public string EnvironmentName { get; private set; } = string.Empty;
    public string LoginUrl { get; private set; } = "/Account/Login";
    public string LoginButtonText { get; private set; } = "User panel login";
    public string LogoutUrl { get; private set; } = "/Account/Logout";
    public string? TechnicalDetails { get; private set; }
    public string Title { get; private set; } = "Something went wrong";
    public string Description { get; private set; } = "PlatformHub could not complete this request.";
    public string Explanation { get; private set; } = "An unexpected error occurred while processing your request.";
    public IReadOnlyList<string> SuggestedActions { get; private set; } = Array.Empty<string>();

    public async Task OnGetAsync(int? statusCode)
    {
        StatusCode = statusCode ?? 500;
        TraceId = HttpContext.TraceIdentifier;
        var reExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        RequestPath = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path
            ?? reExecuteFeature?.OriginalPath
            ?? HttpContext.Request.Path.Value
            ?? string.Empty;
        RequestMethod = HttpContext.Request.Method;
        QueryString = HttpContext.Request.QueryString.HasValue ? HttpContext.Request.QueryString.Value! : string.Empty;
        EnvironmentName = environment.EnvironmentName;

        // Route back to the login page belonging to the panel where the request originated.
        var originalPath = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()?.OriginalPath
            ?? RequestPath;
        // The authenticated panel claim is authoritative. Workspace menu items may still
        // point to a legacy /PlatformAdmin route, which must not change the login panel.
        var isPlatformAdminPanel = HttpContext.User.Identity?.IsAuthenticated == true
            ? HttpContext.User.FindFirst("ph_platform_admin")?.Value == "true"
            : originalPath.StartsWith("/PlatformAdmin", StringComparison.OrdinalIgnoreCase);
        LoginUrl = isPlatformAdminPanel ? "/PlatformAdmin/Account/Login" : "/Account/Login";
        LogoutUrl = isPlatformAdminPanel ? "/PlatformAdmin/Account/Logout" : "/Account/Logout";

        // If a signed-in workspace request fails because the API is unavailable, clear the
        // workspace cookie and return to the user login screen instead of rendering Error Center
        // inside the authenticated workspace layout. Platform Admin keeps its own panel behavior.
        if ((StatusCode == StatusCodes.Status502BadGateway || StatusCode == StatusCodes.Status503ServiceUnavailable)
            && HttpContext.User.Identity?.IsAuthenticated == true
            && !isPlatformAdminPanel)
        {
            await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Redirect("/Account/Login");
            return;
        }

        // Authentication expiry should send the user to sign-in; other failures stay on Error Center.
        if (StatusCode == StatusCodes.Status401Unauthorized)
        {
            Response.Redirect(LoginUrl);
            return;
        }
        LoginButtonText = isPlatformAdminPanel ? "Platform Admin login" : "User panel login";
        var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (environment.IsDevelopment())
        {
            var capturedApiDetails = HttpContext.Items["PlatformHub.ErrorDetails"] as string;
            if (exception is not null)
                TechnicalDetails = $"{exception.GetType().FullName}: {exception.Message}\n\n{exception}";
            else if (!string.IsNullOrWhiteSpace(capturedApiDetails))
                TechnicalDetails = capturedApiDetails;
            else
                TechnicalDetails = StatusCode >= 500
                    ? "No exception object was captured for this response. Use the reference ID to find the corresponding MVC/API log entry. For API connection errors, verify Api:BaseUrl and confirm the API is running."
                    : $"No exception object was captured for this HTTP {StatusCode} response. The original request path is '{RequestPath}'. Check the route, endpoint mapping, and resource identifier.";
        }

        if (StatusCode is < 400 or > 599) StatusCode = 500;

        (Title, Description, Explanation, SuggestedActions) = StatusCode switch
        {
            400 => ("Invalid request", "The server could not understand the request.", "Some submitted information may be missing or invalid.",
                new[] { "Check the information you entered and try again.", "If this happened while saving, verify required fields and selected project/module." }),
            401 => ("Sign-in required", "Your session may have expired or the request was not authenticated.", "Sign in again to refresh your session and retry the operation.",
                new[] { "Sign out and sign in again.", "If this continues, ask an administrator to check API token generation and authentication settings." }),
            403 => ("Access denied", "Your account does not have permission to open this resource.", "The request was understood, but access is not allowed for the current account.",
                new[] { "Sign in with the correct account.", "Ask a Platform Administrator to verify your role and permissions." }),
            404 => ("Page not found", "The requested page or resource could not be found.", "The URL may be incorrect, or the resource may have been removed.",
                new[] { "Check the URL and try again.", "Return to the previous page and open the resource from the menu." }),
            409 => ("Conflict", "The requested change conflicts with the current state of the resource.", "The item may have changed, already exist, or still be referenced by another record.",
                new[] { "Refresh the page and review the latest data.", "Check for duplicate codes or dependent records before trying again." }),
            422 => ("Validation failed", "The submitted information could not be accepted.", "One or more fields may violate a business rule.",
                new[] { "Review the validation message and required fields.", "Confirm that the selected tenant, branch, project, and role belong together." }),
            429 => ("Too many requests", "PlatformHub is receiving requests too quickly.", "A rate limit or temporary service protection has been reached.",
                new[] { "Wait briefly before retrying.", "Avoid repeatedly submitting the same operation." }),
            408 or 504 => ("Request timed out", "The request took too long to complete.", "The API or database may be busy or temporarily unreachable.",
                new[] { "Try again in a moment.", "Confirm PlatformHub.Api is running and its configured URL is reachable." }),
            502 or 503 => ("Service temporarily unavailable", "PlatformHub could not get a valid response from a required service.", "The API may be stopped, unreachable, or starting up.",
                new[] { "Confirm PlatformHub.Api is running.", "Check the Api:BaseUrl setting in PlatformHub.Mvc configuration.", "Review the API and MVC application logs using the reference ID above." }),
            _ => ("Unexpected application error", "PlatformHub encountered an unexpected problem.", "The request failed unexpectedly. The detailed exception is recorded in the server logs.",
                new[] { "Try the operation again.", "Check that PlatformHub.Api and SQL Server are running and reachable.", "Review application logs using the reference ID above." })
        };

        if (StatusCode >= 500)
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error is not null)
                logger.LogError(exceptionFeature.Error, "Unhandled MVC error. TraceId={TraceId}, Path={Path}", TraceId, RequestPath);
            else
                logger.LogWarning("HTTP {StatusCode} returned. TraceId={TraceId}, Path={Path}", StatusCode, TraceId, RequestPath);
        }
    }
}
