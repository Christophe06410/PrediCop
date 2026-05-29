using Microsoft.Extensions.Logging;

namespace PrediCop.Mobile.Services;

/// <summary>
/// Signale les erreurs graves de l'application mobile au serveur
/// afin qu'elles soient persistées en base de données.
/// Les erreurs sont envoyées en fire-and-forget pour ne jamais bloquer l'UI.
/// </summary>
public class MobileErrorService(ApiService api, AuthService auth, ILogger<MobileErrorService> log)
{
    private static readonly string AppVersion =
        AppInfo.VersionString ?? "?";

    private static readonly string Platform =
        DeviceInfo.Platform.ToString();

    private static readonly string DeviceModel =
        $"{DeviceInfo.Manufacturer} {DeviceInfo.Model}";

    /// <summary>
    /// Signale une erreur HTTP (réponse non-2xx d'un appel API).
    /// Appelé automatiquement par <see cref="ApiService"/>.
    /// </summary>
    public void ReportApiError(string method, string endpoint, int statusCode, string responseBody)
    {
        var message = $"{method} {endpoint} → HTTP {statusCode}";
        log.LogError("[MobileError] ApiError: {Message} | Body: {Body}", message, responseBody);
        _ = SendAsync("ApiError", message, stackTrace: null, endpoint, statusCode);
    }

    /// <summary>
    /// Signale une exception non gérée ou une erreur grave applicative.
    /// </summary>
    public void ReportException(string category, Exception ex, string? context = null)
    {
        var message = context is null ? ex.Message : $"{context}: {ex.Message}";
        log.LogError(ex, "[MobileError] {Category}: {Message}", category, message);
        _ = SendAsync(category, message, ex.ToString(), endpoint: null, httpStatusCode: null);
    }

    /// <summary>
    /// Signale une erreur réseau (pas de connexion, timeout, etc.).
    /// </summary>
    public void ReportNetworkError(string endpoint, Exception ex)
    {
        var message = $"Network error on {endpoint}: {ex.Message}";
        log.LogError(ex, "[MobileError] NetworkError: {Message}", message);
        _ = SendAsync("NetworkError", message, ex.ToString(), endpoint, httpStatusCode: null);
    }

    private async Task SendAsync(
        string category,
        string message,
        string? stackTrace,
        string? endpoint,
        int? httpStatusCode)
    {
        // Ne pas envoyer si pas de réseau — inutile
        if (!api.IsNetworkAvailable) return;

        try
        {
            var payload = new
            {
                AppVersion,
                Platform,
                DeviceModel,
                Category      = category,
                Message       = message,
                StackTrace    = stackTrace,
                Endpoint      = endpoint,
                HttpStatusCode = httpStatusCode,
            };

            // Endpoint public — pas besoin d'authentification
            // On utilise PostAsync sans lever d'exception pour éviter les boucles infinies
            await api.PostFireAndForgetAsync("api/mobile-logs/error", payload);
        }
        catch (Exception ex)
        {
            // Dernier recours : log local uniquement, on ne veut pas de récursion
            log.LogWarning(ex, "[MobileErrorService] Impossible d'envoyer le rapport d'erreur au serveur");
        }
    }
}
