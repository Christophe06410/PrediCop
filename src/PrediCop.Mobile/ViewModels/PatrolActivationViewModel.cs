using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class PatrolActivationViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly AuthService _auth;
    private readonly GpsTrackingService _gps;
    private readonly SignalRService _signalR;
    private readonly TenantFeaturesService _features;

    public PatrolActivationViewModel(
        ApiService api,
        AuthService auth,
        GpsTrackingService gps,
        SignalRService signalR,
        TenantFeaturesService features)
    {
        _api = api;
        _auth = auth;
        _gps = gps;
        _signalR = signalR;
        _features = features;
        Vehicles = [];
        Indicatif = "";
        PatrolTypes = [];
        AvailableAgents = [];
        SelectedAgents = [];
        ErrorMessage = "";
    }

    [ObservableProperty] private ObservableCollection<VehicleItem> _vehicles;
    [ObservableProperty] private VehicleItem? _selectedVehicle;
    [ObservableProperty] private string _indicatif;
    [ObservableProperty] private ObservableCollection<PatrolTypeItem> _patrolTypes;
    [ObservableProperty] private PatrolTypeItem? _selectedPatrolType;
    [ObservableProperty] private ObservableCollection<AgentItem> _availableAgents;
    [ObservableProperty] private ObservableCollection<AgentItem> _selectedAgents;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isActivating;
    [ObservableProperty] private string _errorMessage;
    [ObservableProperty] private bool _hasError;

    public bool CanActivate =>
        SelectedVehicle != null && !string.IsNullOrWhiteSpace(Indicatif) && SelectedPatrolType != null;

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            // Vérifie si on est déjà dans un véhicule actif (reconnexion en cours de service)
            var activeVehicle = await _api.GetAsync<VehicleItem>("api/patrol/my-active-vehicle");
            if (activeVehicle != null && !string.IsNullOrEmpty(activeVehicle.Indicatif))
            {
                var (success, _) = await _auth.SelectVehicleAsync(activeVehicle.Id);
                if (success)
                {
                    if (_features.Current.GpsTrackingEnabled && _auth.VehicleId.HasValue)
                        try { await _gps.StartAsync(_auth.VehicleId.Value); } catch { }
                    if (!_signalR.IsConnected && _auth.Token != null && _auth.VehicleId.HasValue)
                        try { await _signalR.ConnectAsync(_auth.Token, _auth.VehicleId.Value); } catch { }
                    AppShell.SwitchToTab("missions");
                    return;
                }
            }

            PatrolTypes = new ObservableCollection<PatrolTypeItem>([
                new PatrolTypeItem("Car",        "Voiture",  "🚔"),
                new PatrolTypeItem("Motorcycle", "Moto",     "🏍️"),
                new PatrolTypeItem("Bicycle",    "Vélo",     "🚲"),
                new PatrolTypeItem("Pedestrian", "Pédestre", "👮"),
            ]);
            SelectedPatrolType = PatrolTypes[0];

            var vehicleList = await _api.GetAsync<List<VehicleItem>>("api/patrol/vehicles");
            Vehicles = vehicleList != null
                ? new ObservableCollection<VehicleItem>(vehicleList)
                : [];

            var agentList = await _api.GetAsync<List<AgentItem>>("api/patrol/available-agents");
            AvailableAgents = agentList != null
                ? new ObservableCollection<AgentItem>(agentList)
                : [];
        }
        catch (Exception ex)
        {
#if DEBUG
            MobileLogger.Log("PatrolActivation", $"LoadAsync ERROR: {ex.GetType().Name}: {ex.Message}");
#endif
            ErrorMessage = $"Erreur de chargement : {ex.Message}";
            HasError = true;
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleAgent(AgentItem agent)
    {
        if (SelectedAgents.Contains(agent))
            SelectedAgents.Remove(agent);
        else
            SelectedAgents.Add(agent);
    }

    [RelayCommand]
    private async Task ActivatePatrolAsync()
    {
        if (SelectedVehicle is null || SelectedPatrolType is null || string.IsNullOrWhiteSpace(Indicatif))
        {
            ErrorMessage = "Veuillez sélectionner un véhicule, un indicatif et un type de patrouille.";
            HasError = true;
            return;
        }

        IsActivating = true;
        HasError = false;
        try
        {
            var vehicleId = SelectedVehicle.Id;
#if DEBUG
            MobileLogger.Log("PatrolActivation", $"Activate vehicleId={vehicleId} indicatif={Indicatif} type={SelectedPatrolType.Value} agents={SelectedAgents.Count}");
#endif
            await _api.PostAsync($"api/patrol/{vehicleId}/activate", new
            {
                indicatif = Indicatif.Trim(),
                patrolType = SelectedPatrolType.Value,
                agentIds = SelectedAgents.Select(a => a.Id).ToList()
            });
#if DEBUG
            MobileLogger.Log("PatrolActivation", $"Activate OK vehicleId={vehicleId}");
#endif

            // Sélectionner le véhicule pour que le JWT soit mis à jour
            var (success, _) = await _auth.SelectVehicleAsync(vehicleId);

            // Démarrer le GPS (mode véhicule = chef met à jour aussi la position du véhicule)
            if (_features.Current.GpsTrackingEnabled && _auth.VehicleId.HasValue)
            {
                if (!_gps.IsTracking)
                    try { await _gps.StartAsync(_auth.VehicleId.Value); } catch { }
            }

            // SignalR
            if (!_signalR.IsConnected && _auth.Token != null && _auth.VehicleId.HasValue)
                try { await _signalR.ConnectAsync(_auth.Token, _auth.VehicleId.Value); } catch { }

            AppShell.SwitchToTab("missions");
        }
        catch (Exception ex)
        {
#if DEBUG
            MobileLogger.Log("PatrolActivation", $"ERROR: {ex.GetType().Name}: {ex.Message}");
#endif
            ErrorMessage = $"Erreur d'activation : {ex.Message}";
            HasError = true;
        }
        finally { IsActivating = false; }
    }

    [RelayCommand]
    private Task SkipActivationAsync()
    {
        // Le chef peut passer sans activer (ex : déjà en service)
        AppShell.SwitchToTab("missions");
        return Task.CompletedTask;
    }
}

public class VehicleItem
{
    public Guid Id { get; set; }
    public string CallSign { get; set; } = "";
    public string LicensePlate { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Indicatif { get; set; }
    public string DisplayName => string.IsNullOrEmpty(Indicatif)
        ? CallSign
        : $"{CallSign} — {Indicatif}";
}

public class AgentItem
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string BadgeNumber { get; set; } = "";
    public string Role { get; set; } = "";
}

public class PatrolTypeItem
{
    public string Value { get; set; }
    public string Label { get; set; }
    public string Emoji { get; set; }
    public string DisplayName => $"{Emoji} {Label}";

    public PatrolTypeItem(string value, string label, string emoji)
    {
        Value = value;
        Label = label;
        Emoji = emoji;
    }
}
