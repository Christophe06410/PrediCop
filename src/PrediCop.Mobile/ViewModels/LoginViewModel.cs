using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class LoginViewModel(
    AuthService auth,
    TenantFeaturesService features,
    SignalRService signalR,
    GpsTrackingService gps,
    ILogger<LoginViewModel> log) : ObservableObject
{
#if DEBUG
    [ObservableProperty] private string email = "officier@predicop.fr";
    [ObservableProperty] private string password = "Officer123!";
#else
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
#endif

    [ObservableProperty] private string errorMessage = "";
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isLoadingTenants;
    [ObservableProperty] private ObservableCollection<TenantItem> tenants = [];
    [ObservableProperty] private TenantItem? selectedTenant;

    public async Task LoadTenantsAsync()
    {
        IsLoadingTenants = true;
        try
        {
            var list = await auth.GetTenantsAsync();
            Tenants = new ObservableCollection<TenantItem>(list);

#if DEBUG
            SelectedTenant = Tenants.FirstOrDefault(t => t.Slug == "predicop") ?? Tenants.FirstOrDefault();
#endif
        }
        finally { IsLoadingTenants = false; }
    }

    /// <summary>
    /// Appelé après login et à la reprise de session persistée.
    /// Charge les feature flags, configure les onglets selon le rôle,
    /// démarre GPS + SignalR uniquement pour les Officers.
    /// </summary>
    public async Task ConnectServicesAsync()
    {
        if (auth.Token == null || auth.CurrentUser == null) return;

        // Charger les feature flags du tenant
        await features.LoadAsync();

        // Ajuster les onglets visibles selon rôle + module
        if (Shell.Current is AppShell shell)
            shell.BuildTabs(auth.CurrentUser.Role, features.Current.ModuleVerbalisationEnabled);

        var role = auth.CurrentUser.Role;
        bool isOfficer = string.Equals(role, "Officer", StringComparison.OrdinalIgnoreCase);
        bool isPatrolLeader = string.Equals(role, "PatrolLeader", StringComparison.OrdinalIgnoreCase);
        bool isPatrolAgent = string.Equals(role, "PatrolAgent", StringComparison.OrdinalIgnoreCase);

        if (features.Current.GpsTrackingEnabled)
        {
            if (isOfficer && auth.VehicleId.HasValue)
            {
                // Officer classique : GPS lié au véhicule
                if (!signalR.IsConnected)
                    try { await signalR.ConnectAsync(auth.Token, auth.VehicleId.Value); } catch { }
                if (!gps.IsTracking)
                    try { await gps.StartAsync(auth.VehicleId.Value); } catch { }
            }
            else if (isPatrolLeader || isPatrolAgent)
            {
                // Chef et agents : GPS individuel immédiatement (même avant activation du véhicule)
                if (!gps.IsTracking)
                    try { await gps.StartAgentTrackingAsync(); } catch { }
            }
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (SelectedTenant is null)
        {
            ErrorMessage = "Veuillez sélectionner votre ville.";
            HasError = true;
            return;
        }

        HasError = false;
        IsLoading = true;
        log.LogInformation("Login attempt for '{Email}' on tenant '{Slug}'", Email.Trim(), SelectedTenant.Slug);
        try
        {
            var success = await auth.LoginAsync(Email.Trim(), Password, SelectedTenant.Slug);
            if (success)
            {
                log.LogInformation("Login succeeded — role={Role}", auth.CurrentUser?.Role);
                await ConnectServicesAsync();

                var dest = AppShell.GetFirstRoute(auth.CurrentUser?.Role ?? "");
                await Shell.Current.GoToAsync(dest);
            }
            else
            {
                log.LogWarning("Login returned false (bad credentials)");
                ErrorMessage = "Identifiants incorrects.";
                HasError = true;
            }
        }
        catch (HttpRequestException ex)
        {
            log.LogError(ex, "HTTP error during login");
            ErrorMessage = $"Erreur HTTP {(int?)ex.StatusCode}: {ex.Message}";
            HasError = true;
        }
        catch (TaskCanceledException ex)
        {
            log.LogError(ex, "Timeout during login");
            ErrorMessage = "Délai d'attente dépassé. Vérifiez l'IP/port du serveur.";
            HasError = true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error during login");
            ErrorMessage = $"[{ex.GetType().Name}] {ex.Message}";
            HasError = true;
        }
        finally { IsLoading = false; }
    }
}
