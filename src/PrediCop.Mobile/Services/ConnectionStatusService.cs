using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PrediCop.Mobile.Services;

/// <summary>
/// Combine l'état réseau (REST) et l'état temps réel (SignalR) en un statut affichable.
/// Réseau OK ≠ temps réel OK : si SignalR est down mais le réseau up, on est en MODE DÉGRADÉ
/// (les notifications push n'arrivent pas, seul le polling REST rattrape les missions).
/// Singleton, écouté par l'AppHeader (pastille de statut).
/// </summary>
public class ConnectionStatusService : INotifyPropertyChanged, IDisposable
{
    private readonly SignalRService _signalR;
    private readonly IConnectivityService _connectivity;

    public ConnectionStatusService(SignalRService signalR, IConnectivityService connectivity)
    {
        _signalR = signalR;
        _connectivity = connectivity;
        _signalR.StatusChanged += OnSignalRStatusChanged;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        Recompute();
    }

    private void OnSignalRStatusChanged(object? sender, SignalRStatus status) => Recompute();
    private void OnConnectivityChanged(object? sender, bool connected) => Recompute();

    public enum Level { Realtime, Degraded, Offline, Connecting }

    private Level _state = Level.Connecting;
    public Level State
    {
        get => _state;
        private set { if (_state != value) { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(IsRealtime)); OnPropertyChanged(nameof(IsDegraded)); } }
    }

    /// <summary>Vrai uniquement quand les notifications temps réel fonctionnent.</summary>
    public bool IsRealtime => State == Level.Realtime;

    /// <summary>Vrai quand le réseau marche mais pas le temps réel → afficher un avertissement.</summary>
    public bool IsDegraded => State is Level.Degraded or Level.Offline;

    public string StatusText => State switch
    {
        Level.Realtime   => "TEMPS RÉEL",
        Level.Connecting => "CONNEXION…",
        Level.Degraded   => "MODE DÉGRADÉ",
        _                => "HORS LIGNE"
    };

    public string StatusColor => State switch
    {
        Level.Realtime   => "#22c55e", // vert
        Level.Connecting => "#facc15", // jaune
        Level.Degraded   => "#facc15", // jaune
        _                => "#ef4444"  // rouge
    };

    private void Recompute()
    {
        if (!_connectivity.IsConnected)
        {
            State = Level.Offline;
            return;
        }

        State = _signalR.Status switch
        {
            SignalRStatus.Connected                                  => Level.Realtime,
            SignalRStatus.Connecting or SignalRStatus.Reconnecting   => Level.Connecting,
            _                                                        => Level.Degraded
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _signalR.StatusChanged -= OnSignalRStatusChanged;
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
    }
}
