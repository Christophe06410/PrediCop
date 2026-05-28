namespace PrediCop.Mobile.Services;

/// <summary>
/// Préférences utilisateur persistées sur l'appareil (Preferences.Default).
/// </summary>
public static class AppPreferences
{
    private const string KeyAlertSound = "alertSoundEnabled";

    /// <summary>Joue le son de notification système quand une mission est proposée.</summary>
    public static bool AlertSoundEnabled
    {
        get => Preferences.Default.Get(KeyAlertSound, true);
        set => Preferences.Default.Set(KeyAlertSound, value);
    }
}
