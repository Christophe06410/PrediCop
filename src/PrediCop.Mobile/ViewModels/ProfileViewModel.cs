using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class ProfileViewModel(
    AuthService auth,
    ApiService api,
    GpsTrackingService gps,
    SignalRService signalR) : ObservableObject
{
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string badge = "";
    [ObservableProperty] private string currentVehicle = "Aucun véhicule sélectionné";
    [ObservableProperty] private bool isLoadingVehicles;

    public List<VehicleItem> AvailableVehicles { get; private set; } = [];

    public void LoadProfile()
    {
        if (auth.CurrentUser == null) return;
        UserName = auth.CurrentUser.FullName;
        Badge = $"Rôle : {auth.CurrentUser.Role}";
        CurrentVehicle = auth.VehicleId.HasValue ? "Véhicule assigné" : "Aucun véhicule sélectionné";
    }

    public async Task<List<VehicleItem>> LoadVehiclesAsync()
    {
        IsLoadingVehicles = true;
        try
        {
            var vehicles = await api.GetAsync<List<ApiVehicleDto>>("api/vehicles");
            AvailableVehicles = vehicles?
                .Select(v => new VehicleItem(v.Id, $"{v.CallSign} — {v.LicensePlate}", v.Id == auth.VehicleId))
                .ToList() ?? [];
            return AvailableVehicles;
        }
        catch { return []; }
        finally { IsLoadingVehicles = false; }
    }

    public async Task<bool> SelectVehicleAsync(Guid vehicleId, string callSign)
    {
        var (success, _) = await auth.SelectVehicleAsync(vehicleId);
        if (!success) return false;

        CurrentVehicle = callSign;

        // Reconnect SignalR with the new vehicleId and restart GPS
        try { await signalR.ConnectAsync(auth.Token!, vehicleId); } catch { }
        try
        {
            gps.Stop();
            await gps.StartAsync(vehicleId);
        }
        catch { }

        return true;
    }

    public void StopGps() => gps.Stop();

    [RelayCommand]
    private async Task LogoutAsync()
    {
        auth.Logout();
        gps.Stop();
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
