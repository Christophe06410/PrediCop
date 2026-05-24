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
    /// Les onglets patrouille/missions/carte sont réservés aux Officers, PatrolLeader, PatrolAgent.
    /// L'onglet Verbalisation n'est visible que si le module est activé par le tenant.
    /// </summary>
    public void BuildTabs(string role, bool verbalisationEnabled)
    {
        bool isVerbalisateur = string.Equals(role, "Verbalisateur",
            StringComparison.OrdinalIgnoreCase);

        bool isPatrolRole = string.Equals(role, "Officer", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(role, "PatrolLeader", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(role, "PatrolAgent", StringComparison.OrdinalIgnoreCase);

        TabMissions.IsVisible = isPatrolRole;
        TabPatrol.IsVisible   = isPatrolRole;
        TabMap.IsVisible      = isPatrolRole;
        TabTickets.IsVisible  = verbalisationEnabled;
        // TabProfile toujours visible
    }

    private bool _initialized;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Only run once per Shell lifetime — BuildTabs() changes tab visibility which can
        // re-trigger OnAppearing, causing double navigation and double message registrations.
        if (_initialized) return;
        _initialized = true;

        var auth    = Handler?.MauiContext?.Services.GetService<AuthService>();
        var loginVm = Handler?.MauiContext?.Services.GetService<LoginViewModel>();

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
    public static string GetFirstRoute(string role)
    {
        if (string.Equals(role, "Verbalisateur", StringComparison.OrdinalIgnoreCase))
            return "//main/tickets";
        if (string.Equals(role, "PatrolLeader", StringComparison.OrdinalIgnoreCase))
            return "//patrol-activation"; // Page d'activation avant les onglets principaux
        return "//main/missions";
    }
}
