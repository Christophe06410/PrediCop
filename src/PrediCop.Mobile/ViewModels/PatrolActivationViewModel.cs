using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class PatrolActivationViewModel(
    ApiService api,
    AuthService auth,
    GpsTrackingService gps,
    SignalRService signalR,
    TenantFeaturesService features) : ObservableObject
{
    [ObservableProperty] private ObservableCollection<VehicleItem> vehicles = [];
    [ObservableProperty] private VehicleItem? selectedVehicle;
    [ObservableProperty] private string indicatif = "";
    [ObservableProperty] private ObservableCollection<PatrolTypeItem> patrolTypes = [];
    [ObservableProperty] private PatrolTypeItem? selectedPatrolType;
    [ObservableProperty] private ObservableCollection<AgentItem> availableAgents = [];
    [ObservableProperty] private ObservableCollection<AgentItem> selectedAgents = [];
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isActivating;
    [ObservableProperty] private string errorMessage = "";
    [ObservableProperty] private bool hasError;

    public bool CanActivate =>
        SelectedVehicle != null && !string.IsNullOrWhiteSpace(Indicatif) && SelectedPatrolType != null;

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            PatrolTypes = new ObservableCollection<PatrolTypeItem>([
                new PatrolTypeItem("Car",        "Voiture",  "🚔"),
                new PatrolTypeItem("Motorcycle", "Moto",     "🏍️"),
                new PatrolTypeItem("Bicycle",    "Vélo",     "🚲"),
                new PatrolTypeItem("Pedestrian", "Pédestre", "👮"),
            ]);
            SelectedPatrolType = PatrolTypes[0];

            var vehicleList = await api.GetAsync<List<VehicleItem>>("api/patrol/vehicles");
            Vehicles = vehicleList != null
                ? new ObservableCollection<VehicleItem>(vehicleList)
                : [];

            var agentList = await api.GetAsync<List<AgentItem>>("api/patrol/available-agents");
            AvailableAgents = agentList != null
                ? new ObservableCollection<AgentItem>(agentList)
                : [];
        }
        catch (Exception ex)
        {
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
            await api.PostAsync($"api/patrol/{vehicleId}/activate", new
            {
                indicatif = Indicatif.Trim(),
                patrolType = SelectedPatrolType.Value,
                agentIds = SelectedAgents.Select(a => a.Id).ToList()
            });

            // Sélectionner le véhicule pour que le JWT soit mis à jour
            var (success, _) = await auth.SelectVehicleAsync(vehicleId);

            // Démarrer le GPS (mode véhicule = chef met à jour aussi la position du véhicule)
            if (features.Current.GpsTrackingEnabled && auth.VehicleId.HasValue)
            {
                if (!gps.IsTracking)
                    try { await gps.StartAsync(auth.VehicleId.Value); } catch { }
            }

            // SignalR
            if (!signalR.IsConnected && auth.Token != null && auth.VehicleId.HasValue)
                try { await signalR.ConnectAsync(auth.Token, auth.VehicleId.Value); } catch { }

            await Shell.Current.GoToAsync("//main/missions");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur d'activation : {ex.Message}";
            HasError = true;
        }
        finally { IsActivating = false; }
    }

    [RelayCommand]
    private async Task SkipActivationAsync()
    {
        // Le chef peut passer sans activer (ex : déjà en service)
        await Shell.Current.GoToAsync("//main/missions");
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
