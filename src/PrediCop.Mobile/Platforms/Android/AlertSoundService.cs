using Android.Media;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Platforms.Android;

public class AlertSoundService : IAlertSoundService
{
    private MediaPlayer? _player;
    private volatile CancellationTokenSource _cts = new();

    public void PlayAlert()
    {
        StopAlert();
        try
        {
            var ctx = global::Android.App.Application.Context;
            var resId = ctx.Resources!.GetIdentifier("new_mission", "raw", ctx.PackageName);
            if (resId == 0) return;

            // Définir les attributs audio AVANT prepare() pour qu'ils soient pris en compte
            var attrs = new global::Android.Media.AudioAttributes.Builder()
                .SetUsage(global::Android.Media.AudioUsageKind.Alarm)!
                .SetContentType(global::Android.Media.AudioContentType.Sonification)!
                .Build()!;

            var player = new MediaPlayer();
            player.SetAudioAttributes(attrs);

            var fd = ctx.Resources.OpenRawResourceFd(resId)!;
            player.SetDataSource(fd.FileDescriptor, fd.StartOffset, fd.Length);
            fd.Close();
            player.Prepare();

            _player = player;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            player.Start();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1500, token);
                    if (!token.IsCancellationRequested) { player.SeekTo(0); player.Start(); }
                    await Task.Delay(1500, token);
                    if (!token.IsCancellationRequested) { player.SeekTo(0); player.Start(); }
                }
                catch { }
            });
        }
        catch { }
    }

    public void StopAlert()
    {
        _cts.Cancel();
        try { _player?.Stop(); _player?.Release(); _player = null; } catch { }
    }
}
