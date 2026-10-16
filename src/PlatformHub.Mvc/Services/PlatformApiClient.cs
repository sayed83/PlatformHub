using System.Net;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PlatformHub.Mvc.Models;
using System.Text;
using System.Text.Json;

namespace PlatformHub.Mvc.Services;

public sealed class PlatformApiClient(IHttpClientFactory factory, ILogger<PlatformApiClient> logger, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // MVC can issue several API calls concurrently when an access token expires.
    // Serialize refreshes and cache the rotation briefly so sibling requests using
    // the same old cookie can reuse the newly rotated tokens instead of rendering
    // empty data after an "already been rotated" 401.
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);
    private static readonly ConcurrentDictionary<string, CachedApiTokens> RotatedTokens = new(StringComparer.Ordinal);
    private sealed record CachedApiTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

    public async Task<(T? Value, string? Error)> PostWithErrorAsync<T>(
        string path, object body, CancellationToken ct = default, string? token = null)
    {
        var client = factory.CreateClient("PlatformApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await SendWithRefreshAsync(client, request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Platform API POST {Path} failed with HTTP {StatusCode}. Response body omitted",
                    path, (int)response.StatusCode);
                var message = TryReadApiMessage(responseBody) ?? $"Platform API returned HTTP {(int)response.StatusCode}.";
                return (default, message);
            }
            if (string.IsNullOrWhiteSpace(responseBody)) return (default, null);
            return (JsonSerializer.Deserialize<T>(responseBody, JsonOptions), null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not connect to Platform API for POST {Path}", path);
            return (default, "Could not connect to the Platform API.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Platform API POST {Path} timed out", path);
            return (default, "The Platform API request timed out.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Platform API returned invalid JSON for POST {Path}", path);
            return (default, "The Platform API returned an unexpected response.");
        }
    }

    private static string? TryReadApiMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var name in new[] { "message", "detail", "title", "error" })
                if (document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
        }
        catch (JsonException) { }
        // Do not surface arbitrary API payloads, which may contain stack traces or sensitive data.
        return null;
    }

    public async Task<T?> PostAsync<T>(
        string path,
        object body,
        CancellationToken ct = default,
        string? token = null)
    {
        var client = factory.CreateClient("PlatformApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await SendWithRefreshAsync(client, request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Platform API POST {Path} failed with HTTP {StatusCode}. Response body omitted",
                    path,
                    (int)response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not connect to Platform API for POST {Path}", path);
            return default;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Platform API POST {Path} timed out", path);
            return default;
        }
    }

    public async Task<T?> PutAsync<T>(
        string path,
        object body,
        CancellationToken ct = default,
        string? token = null)
    {
        var client = factory.CreateClient("PlatformApi");
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await SendWithRefreshAsync(client, request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Platform API PUT {Path} failed with HTTP {StatusCode}. Response body omitted",
                    path, (int)response.StatusCode);
                return default;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not connect to Platform API for PUT {Path}", path);
            return default;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Platform API PUT {Path} timed out", path);
            return default;
        }
    }


    public async Task<(T? Value, int? StatusCode, string? Error)> GetWithStatusAsync<T>(
        string path, string? token = null, CancellationToken ct = default)
    {
        var client = factory.CreateClient("PlatformApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await SendWithRefreshAsync(client, request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var apiMessage = TryReadApiMessage(body) ?? $"Platform API returned HTTP {(int)response.StatusCode}.";
                logger.LogWarning("Platform API GET {Path} failed with HTTP {StatusCode}. Error={Error}",
                    path, (int)response.StatusCode, apiMessage);
                SetDevelopmentErrorDetails($"API request: GET {path}\nHTTP status: {(int)response.StatusCode} ({response.ReasonPhrase})\nAPI message: {apiMessage}");
                return (default, (int)response.StatusCode, apiMessage);
            }

            if (string.IsNullOrWhiteSpace(body))
                return (default, (int)response.StatusCode, "Platform API returned an empty response.");

            return (JsonSerializer.Deserialize<T>(body, JsonOptions), (int)response.StatusCode, null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not connect to Platform API for GET {Path}", path);
            SetDevelopmentErrorDetails($"API request: GET {path}\nException: {ex.GetType().FullName}\nMessage: {ex.Message}\n\n{ex}");
            return (default, 502, "Cannot reach Platform API. Check that the API is running and Api:BaseUrl is correct.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Platform API GET {Path} timed out", path);
            SetDevelopmentErrorDetails($"API request: GET {path}\nException: {ex.GetType().FullName}\nMessage: {ex.Message}\n\n{ex}");
            return (default, 504, "Platform API request timed out.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Platform API returned invalid JSON for GET {Path}", path);
            SetDevelopmentErrorDetails($"API request: GET {path}\nException: {ex.GetType().FullName}\nMessage: {ex.Message}\n\n{ex}");
            return (default, 502, "Platform API returned invalid JSON.");
        }
    }

    private void SetDevelopmentErrorDetails(string details)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null && context.RequestServices
                .GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            context.Items["PlatformHub.ErrorDetails"] = details;
    }

    public async Task<T?> GetAsync<T>(
        string path,
        string? token = null,
        CancellationToken ct = default)
    {
        // Keep a single GET implementation so status codes, timeouts, invalid JSON,
        // and API outage diagnostics are handled consistently.
        var result = await GetWithStatusAsync<T>(path, token, ct);
        if (result.Value is null && result.Error is not null)
        {
            logger.LogWarning(
                "Platform API GET failed. Path={Path}, StatusCode={StatusCode}, Error={Error}",
                path, result.StatusCode, result.Error);
        }

        return result.Value;
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken ct = default, string? token = null)
    {
        var client = factory.CreateClient("PlatformApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await SendWithRefreshAsync(client, request, ct);
            if (response.IsSuccessStatusCode) return true;
            logger.LogWarning("Platform API DELETE {Path} failed with HTTP {StatusCode}. Response body omitted",
                path, (int)response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not connect to Platform API for DELETE {Path}", path);
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Platform API DELETE {Path} timed out", path);
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        var context = httpContextAccessor.HttpContext;
        var refreshToken = context?.User.FindFirst("ph_api_refresh_token")?.Value;
        var userIdText = context?.User.FindFirst("ph_user_id")?.Value;
        var isAuthEndpoint = request.RequestUri?.OriginalString.Contains("/api/v1/auth/", StringComparison.OrdinalIgnoreCase) == true;
        var method = request.Method;
        var uri = request.RequestUri;
        var version = request.Version;
        var headers = request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray());
        var contentBytes = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(ct);
        var contentHeaders = request.Content?.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray());

        var response = await client.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || context is null || isAuthEndpoint)
            return response;

        if (string.IsNullOrWhiteSpace(refreshToken) || !long.TryParse(userIdText, out var userId))
        {
            if (context.User.Identity?.IsAuthenticated == true)
                context.Items["PlatformHub.SessionExpired"] = true;
            return response;
        }

        response.Dispose();
        var previousTokenKey = HashRefreshToken(refreshToken);
        await RefreshGate.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in RotatedTokens)
                if (entry.Value.ExpiresAt <= now)
                    RotatedTokens.TryRemove(entry.Key, out _);

            if (RotatedTokens.TryGetValue(previousTokenKey, out var cached) && cached.ExpiresAt > now)
            {
                await UpdateCookieTokensAsync(context, cached.AccessToken, cached.RefreshToken);
                return await RetryWithAccessTokenAsync(client, method, uri, version, headers,
                    contentBytes, contentHeaders, cached.AccessToken, ct);
            }

            using var refreshResponse = await client.PostAsJsonAsync(
                "api/v1/auth/refresh", new { RefreshToken = refreshToken }, ct);
            if (!refreshResponse.IsSuccessStatusCode)
            {
                var refreshError = await refreshResponse.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Platform API refresh failed with HTTP {StatusCode} for UserId={UserId}",
                    (int)refreshResponse.StatusCode, userId);
                var isConcurrentRotation = TryReadApiMessage(refreshError)?.Contains(
                    "already been rotated", StringComparison.OrdinalIgnoreCase) == true;
                if (refreshResponse.StatusCode == HttpStatusCode.Unauthorized && !isConcurrentRotation)
                {
                    context.Items["PlatformHub.SessionExpired"] = true;
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var tokens = await refreshResponse.Content.ReadFromJsonAsync<ApiLoginResponse>(JsonOptions, ct);
            if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken) || string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                logger.LogError("Platform API refresh returned an incomplete token response for UserId={UserId}", userId);
                context.Items["PlatformHub.SessionExpired"] = true;
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            RotatedTokens[previousTokenKey] = new CachedApiTokens(
                tokens.AccessToken, tokens.RefreshToken, DateTimeOffset.UtcNow.AddMinutes(1));
            await UpdateCookieTokensAsync(context, tokens.AccessToken, tokens.RefreshToken);
            return await RetryWithAccessTokenAsync(client, method, uri, version, headers,
                contentBytes, contentHeaders, tokens.AccessToken, ct);
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private async Task UpdateCookieTokensAsync(HttpContext context, string accessToken, string refreshToken)
    {
        var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(context.User.Identity);
        foreach (var claim in identity.FindAll("ph_api_access_token").ToArray()) identity.RemoveClaim(claim);
        foreach (var claim in identity.FindAll("ph_api_refresh_token").ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim("ph_api_access_token", accessToken));
        identity.AddClaim(new Claim("ph_api_refresh_token", refreshToken));
        var principal = new ClaimsPrincipal(identity);
        context.User = principal;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            auth.Properties ?? new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }

    private static async Task<HttpResponseMessage> RetryWithAccessTokenAsync(
        HttpClient client,
        HttpMethod method,
        Uri? uri,
        Version version,
        IReadOnlyDictionary<string, string[]> headers,
        byte[]? contentBytes,
        IReadOnlyDictionary<string, string[]>? contentHeaders,
        string accessToken,
        CancellationToken ct)
    {
        var retry = new HttpRequestMessage(method, uri) { Version = version };
        foreach (var header in headers)
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (contentBytes is not null)
        {
            retry.Content = new ByteArrayContent(contentBytes);
            if (contentHeaders is not null)
                foreach (var header in contentHeaders)
                    retry.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return await client.SendAsync(retry, ct);
    }

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

}
