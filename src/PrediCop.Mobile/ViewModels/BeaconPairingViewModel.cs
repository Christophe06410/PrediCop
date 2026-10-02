using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;
using System.Collections.ObjectModel;

namespace PrediCop.Mobile.ViewModels;

public partial class BeaconPairingViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly BleVehicleScanner _bleScanner;

    public BeaconPairingViewModel(ApiService api, BleVehicleScanner bleScanner)
    {
        _api = api;
        _bleScanner = bleScanner;
        StatusMessage = "Sélectionnez un véhicule puis lancez le scan.";
    }

    private VehicleItem? _selectedVehicle;
    public VehicleItem? SelectedVehicle
    {
        get => _selectedVehicle;
        set
        {
            if (SetProperty(ref _selectedVehicle, value))
            {
                OnPropertyChanged(nameof(CanScan));
                OnPropertyChanged(nameof(IsVehicleSelected));
                OnPropertyChanged(nameof(SelectedVehicleBeacon));
            }
        }
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { if (SetProperty(ref _isScanning, value)) OnPropertyChanged(nameof(CanScan)); }
    }

    private bool _isSaving;
    public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

    private bool _hasBeacons;
    public bool HasBeacons { get => _hasBeacons; set => SetProperty(ref _hasBeacons, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public bool IsVehicleSelected => SelectedVehicle is not null;
    public bool CanScan => IsVehicleSelected && !IsScanning;
    public string SelectedVehicleBeacon => SelectedVehicle?.BeaconDisplay ?? "";

    public ObservableCollection<VehicleItem> Vehicles { get; } = [];
    public ObservableCollection<DiscoveredBeacon> DiscoveredBeacons { get; } = [];

    public async Task LoadVehiclesAsync()
    {
        try
        {
            var list = await _api.GetAsync<List<ApiVehicleDto>>("api/vehicles");
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
            var beacons = await _bleScanner.ScanForPairingAsync();
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
            await _api.PutAsync<object>($"api/vehicles/{SelectedVehicle.Id}", new { BeaconUuid = beacon.Uuid });
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
