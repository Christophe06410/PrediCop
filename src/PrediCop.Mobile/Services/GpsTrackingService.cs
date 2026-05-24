namespace PrediCop.Mobile.Services;

public class GpsTrackingService : IDisposable
{
    private readonly ApiService _api;
    private CancellationTokenSource? _cts;
    private Guid _vehicleId;
    private bool _agentMode; // true = tracking individuel uniquement (PatrolAgent / PatrolLeader sans véhicule)

    public event EventHandler<Location>? PositionChanged;
    public bool IsTracking => _cts != null && !_cts.IsCancellationRequested;

    public GpsTrackingService(ApiService api) => _api = api;

    /// <summary>Démarre le tracking GPS lié à un véhicule (Officer / PatrolLeader avec véhicule actif).</summary>
    public async Task StartAsync(Guid vehicleId)
    {
        if (IsTracking) return;
        _vehicleId = vehicleId;
        _agentMode = false;
        await StartTrackingAsync();
    }

    /// <summary>Démarre le tracking GPS individuel (PatrolAgent ou PatrolLeader avant activation).</summary>
    public async Task StartAgentTrackingAsync()
    {
        if (IsTracking) return;
        _vehicleId = Guid.Empty;
        _agentMode = true;
        await StartTrackingAsync();
    }

    private async Task StartTrackingAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted) return;
        _cts = new CancellationTokenSource();
        _ = PollLocationAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task PollLocationAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var location = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)), ct);
                if (location != null)
                    await OnLocationChangedAsync(location);
            }
            catch (OperationCanceledException) { break; }
            catch { /* non-fatal */ }

            try { await Task.Delay(TimeSpan.FromSeconds(10), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task OnLocationChangedAsync(Location location)
    {
        PositionChanged?.Invoke(this, location);
        try
        {
            // Toujours envoyer la position individuelle (pour les dots sur la carte)
            await _api.PostAsync("api/patrol/my-position", new
            {
                latitude = location.Latitude,
                longitude = location.Longitude
            });

            // En mode véhicule, mettre aussi à jour la position du véhicule (icône patrouille)
            if (!_agentMode && _vehicleId != Guid.Empty)
            {
                await _api.PostAsync($"api/vehicles/{_vehicleId}/position", new
                {
                    latitude = location.Latitude,
                    longitude = location.Longitude
                });
            }
        }
        catch { /* connection errors are non-fatal */ }
    }

    public void Dispose() => Stop();
}
