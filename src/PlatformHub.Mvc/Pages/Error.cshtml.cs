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
    public string? TechnicalDetails { get; private set; }
    public string Title { get; private set; } = "Something went wrong";
    public string Description { get; private set; } = "PlatformHub could not complete this request.";
    public string Explanation { get; private set; } = "An unexpected error occurred while processing your request.";
    public IReadOnlyList<string> SuggestedActions { get; private set; } = Array.Empty<string>();

    public void OnGet(int? statusCode)
    {
        StatusCode = statusCode ?? 500;
        TraceId = HttpContext.TraceIdentifier;
        RequestPath = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path
            ?? HttpContext.Request.Path.Value
            ?? string.Empty;
        RequestMethod = HttpContext.Request.Method;
        QueryString = HttpContext.Request.QueryString.HasValue ? HttpContext.Request.QueryString.Value! : string.Empty;
        EnvironmentName = environment.EnvironmentName;
        var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (environment.IsDevelopment() && exception is not null)
            TechnicalDetails = $"{exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}";

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
