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

        bool isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

        // Admin/Manager voient tout pour le monitoring terrain
        bool showPatrolTabs = isPatrolRole || isAdmin;

        TabMissions.IsVisible = showPatrolTabs;
        TabPatrol.IsVisible   = showPatrolTabs;
        TabMap.IsVisible      = showPatrolTabs;
        TabTickets.IsVisible  = verbalisationEnabled || isAdmin;
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

            await NavigateAfterLoginAsync(auth.CurrentUser?.Role ?? "");
        }
        else
            await GoToAsync("//login");
    }

    /// <summary>
    /// Sélectionne un onglet du TabBar principal sans GoToAsync.
    /// GoToAsync("//main/tab") est instable en release — on pilote CurrentItem directement.
    /// Thread-safe : dispatch automatique sur le thread UI si nécessaire.
    /// </summary>
    public static void SwitchToTab(string tabRoute)
    {
        if (Current is not AppShell shell) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            shell.CurrentItem = shell.MainTabBar;
            var tab = shell.MainTabBar.Items.FirstOrDefault(t => t.Route == tabRoute);
            if (tab != null)
                shell.MainTabBar.CurrentItem = tab;
        });
    }

    /// <summary>
    /// Navigation post-login fiable. GoToAsync("//main/tab") est instable en release
    /// pour les TabBar — on pilote CurrentItem directement pour les onglets.
    /// </summary>
    public static async Task NavigateAfterLoginAsync(string role)
    {
        if (Current is not AppShell shell) return;

        if (string.Equals(role, "PatrolLeader", StringComparison.OrdinalIgnoreCase))
        {
            await shell.GoToAsync("//patrol-activation");
            return;
        }

        string tabRoute = string.Equals(role, "Verbalisateur", StringComparison.OrdinalIgnoreCase)
            ? "tickets" : "missions";

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            shell.CurrentItem = shell.MainTabBar;
            var tab = shell.MainTabBar.Items.FirstOrDefault(t => t.Route == tabRoute);
            if (tab != null)
                shell.MainTabBar.CurrentItem = tab;
        });
    }
}
