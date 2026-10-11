using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PlatformHub.Mvc.Services;

public sealed class PlatformApiClient(
    IHttpClientFactory factory,
    ILogger<PlatformApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Platform API POST {Path} failed with HTTP {StatusCode}. Response: {ResponseBody}",
                    path,
                    (int)response.StatusCode,
                    Truncate(responseBody));
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
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Platform API PUT {Path} failed with HTTP {StatusCode}. Response: {ResponseBody}",
                    path, (int)response.StatusCode, Truncate(responseBody));
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

    public async Task<T?> GetAsync<T>(
        string path,
        string? token = null,
        CancellationToken ct = default)
    {
        var client = factory.CreateClient("PlatformApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Platform API GET {Path} failed with HTTP {StatusCode}. Response: {ResponseBody}",
                    path,
                    (int)response.StatusCode,
                    Truncate(responseBody));
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not connect to Platform API for GET {Path}", path);
            return default;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Platform API GET {Path} timed out", path);
            return default;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Platform API returned invalid JSON for GET {Path}", path);
            return default;
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 2000 ? value : value[..2000];
}
