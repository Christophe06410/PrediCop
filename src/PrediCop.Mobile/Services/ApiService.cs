using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PrediCop.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiService> _log;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Initialisé après construction pour éviter la dépendance circulaire
    // ApiService → MobileErrorService → ApiService
    internal MobileErrorService? ErrorReporter { get; set; }

    public ApiService(HttpClient http, ILogger<ApiService> log)
    {
        _http = http;
        _log = log;
        _log.LogInformation("ApiService created. BaseAddress={BaseAddress}", http.BaseAddress);
    }

    /// <summary>Indique si l'accès Internet est disponible selon MAUI Connectivity.</summary>
    public bool IsNetworkAvailable =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public void SetAuthToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _http.GetAsync(endpoint, ct);
            _log.LogInformation("GET {Endpoint} → {Status} in {Ms}ms", endpoint, (int)response.StatusCode, sw.ElapsedMilliseconds);
            await HandleErrorResponseAsync(response, "GET", endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _log.LogInformation("GET {Endpoint} FAILED after {Ms}ms: {Message}", endpoint, sw.ElapsedMilliseconds, ex.Message);
            ReportNetworkException("GET", endpoint, ex);
            throw;
        }
    }

    public async Task<T?> PostAsync<T>(string endpoint, object? body, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _http.PostAsJsonAsync(endpoint, body, ct);
            _log.LogInformation("POST {Endpoint} → {Status} in {Ms}ms", endpoint, (int)response.StatusCode, sw.ElapsedMilliseconds);
            await HandleErrorResponseAsync(response, "POST", endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _log.LogInformation("POST {Endpoint} FAILED after {Ms}ms: {Message}", endpoint, sw.ElapsedMilliseconds, ex.Message);
            ReportNetworkException("POST", endpoint, ex);
            throw;
        }
    }

    public async Task PostAsync(string endpoint, object? body, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _http.PostAsJsonAsync(endpoint, body, ct);
            _log.LogInformation("POST {Endpoint} → {Status} in {Ms}ms", endpoint, (int)response.StatusCode, sw.ElapsedMilliseconds);
            await HandleErrorResponseAsync(response, "POST", endpoint);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _log.LogInformation("POST {Endpoint} FAILED after {Ms}ms: {Message}", endpoint, sw.ElapsedMilliseconds, ex.Message);
            ReportNetworkException("POST", endpoint, ex);
            throw;
        }
    }

    public async Task<T?> PutAsync<T>(string endpoint, object? body, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _http.PutAsJsonAsync(endpoint, body, ct);
            _log.LogInformation("PUT {Endpoint} → {Status} in {Ms}ms", endpoint, (int)response.StatusCode, sw.ElapsedMilliseconds);
            await HandleErrorResponseAsync(response, "PUT", endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _log.LogInformation("PUT {Endpoint} FAILED after {Ms}ms: {Message}", endpoint, sw.ElapsedMilliseconds, ex.Message);
            ReportNetworkException("PUT", endpoint, ex);
            throw;
        }
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
        ErrorReporter?.ReportApiError("POST", "/api/auth/change-password", (int)response.StatusCode, body);

        var error = (int)response.StatusCode == 400
            ? "Mot de passe actuel incorrect."
            : $"Erreur serveur ({(int)response.StatusCode}).";
        return (false, error);
    }

    public async Task<bool> RegisterDeviceTokenAsync(string deviceToken, CancellationToken ct = default)
    {
        _log.LogDebug("POST /api/auth/device-token");
        var response = await _http.PostAsJsonAsync("/api/auth/device-token",
            new { DeviceToken = deviceToken }, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Envoie un POST sans lever d'exception — utilisé uniquement par MobileErrorService
    /// pour éviter toute récursion infinie en cas d'erreur.
    /// </summary>
    internal async Task PostFireAndForgetAsync(string endpoint, object? body)
    {
        try
        {
            _log.LogDebug("POST (fire-and-forget) {Endpoint}", endpoint);
            await _http.PostAsJsonAsync(endpoint, body);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "PostFireAndForgetAsync failed for {Endpoint}", endpoint);
        }
    }

    private async Task HandleErrorResponseAsync(HttpResponseMessage response, string method, string endpoint)
    {
        if (response.IsSuccessStatusCode)
        {
            _log.LogDebug("{Method} {Endpoint} → {Status}", method, endpoint, (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        _log.LogError("{Method} {Endpoint} → {Status} | Body: {Body}",
            method, endpoint, (int)response.StatusCode, body);

        ErrorReporter?.ReportApiError(method, endpoint, (int)response.StatusCode, body);
    }

    private void ReportNetworkException(string method, string endpoint, Exception ex)
    {
        _log.LogError(ex, "{Method} {Endpoint} → Network/Unexpected exception", method, endpoint);
        ErrorReporter?.ReportNetworkError($"{method} {endpoint}", ex);
    }
}
