using AudioToolbox;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Platforms.iOS;

public class AlertSoundService : IAlertSoundService
{
    // 1007 = system "SMS received" tone, audible and short
    private const uint NotificationSoundId = 1007;

    public void PlayAlert()
    {
        try { SystemSound.FromFile(NotificationSoundId.ToString())?.PlaySystemSound(); }
        catch
        {
            try { new SystemSound(NotificationSoundId).PlaySystemSound(); } catch { }
        }
    }

    public void StopAlert()
    {
        // iOS SystemSound plays a short one-shot tone — it stops naturally, no explicit stop API.
    }
}
