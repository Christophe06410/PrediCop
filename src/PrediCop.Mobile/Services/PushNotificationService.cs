namespace PrediCop.Mobile.Services;

/// <summary>
/// Enregistre le device token FCM auprès de l'API après login.
/// No-op sur Windows (Plugin.Firebase ne supporte pas Windows).
/// </summary>
public class PushNotificationService(ApiService api)
{
    private bool _subscribed;

    public async Task RegisterAsync()
    {
#if ANDROID || IOS
        try
        {
#if ANDROID
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[Push] Permission POST_NOTIFICATIONS refusée");
                return;
            }
#endif

#if IOS
            // Demande l'autorisation APNs (affiche la dialog système au premier appel)
            await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
#endif

            var token = await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                await SendTokenToApiAsync(token);

            if (!_subscribed)
            {
                // Rafraîchissement automatique du token
                Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.TokenChanged +=
                    async (_, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Token))
                            await SendTokenToApiAsync(e.Token);
                    };

                // Tap sur une notification reçue en background → ouvre l'onglet Missions
                Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.NotificationTapped +=
                    (_, e) => HandleNotificationTap(e.Notification?.Data as IDictionary<string, string>);

                _subscribed = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] Registration failed: {ex.Message}");
        }
#else
        await Task.CompletedTask;
#endif
    }

    private async Task SendTokenToApiAsync(string token)
    {
        try
        {
            await api.PostAsync<object>("api/devices/push-token", new { token });
            System.Diagnostics.Debug.WriteLine($"[Push] Token envoyé à l'API: {token[..Math.Min(20, token.Length)]}…");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] Échec envoi token: {ex.Message}");
        }
    }

    private static void HandleNotificationTap(IDictionary<string, string>? data)
    {
        if (data == null) return;
        data.TryGetValue("type", out var type);
        if (type == "mission_proposed")
            MainThread.BeginInvokeOnMainThread(
                () => _ = Shell.Current?.GoToAsync("//main/missions"));
    }
}
