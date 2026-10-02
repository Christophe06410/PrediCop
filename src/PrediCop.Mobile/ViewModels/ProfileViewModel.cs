using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly ApiService _api;
    private readonly GpsTrackingService _gps;
    private readonly SignalRService _signalR;

    public ProfileViewModel(
        AuthService auth,
        ApiService api,
        GpsTrackingService gps,
        SignalRService signalR)
    {
        _auth = auth;
        _api = api;
        _gps = gps;
        _signalR = signalR;
        UserName = "";
        Badge = "";
        CurrentVehicle = "Aucun véhicule sélectionné";
        AlertSoundEnabled = AppPreferences.AlertSoundEnabled;
    }

    [ObservableProperty] private string _userName;
    [ObservableProperty] private string _badge;
    [ObservableProperty] private string _currentVehicle;
    [ObservableProperty] private bool _isLoadingVehicles;
    [ObservableProperty] private bool _alertSoundEnabled;
    [ObservableProperty] private bool _isInPatrol;

    partial void OnAlertSoundEnabledChanged(bool value)
    {
        AppPreferences.AlertSoundEnabled = value;
    }

    public List<VehicleItem> AvailableVehicles { get; private set; } = [];

    public bool IsAdminOrManager =>
        _auth.CurrentUser?.Role is "Manager" or "Admin";

    public void LoadProfile()
    {
        if (_auth.CurrentUser == null) return;
        UserName = _auth.CurrentUser.FullName;
        Badge = $"Rôle : {_auth.CurrentUser.Role}";
        CurrentVehicle = _auth.VehicleDisplayLabel ?? _auth.VehicleCallSign ?? "Aucun véhicule sélectionné";
        IsInPatrol = _auth.VehicleId.HasValue;
    }

    /// <summary>Si un véhicule est assigné mais que le label ne contient pas encore la plaque,
    /// on va chercher l'info dans l'API et on met à jour l'affichage + le cache.</summary>
    public async Task RefreshVehicleLabelAsync()
    {
        if (_auth.VehicleId is null) return;

        // La plaque est déjà dans le label (format "CallSign — Plaque") — rien à faire
        if (_auth.VehicleDisplayLabel?.Contains('—') == true)
        {
            CurrentVehicle = _auth.VehicleDisplayLabel;
            return;
        }

        try
        {
            var vehicles = await _api.GetAsync<List<ApiVehicleDto>>("api/vehicles");
            var match = vehicles?.FirstOrDefault(v => v.Id == _auth.VehicleId);
            if (match is null) return;

            var label = $"{match.CallSign} — {match.LicensePlate}";
            CurrentVehicle = label;
            _auth.SetVehicleDisplayLabel(label);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] RefreshVehicleLabelAsync ÉCHEC: {ex.Message}");
        }
    }

    public async Task<List<VehicleItem>> LoadVehiclesAsync()
    {
        IsLoadingVehicles = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var vehicles = await _api.GetAsync<List<ApiVehicleDto>>("api/vehicles");
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] GET api/vehicles: {sw.ElapsedMilliseconds}ms — {vehicles?.Count ?? 0} véhicule(s)");
            AvailableVehicles = vehicles?
                .Select(v => new VehicleItem(v.Id, $"{v.CallSign} — {v.LicensePlate}", v.Id == _auth.VehicleId))
                .ToList() ?? [];
            return AvailableVehicles;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] GET api/vehicles ÉCHEC après {sw.ElapsedMilliseconds}ms : {ex.Message}");
            return [];
        }
        finally { IsLoadingVehicles = false; }
    }

    public async Task<bool> SelectVehicleAsync(Guid vehicleId, string callSign)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var (success, _) = await _auth.SelectVehicleAsync(vehicleId);
        System.Diagnostics.Debug.WriteLine($"[ProfileVM] auth.SelectVehicleAsync: {sw.ElapsedMilliseconds}ms");
        if (!success) return false;

        CurrentVehicle = callSign;
        IsInPatrol = true;
        _auth.SetVehicleDisplayLabel(callSign);

        // SignalR + GPS se reconnectent en arrière-plan : ne pas bloquer l'UI
        _ = Task.Run(async () =>
        {
            var bgSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _signalR.ConnectAsync(_auth.Token!, vehicleId);
                System.Diagnostics.Debug.WriteLine($"[ProfileVM] signalR.ConnectAsync: {bgSw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileVM] signalR.ConnectAsync ÉCHEC: {ex.Message}");
            }

            bgSw.Restart();
            try
            {
                _gps.Stop();
                await _gps.StartAsync(vehicleId);
                System.Diagnostics.Debug.WriteLine($"[ProfileVM] gps.StartAsync: {bgSw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileVM] gps.StartAsync ÉCHEC: {ex.Message}");
            }
        });

        System.Diagnostics.Debug.WriteLine($"[ProfileVM] SelectVehicleAsync (UI total): {sw.ElapsedMilliseconds}ms");
        return true;
    }

    /// <summary>Demande la permission GPS sur le thread principal avant que l'utilisateur sélectionne un véhicule.
    /// L'IPC Android de RequestAsync peut prendre plusieurs secondes — l'appeler en amont évite de geler l'UI.</summary>
    public static async Task EnsureLocationPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    }

    public async Task<bool> LeavePatrolAsync()
    {
        try
        {
            await _api.PostAsync("api/patrol/leave", null);
            _gps.Stop();
            _auth.ClearVehicle();
            CurrentVehicle = "Aucun véhicule sélectionné";
            IsInPatrol = false;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] LeavePatrolAsync ÉCHEC: {ex.Message}");
            return false;
        }
    }

    public void StopGps() => _gps.Stop();

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _auth.Logout();
        _gps.Stop();
        await Shell.Current.GoToAsync("//login");
    }

    public record VehicleItem(Guid Id, string Label, bool IsCurrent);

    private class ApiVehicleDto
    {
        public Guid Id { get; set; }
        public string CallSign { get; set; } = "";
        public string LicensePlate { get; set; } = "";
    }
}
