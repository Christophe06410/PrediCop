namespace PrediCop.Core.Interfaces;

public interface IPushNotificationService
{
    /// <summary>Envoie un push à un device token spécifique.</summary>
    Task SendToDeviceAsync(string deviceToken, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default);

    /// <summary>Envoie un push à plusieurs devices (pour tous les agents d'un véhicule).</summary>
    Task SendToDevicesAsync(IEnumerable<string> deviceTokens, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default);
}
