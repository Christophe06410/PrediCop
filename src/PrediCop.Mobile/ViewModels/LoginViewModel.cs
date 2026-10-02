using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly TenantFeaturesService _features;
    private readonly SignalRService _signalR;
    private readonly GpsTrackingService _gps;
    private readonly PushNotificationService _push;
    private readonly ILogger<LoginViewModel> _log;

    public LoginViewModel(
        AuthService auth,
        TenantFeaturesService features,
        SignalRService signalR,
        GpsTrackingService gps,
        PushNotificationService push,
        ILogger<LoginViewModel> log)
    {
        _auth = auth;
        _features = features;
        _signalR = signalR;
        _gps = gps;
        _push = push;
        _log = log;
#if DEBUG
        Email = "officier@predicop.fr";
        Password = "Officer123!";
#else
        Email = "";
        Password = "";
#endif
        ErrorMessage = "";
        Tenants = [];
    }

    [ObservableProperty]
    private string _email;

    [ObservableProperty]
    private string _password;

    [ObservableProperty] private string _errorMessage;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingTenants;
    [ObservableProperty] private bool _tenantsLoadFailed;
    [ObservableProperty] private ObservableCollection<TenantItem> _tenants;
    [ObservableProperty] private TenantItem? _selectedTenant;

    private const string LastTenantKey = "login_last_tenant_slug";

    public async Task LoadTenantsAsync()
    {
        IsLoadingTenants = true;
        TenantsLoadFailed = false;
        try
        {
            List<TenantItem> list = [];
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (attempt > 0) await Task.Delay(2000);
                list = await _auth.GetTenantsAsync();
                if (list.Count > 0) break;
            }

            Tenants = new ObservableCollection<TenantItem>(list);

            if (Tenants.Count == 0)
            {
                TenantsLoadFailed = true;
                ErrorMessage = "Aucune ville disponible. Vérifiez la connexion au serveur.";
                HasError = true;
                return;
            }

            HasError = false;
            ErrorMessage = "";

#if DEBUG
            SelectedTenant = Tenants.FirstOrDefault(t => t.Slug == "predicop") ?? Tenants.FirstOrDefault();
#else
            var lastSlug = Preferences.Get(LastTenantKey, null);
            SelectedTenant = (lastSlug != null ? Tenants.FirstOrDefault(t => t.Slug == lastSlug) : null)
                             ?? Tenants.FirstOrDefault();
#endif
        }
        catch
        {
            TenantsLoadFailed = true;
        }
        finally { IsLoadingTenants = false; }
    }

    [RelayCommand]
    private Task RefreshTenantsAsync() => LoadTenantsAsync();

    /// <summary>
    /// Appelé après login et à la reprise de session persistée.
    /// Charge les feature flags, configure les onglets selon le rôle,
    /// démarre GPS + SignalR uniquement pour les Officers.
    /// </summary>
    public async Task ConnectServicesAsync()
    {
        if (_auth.Token == null || _auth.CurrentUser == null) return;

        var role = _auth.CurrentUser.Role;
        bool isOfficer      = string.Equals(role, "Officer",      StringComparison.OrdinalIgnoreCase);
        bool isPatrolLeader = string.Equals(role, "PatrolLeader", StringComparison.OrdinalIgnoreCase);
        bool isPatrolAgent  = string.Equals(role, "PatrolAgent",  StringComparison.OrdinalIgnoreCase);
        bool isPatrolRole   = isOfficer || isPatrolLeader || isPatrolAgent;

        // Features et SignalR en parallèle — SignalR ne dépend pas des feature flags
        var featuresTask = _features.LoadAsync();
        var signalRTask  = isPatrolRole && _auth.VehicleId.HasValue && !_signalR.IsConnected
            ? _signalR.ConnectAsync(_auth.Token, _auth.VehicleId.Value).ContinueWith(_ => { })
            : Task.CompletedTask;

        try { await Task.WhenAll(featuresTask, signalRTask); } catch { }

        // BuildTabs nécessite les features chargées et doit s'exécuter sur le thread UI
        if (Shell.Current is AppShell shell)
        {
            var verbalisationEnabled = _features.Current.ModuleVerbalisationEnabled;
            await MainThread.InvokeOnMainThreadAsync(() => shell.BuildTabs(role, verbalisationEnabled));
        }

        // Push notifications — enregistre le device token FCM (best-effort, ne bloque pas)
        _ = _push.RegisterAsync();

        // GPS nécessite GpsTrackingEnabled — démarre après features
        if (_features.Current.GpsTrackingEnabled)
        {
            if (isOfficer && _auth.VehicleId.HasValue)
            {
                // Officer classique : GPS lié au véhicule
                if (!_gps.IsTracking)
                    try { await _gps.StartAsync(_auth.VehicleId.Value); } catch { }
            }
            else if (isPatrolLeader || isPatrolAgent)
            {
                // Chef et agents : GPS individuel immédiatement (même avant activation du véhicule)
                if (!_gps.IsTracking)
                    try { await _gps.StartAgentTrackingAsync(); } catch { }
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
        _log.LogInformation("Login attempt for '{Email}' on tenant '{Slug}'", Email.Trim(), SelectedTenant.Slug);
        try
        {
            var success = await _auth.LoginAsync(Email.Trim(), Password, SelectedTenant.Slug);
            if (success)
            {
                _log.LogInformation("Login succeeded — role={Role}", _auth.CurrentUser?.Role);
                Preferences.Set(LastTenantKey, SelectedTenant.Slug);
                await ConnectServicesAsync();

                await AppShell.NavigateAfterLoginAsync(_auth.CurrentUser?.Role ?? "");
            }
            else
            {
                _log.LogWarning("Login returned false (bad credentials)");
                ErrorMessage = "Identifiants incorrects.";
                HasError = true;
            }
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "HTTP error during login");
            ErrorMessage = ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Email ou mot de passe incorrect."
                : $"Erreur de connexion ({(int?)ex.StatusCode}). Vérifiez le réseau.";
            HasError = true;
        }
        catch (TaskCanceledException ex)
        {
            _log.LogError(ex, "Timeout during login");
            ErrorMessage = "Délai d'attente dépassé. Vérifiez l'IP/port du serveur.";
            HasError = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected error during login");
            ErrorMessage = $"[{ex.GetType().Name}] {ex.Message}";
            HasError = true;
        }
        finally { IsLoading = false; }
    }
}
