using Android.Media;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Platforms.Android;

public class AlertSoundService : IAlertSoundService
{
    private Ringtone? _ringtone;
    private volatile CancellationTokenSource _cts = new();

    public void PlayAlert()
    {
        StopAlert(); // annule et stoppe tout son précédent
        try
        {
            var uri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm)
                   ?? RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            if (uri == null) return;

            _ringtone = RingtoneManager.GetRingtone(
                global::Android.App.Application.Context, uri);
            if (_ringtone == null) return;

            // Stream ALARM : joue même en mode silencieux/vibreur
            var attrs = new Android.Media.AudioAttributes.Builder()
                .SetUsage(Android.Media.AudioUsageKind.Alarm)
                .SetContentType(Android.Media.AudioContentType.Sonification)
                .Build()!;
            _ringtone.AudioAttributes = attrs;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var ringtone = _ringtone;

            ringtone.Play();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1200, token);
                    ringtone.Play();
                    await Task.Delay(1200, token);
                    ringtone.Play();
                }
                catch { /* annulé ou erreur périphérique */ }
            });
        }
        catch { }
    }

    public void StopAlert()
    {
        _cts.Cancel();
        try { _ringtone?.Stop(); } catch { }
    }
}
