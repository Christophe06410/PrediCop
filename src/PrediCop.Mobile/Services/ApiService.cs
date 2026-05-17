using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PrediCop.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiService> _log;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiService(HttpClient http, ILogger<ApiService> log)
    {
        _http = http;
        _log = log;
        _log.LogInformation("ApiService created. BaseAddress={BaseAddress}", http.BaseAddress);
    }

    public void SetAuthToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        _log.LogDebug("GET {Endpoint}", endpoint);
        var response = await _http.GetAsync(endpoint, ct);
        await LogIfErrorAsync(response, "GET", endpoint);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
    }

    public async Task<T?> PostAsync<T>(string endpoint, object? body, CancellationToken ct = default)
    {
        _log.LogDebug("POST {Endpoint}", endpoint);
        var response = await _http.PostAsJsonAsync(endpoint, body, ct);
        await LogIfErrorAsync(response, "POST", endpoint);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
    }

    public async Task PostAsync(string endpoint, object? body, CancellationToken ct = default)
    {
        _log.LogDebug("POST {Endpoint}", endpoint);
        var response = await _http.PostAsJsonAsync(endpoint, body, ct);
        await LogIfErrorAsync(response, "POST", endpoint);
        response.EnsureSuccessStatusCode();
    }

    public async Task<T?> PutAsync<T>(string endpoint, object? body, CancellationToken ct = default)
    {
        _log.LogDebug("PUT {Endpoint}", endpoint);
        var response = await _http.PutAsJsonAsync(endpoint, body, ct);
        await LogIfErrorAsync(response, "PUT", endpoint);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken ct = default)
    {
        _log.LogDebug("POST /api/auth/change-password");
        var response = await _http.PostAsJsonAsync("/api/auth/change-password",
            new { CurrentPassword = currentPassword, NewPassword = newPassword }, ct);

        if (response.IsSuccessStatusCode) return (true, null);

        var body = await response.Content.ReadAsStringAsync(ct);
        _log.LogError("POST /api/auth/change-password → {Status} | Body: {Body}", (int)response.StatusCode, body);

        var error = (int)response.StatusCode == 400
            ? "Mot de passe actuel incorrect."
            : $"Erreur serveur ({(int)response.StatusCode}).";
        return (false, error);
    }

    private async Task LogIfErrorAsync(HttpResponseMessage response, string method, string endpoint)
    {
        if (response.IsSuccessStatusCode)
        {
            _log.LogDebug("{Method} {Endpoint} → {Status}", method, endpoint, (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        _log.LogError("{Method} {Endpoint} → {Status} | Body: {Body}",
            method, endpoint, (int)response.StatusCode, body);
    }
}
