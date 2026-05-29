using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;
using System.Collections.ObjectModel;

namespace PrediCop.Mobile.ViewModels;

public partial class BeaconPairingViewModel(ApiService api, BleVehicleScanner bleScanner) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    [NotifyPropertyChangedFor(nameof(IsVehicleSelected))]
    [NotifyPropertyChangedFor(nameof(SelectedVehicleBeacon))]
    private VehicleItem? selectedVehicle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    private bool isScanning;

    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool hasBeacons;
    [ObservableProperty] private string statusMessage = "Sélectionnez un véhicule puis lancez le scan.";

    public bool IsVehicleSelected => SelectedVehicle is not null;
    public bool CanScan => IsVehicleSelected && !IsScanning;
    public string SelectedVehicleBeacon => SelectedVehicle?.BeaconDisplay ?? "";

    public ObservableCollection<VehicleItem> Vehicles { get; } = [];
    public ObservableCollection<DiscoveredBeacon> DiscoveredBeacons { get; } = [];

    public async Task LoadVehiclesAsync()
    {
        try
        {
            var list = await api.GetAsync<List<ApiVehicleDto>>("api/vehicles");
            Vehicles.Clear();
            foreach (var v in list ?? [])
                Vehicles.Add(new VehicleItem(v.Id, v.CallSign, v.LicensePlate, v.BeaconUuid));
        }
        catch
        {
            StatusMessage = "Impossible de charger la liste des véhicules.";
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        DiscoveredBeacons.Clear();
        HasBeacons = false;
        IsScanning = true;
        StatusMessage = "Scan Bluetooth en cours (5 s)…";
        try
        {
            var beacons = await bleScanner.ScanForPairingAsync();
            foreach (var b in beacons)
                DiscoveredBeacons.Add(b);
            HasBeacons = beacons.Count > 0;
            StatusMessage = beacons.Count > 0
                ? $"{beacons.Count} beacon(s) détecté(s). Touchez « Associer » pour lier au véhicule sélectionné."
                : "Aucun beacon détecté. Vérifiez que le Bluetooth est activé et approchez-vous de l'appareil.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur scan : {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task<bool> AssociateBeaconAsync(DiscoveredBeacon beacon)
    {
        if (SelectedVehicle is null) return false;
        IsSaving = true;
        try
        {
            await api.PutAsync<object>($"api/vehicles/{SelectedVehicle.Id}", new { BeaconUuid = beacon.Uuid });
            SelectedVehicle = SelectedVehicle with { BeaconUuid = beacon.Uuid };
            StatusMessage = $"✓ Beacon associé à {SelectedVehicle.CallSign}.";
            return true;
        }
        catch
        {
            StatusMessage = "Erreur lors de l'enregistrement. Vérifiez la connexion.";
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public record VehicleItem(Guid Id, string CallSign, string LicensePlate, string? BeaconUuid)
    {
        public string Label => $"{CallSign} — {LicensePlate}";
        public string BeaconDisplay => BeaconUuid is null
            ? "Aucun beacon configuré"
            : $"Beacon actuel : {(BeaconUuid.Length > 14 ? BeaconUuid[..14] + "…" : BeaconUuid)}";
    }

    private class ApiVehicleDto
    {
        public Guid Id { get; set; }
        public string CallSign { get; set; } = "";
        public string LicensePlate { get; set; } = "";
        public string? BeaconUuid { get; set; }
    }
}
