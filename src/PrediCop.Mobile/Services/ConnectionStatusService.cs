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

    // Grace period : le bandeau dégradé n'apparaît qu'après 30s de déconnexion SignalR,
    // pour éviter de l'afficher le temps de la connexion initiale (post-login).
    private const int DegradedGraceSeconds = 30;
    private bool _isDegradedVisible = false;
    private CancellationTokenSource? _gracePeriodCts;

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
        private set { if (_state != value) { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(IsRealtime)); } }
    }

    /// <summary>Vrai uniquement quand les notifications temps réel fonctionnent.</summary>
    public bool IsRealtime => State == Level.Realtime;

    /// <summary>
    /// Vrai après 30s de déconnexion SignalR continue (grace period pour éviter le flash au démarrage).
    /// Utilisé par AppHeader pour afficher le bandeau avertissement.
    /// </summary>
    public bool IsDegraded => _isDegradedVisible;

    public string StatusText => State switch
    {
        Level.Realtime   => "CONNECTÉ",
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
            StartGracePeriodIfNeeded();
            return;
        }

        var newState = _signalR.Status switch
        {
            SignalRStatus.Connected                                  => Level.Realtime,
            SignalRStatus.Connecting or SignalRStatus.Reconnecting   => Level.Connecting,
            _                                                        => Level.Degraded
        };
        State = newState;

        if (newState is Level.Realtime or Level.Connecting)
        {
            CancelGracePeriod();
            SetDegradedVisible(false);
        }
        else
        {
            StartGracePeriodIfNeeded();
        }
    }

    private void StartGracePeriodIfNeeded()
    {
        if (_gracePeriodCts is not null) return;
        _gracePeriodCts = new CancellationTokenSource();
        var token = _gracePeriodCts.Token;
        Task.Delay(TimeSpan.FromSeconds(DegradedGraceSeconds), token)
            .ContinueWith(t =>
            {
                if (!t.IsCanceled) SetDegradedVisible(true);
            }, TaskScheduler.Default);
    }

    private void CancelGracePeriod()
    {
        _gracePeriodCts?.Cancel();
        _gracePeriodCts?.Dispose();
        _gracePeriodCts = null;
    }

    private void SetDegradedVisible(bool visible)
    {
        if (_isDegradedVisible == visible) return;
        _isDegradedVisible = visible;
        OnPropertyChanged(nameof(IsDegraded));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _signalR.StatusChanged -= OnSignalRStatusChanged;
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        CancelGracePeriod();
    }
}
