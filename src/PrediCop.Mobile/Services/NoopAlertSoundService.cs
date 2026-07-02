namespace PrediCop.Mobile.Services;

/// <summary>Fallback used on platforms without a native implementation (Windows, MacCatalyst).</summary>
public class NoopAlertSoundService : IAlertSoundService
{
    public void PlayAlert() { }
    public void StopAlert() { }
}
