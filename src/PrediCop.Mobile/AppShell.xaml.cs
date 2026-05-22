using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Appelé après login (et à la reprise de session) pour afficher les bons onglets.
    /// Les onglets patrouille/missions/carte sont réservés aux Officers.
    /// L'onglet Verbalisation n'est visible que si le module est activé par le tenant.
    /// </summary>
    public void BuildTabs(string role, bool verbalisationEnabled)
    {
        bool isVerbalisateur = string.Equals(role, "Verbalisateur",
            StringComparison.OrdinalIgnoreCase);

        TabMissions.IsVisible = !isVerbalisateur;
        TabPatrol.IsVisible   = !isVerbalisateur;
        TabMap.IsVisible      = !isVerbalisateur;
        TabTickets.IsVisible  = verbalisationEnabled;
        // TabProfile toujours visible
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var auth     = Handler?.MauiContext?.Services.GetService<AuthService>();
        var loginVm  = Handler?.MauiContext?.Services.GetService<LoginViewModel>();

        if (auth?.IsLoggedIn == true)
        {
            if (loginVm != null)
                await loginVm.ConnectServicesAsync();

            var dest = GetFirstRoute(auth.CurrentUser?.Role ?? "");
            await GoToAsync(dest);
        }
        else
            await GoToAsync("//login");
    }

    /// <summary>Retourne la route de démarrage selon le rôle.</summary>
    public static string GetFirstRoute(string role) =>
        string.Equals(role, "Verbalisateur", StringComparison.OrdinalIgnoreCase)
            ? "//main/tickets"
            : "//main/missions";
}
