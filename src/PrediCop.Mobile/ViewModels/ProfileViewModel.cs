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
    [ObservableProperty] private bool alertSoundEnabled = AppPreferences.AlertSoundEnabled;

    partial void OnAlertSoundEnabledChanged(bool value)
    {
        AppPreferences.AlertSoundEnabled = value;
    }

    public List<VehicleItem> AvailableVehicles { get; private set; } = [];

    public bool IsAdminOrManager =>
        auth.CurrentUser?.Role is "Manager" or "Admin";

    public void LoadProfile()
    {
        if (auth.CurrentUser == null) return;
        UserName = auth.CurrentUser.FullName;
        Badge = $"Rôle : {auth.CurrentUser.Role}";
        CurrentVehicle = auth.VehicleCallSign ?? "Aucun véhicule sélectionné";
    }

    public async Task<List<VehicleItem>> LoadVehiclesAsync()
    {
        IsLoadingVehicles = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var vehicles = await api.GetAsync<List<ApiVehicleDto>>("api/vehicles");
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] GET api/vehicles: {sw.ElapsedMilliseconds}ms — {vehicles?.Count ?? 0} véhicule(s)");
            AvailableVehicles = vehicles?
                .Select(v => new VehicleItem(v.Id, $"{v.CallSign} — {v.LicensePlate}", v.Id == auth.VehicleId))
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

        var (success, _) = await auth.SelectVehicleAsync(vehicleId);
        System.Diagnostics.Debug.WriteLine($"[ProfileVM] auth.SelectVehicleAsync: {sw.ElapsedMilliseconds}ms");
        if (!success) return false;

        CurrentVehicle = callSign;

        // SignalR + GPS se reconnectent en arrière-plan : ne pas bloquer l'UI
        _ = Task.Run(async () =>
        {
            var bgSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await signalR.ConnectAsync(auth.Token!, vehicleId);
                System.Diagnostics.Debug.WriteLine($"[ProfileVM] signalR.ConnectAsync: {bgSw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileVM] signalR.ConnectAsync ÉCHEC: {ex.Message}");
            }

            bgSw.Restart();
            try
            {
                gps.Stop();
                await gps.StartAsync(vehicleId);
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
