using UIKit;

namespace PrediCop.Mobile.Platforms.iOS;

/// <summary>
/// Wrapper autour du root view controller de MAUI.
/// Déclare PrefersHomeIndicatorAutoHidden = true et PrefersStatusBarHidden = true
/// afin que iOS cache la barre d'accueil et la status bar.
/// </summary>
public class FullScreenViewController : UIViewController
{
    private readonly UIViewController _inner;

    public FullScreenViewController(UIViewController inner)
    {
        _inner = inner;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // Intégrer le VC de MAUI comme enfant pour que la navigation continue de fonctionner
        AddChildViewController(_inner);
        View!.AddSubview(_inner.View!);
        _inner.DidMoveToParentViewController(this);

        _inner.View!.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints([
            _inner.View.TopAnchor.ConstraintEqualTo(View.TopAnchor),
            _inner.View.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
            _inner.View.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            _inner.View.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
        ]);
    }

    // Déléguer la gestion de la home indicator au VC enfant (MAUI) —
    // si MAUI veut la montrer pour une page spécifique il peut le faire.
    public override UIViewController? ChildViewControllerForHomeIndicatorAutoHidden => _inner;

    // Cache la home indicator bar (barre de geste en bas de l'écran)
    public override bool PrefersHomeIndicatorAutoHidden => true;

    // Cache la status bar
    public override bool PrefersStatusBarHidden() => true;
    public override UIStatusBarAnimation PreferredStatusBarUpdateAnimation =>
        UIStatusBarAnimation.Fade;
}
