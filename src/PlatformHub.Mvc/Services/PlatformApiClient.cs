using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PlatformHub.Mvc.Services;

public sealed class PlatformApiClient(
    IHttpClientFactory factory,
    ILogger<PlatformApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            using var response = await client.SendAsync(request, ct);
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
            using var response = await client.SendAsync(request, ct);
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
            using var response = await client.SendAsync(request, ct);
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
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Platform API GET {Path} failed with HTTP {StatusCode}. Response body omitted",
                    path, (int)response.StatusCode);
                return (default, (int)response.StatusCode,
                    TryReadApiMessage(body) ?? $"Platform API returned HTTP {(int)response.StatusCode}.");
            }

            if (string.IsNullOrWhiteSpace(body))
                return (default, (int)response.StatusCode, "Platform API returned an empty response.");

            return (JsonSerializer.Deserialize<T>(body, JsonOptions), (int)response.StatusCode, null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not connect to Platform API for GET {Path}", path);
            return (default, 502, "Cannot reach Platform API. Check that the API is running and Api:BaseUrl is correct.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Platform API GET {Path} timed out", path);
            return (default, 504, "Platform API request timed out.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Platform API returned invalid JSON for GET {Path}", path);
            return (default, 502, "Platform API returned invalid JSON.");
        }
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
            using var response = await client.SendAsync(request, ct);
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

}
