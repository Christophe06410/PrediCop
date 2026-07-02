using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace PrediCop.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplyFullScreen();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        // Re-appliquer après un dialog/permission qui aurait restauré les barres
        if (hasFocus) ApplyFullScreen();
    }

    private void ApplyFullScreen()
    {
        if (Window is null) return;

        // Laisser le contenu s'étendre sous les barres système
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is null) return;

        // Cacher la barre de navigation ET la status bar
        controller.Hide(WindowInsetsCompat.Type.SystemBars());

        // Swipe depuis le bord pour les afficher temporairement (gesture swipe = reveal)
        controller.SystemBarsBehavior =
            WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
    }
}
