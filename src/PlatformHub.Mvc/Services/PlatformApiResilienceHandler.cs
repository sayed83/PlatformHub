using System.Net;

namespace PlatformHub.Mvc.Services;

/// <summary>
/// Adds correlation IDs to MVC-to-API requests and retries only safe GET requests
/// for transient transport/server failures. Mutating requests are never retried.
/// </summary>
public sealed class PlatformApiResilienceHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<PlatformApiResilienceHandler> logger) : DelegatingHandler
{
    private const int MaxRetries = 2;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(correlationId) &&
            !request.Headers.Contains("X-Correlation-ID"))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        }

        if (request.Method != HttpMethod.Get)
            return await base.SendAsync(request, cancellationToken);

        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                // The first attempt can use the original request. A retry gets a fresh GET message.
                var currentRequest = attempt == 0 ? request : CloneGetRequest(request);
                response = await base.SendAsync(currentRequest, cancellationToken);

                if (attempt >= MaxRetries || !IsTransient(response.StatusCode))
                    return response;

                var statusCode = (int)response.StatusCode;
                response.Dispose();
                response = null;

                logger.LogWarning(
                    "Transient Platform API GET failure. Path={Path}, StatusCode={StatusCode}, Attempt={Attempt}, CorrelationId={CorrelationId}",
                    request.RequestUri?.PathAndQuery, statusCode, attempt + 1, correlationId);

                await Task.Delay(DelayForAttempt(attempt), cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
            {
                response?.Dispose();
                logger.LogWarning(ex,
                    "Platform API GET transport failure. Path={Path}, Attempt={Attempt}, CorrelationId={CorrelationId}",
                    request.RequestUri?.PathAndQuery, attempt + 1, correlationId);
                await Task.Delay(DelayForAttempt(attempt), cancellationToken);
            }
        }
    }

    private static HttpRequestMessage CloneGetRequest(HttpRequestMessage source)
    {
        var clone = new HttpRequestMessage(HttpMethod.Get, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };

        foreach (var header in source.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static TimeSpan DelayForAttempt(int attempt) =>
        TimeSpan.FromMilliseconds(attempt == 0 ? 200 : 500);
}
