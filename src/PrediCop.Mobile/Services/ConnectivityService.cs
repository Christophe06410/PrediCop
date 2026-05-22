namespace PrediCop.Mobile.Services;

public interface IConnectivityService
{
    bool IsConnected { get; }
    event EventHandler<bool> ConnectivityChanged;
}

public class ConnectivityService : IConnectivityService, IDisposable
{
    public bool IsConnected =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler<bool>? ConnectivityChanged;

    public ConnectivityService()
    {
        Connectivity.Current.ConnectivityChanged += OnPlatformConnectivityChanged;
    }

    private void OnPlatformConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var connected = e.NetworkAccess == NetworkAccess.Internet;
        ConnectivityChanged?.Invoke(this, connected);
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnPlatformConnectivityChanged;
    }
}
