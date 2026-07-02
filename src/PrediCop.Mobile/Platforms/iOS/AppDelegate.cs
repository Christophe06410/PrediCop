using Foundation;
using PrediCop.Mobile.Platforms.iOS;
using UIKit;

namespace PrediCop.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnActivated(UIApplication application)
    {
        base.OnActivated(application);
        ApplyFullScreen(application);
    }

    private static bool _fullScreenApplied;

    private static void ApplyFullScreen(UIApplication application)
    {
        if (_fullScreenApplied) return;

        // Trouver la fenêtre clé dans la scène active (iOS 13+)
        UIWindow? window = null;
        foreach (var scene in application.ConnectedScenes)
        {
            if (scene is UIWindowScene windowScene)
            {
                window = windowScene.Windows.FirstOrDefault(w => w.IsKeyWindow)
                      ?? windowScene.Windows.FirstOrDefault();
                if (window is not null) break;
            }
        }

        var rootVC = window?.RootViewController;
        if (rootVC is null || rootVC is FullScreenViewController) return;

        // Remplacer le root VC par notre wrapper full-screen
        // Le VC MAUI devient un enfant → navigation et tout le reste continuent de fonctionner
        window!.RootViewController = new FullScreenViewController(rootVC);
        _fullScreenApplied = true;
    }
}
