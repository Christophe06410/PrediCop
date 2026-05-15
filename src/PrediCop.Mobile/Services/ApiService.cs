using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiService(HttpClient http)
    {
        _http = http;
    }

    public void SetAuthToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(endpoint, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
    }

    public async Task<T?> PostAsync<T>(string endpoint, object? body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(endpoint, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
    }

    public async Task PostAsync(string endpoint, object? body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(endpoint, body, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<T?> PutAsync<T>(string endpoint, object? body, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(endpoint, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
    }
}
