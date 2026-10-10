using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PlatformHub.Mvc.Services;

public sealed class PlatformApiClient(IHttpClientFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        var client = factory.CreateClient("PlatformApi");
        using var response = await client.PostAsync(path,
            new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json"), ct);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    public async Task<T?> GetAsync<T>(string path, string? token = null, CancellationToken ct = default)
    {
        var client = factory.CreateClient("PlatformApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }
}
