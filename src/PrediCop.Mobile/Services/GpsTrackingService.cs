namespace PrediCop.Mobile.Services;

public class GpsTrackingService : IDisposable
{
    private readonly ApiService _api;
    private IDisposable? _locationListener;
    private Guid _vehicleId;
    private bool _isTracking;

    public event EventHandler<Location>? PositionChanged;

    public GpsTrackingService(ApiService api)
    {
        _api = api;
    }

    public async Task StartAsync(Guid vehicleId)
    {
        _vehicleId = vehicleId;
        _isTracking = true;

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted) return;

        _locationListener = Geolocation.Default.ListenForeground(new GeolocationListenerRequest
        {
            MinimumTime = TimeSpan.FromSeconds(10),
            MinimumDistance = 20
        }, OnLocationChanged);
    }

    public void Stop()
    {
        _isTracking = false;
        _locationListener?.Dispose();
        _locationListener = null;
    }

    private async void OnLocationChanged(Location location)
    {
        PositionChanged?.Invoke(this, location);
        if (_isTracking && _vehicleId != Guid.Empty)
        {
            try
            {
                await _api.PostAsync($"api/vehicles/{_vehicleId}/position", new
                {
                    latitude = location.Latitude,
                    longitude = location.Longitude
                });
            }
            catch { /* connection errors are non-fatal */ }
        }
    }

    public void Dispose() => Stop();
}
