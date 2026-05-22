using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace PrediCop.Mobile.Services;

/// <summary>
/// Scans for BLE beacons installed in patrol vehicles and auto-assigns the officer
/// to the vehicle whose beacon UUID matches a discovered device.
/// </summary>
public class BleVehicleScanner
{
    private readonly IBluetoothLE _ble;
    private readonly IAdapter _adapter;
    private readonly AuthService _auth;
    private readonly ApiService _api;

    private const int ScanDurationMs = 5000;

    public BleVehicleScanner(IBluetoothLE ble, IAdapter adapter, AuthService auth, ApiService api)
    {
        _ble = ble;
        _adapter = adapter;
        _auth = auth;
        _api = api;
    }

    /// <summary>
    /// Requests Bluetooth permissions, scans BLE for ~5 s, matches discovered
    /// device service UUIDs (or device name/id) against known vehicle BeaconUuids,
    /// and if a match is found calls <see cref="AuthService.SelectVehicleAsync"/>.
    /// </summary>
    /// <returns>Vehicle call sign if auto-assigned, null otherwise.</returns>
    public async Task<string?> ScanAndAssignAsync(CancellationToken ct = default)
    {
        // 1. Check Bluetooth availability
        if (_ble.State == BluetoothState.Unavailable || _ble.State == BluetoothState.Unknown)
            return null;

        // 2. Request permissions (Android 12+ needs BLUETOOTH_SCAN / BLUETOOTH_CONNECT)
        var permissionStatus = await RequestPermissionsAsync();
        if (!permissionStatus)
            return null;

        // 3. Fetch vehicle list with BeaconUuids
        List<ApiVehicleWithBeacon>? vehicles;
        try
        {
            vehicles = await _api.GetAsync<List<ApiVehicleWithBeacon>>("api/vehicles", ct);
        }
        catch
        {
            return null;
        }

        if (vehicles is null or { Count: 0 })
            return null;

        // Build a lookup normalised beacon UUID -> vehicle
        var beaconMap = vehicles
            .Where(v => !string.IsNullOrWhiteSpace(v.BeaconUuid))
            .ToDictionary(
                v => v.BeaconUuid!.ToUpperInvariant().Trim(),
                v => v);

        if (beaconMap.Count == 0)
            return null;

        // 4. Scan
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ApiVehicleWithBeacon? matched = null;

        void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
        {
            // Try service UUIDs advertised by the beacon
            foreach (var svc in e.Device.AdvertisementRecords
                         .Where(r => r.Type == Plugin.BLE.Abstractions.AdvertisementRecordType.UuidsComplete128Bit
                                  || r.Type == Plugin.BLE.Abstractions.AdvertisementRecordType.UuidsIncomplete128Bit)
                         .Select(r => ParseUuidFromBytes(r.Data))
                         .Where(u => u is not null)
                         .Select(u => u!))
            {
                if (beaconMap.TryGetValue(svc, out var v))
                {
                    matched = v;
                    return;
                }
            }

            // Fallback: try device Id as UUID string
            var deviceId = e.Device.Id.ToString().ToUpperInvariant();
            if (beaconMap.TryGetValue(deviceId, out var vById))
                matched = vById;
        }

        _adapter.DeviceDiscovered += OnDeviceDiscovered;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ScanDurationMs);
            try
            {
                await _adapter.StartScanningForDevicesAsync(cancellationToken: cts.Token);
            }
            catch (OperationCanceledException) { /* scan timeout — normal */ }
        }
        finally
        {
            _adapter.DeviceDiscovered -= OnDeviceDiscovered;
            try { await _adapter.StopScanningForDevicesAsync(); } catch { }
        }

        if (matched is null)
            return null;

        // 5. Auto-assign vehicle via AuthService
        var (success, callSign) = await _auth.SelectVehicleAsync(matched.Id);
        return success ? (callSign ?? matched.CallSign) : null;
    }

    private static async Task<bool> RequestPermissionsAsync()
    {
        var results = await Permissions.RequestAsync<BlePermissions>();
        return results == PermissionStatus.Granted;
    }

    /// <summary>Parses a 16-byte little-endian UUID from a BLE advertisement record.</summary>
    private static string? ParseUuidFromBytes(byte[]? data)
    {
        if (data is null || data.Length < 16) return null;
        try
        {
            var guid = new Guid(data);
            return guid.ToString().ToUpperInvariant();
        }
        catch { return null; }
    }

    private class ApiVehicleWithBeacon
    {
        public Guid Id { get; set; }
        public string CallSign { get; set; } = "";
        public string? BeaconUuid { get; set; }
    }
}

/// <summary>
/// Composite permission group for BLE scanning (Android).
/// </summary>
public class BlePermissions : Permissions.BasePlatformPermission
{
#if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
    [
        (Android.Manifest.Permission.AccessFineLocation, true),
        ("android.permission.BLUETOOTH_SCAN", true),
        ("android.permission.BLUETOOTH_CONNECT", true)
    ];
#endif
}
