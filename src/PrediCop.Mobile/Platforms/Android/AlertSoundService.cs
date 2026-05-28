using Android.Media;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Platforms.Android;

public class AlertSoundService : IAlertSoundService
{
    public void PlayAlert()
    {
        try
        {
            var uri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            if (uri == null) return;
            var ringtone = RingtoneManager.GetRingtone(global::Android.App.Application.Context, uri);
            ringtone?.Play();
        }
        catch { /* device may have no default notification sound */ }
    }
}
