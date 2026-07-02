using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Views;

public partial class AppHeader : ContentView
{
    private MissionAlertService? _alertService;
    private ConnectionStatusService? _connectionStatus;

    public AppHeader()
    {
        InitializeComponent();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Toujours désabonner en premier pour éviter les subscriptions en double
        if (_alertService is not null)
            _alertService.PropertyChanged -= OnAlertServiceChanged;
        if (_connectionStatus is not null)
            _connectionStatus.PropertyChanged -= OnConnectionStatusChanged;

        if (Handler is null) return;

        _alertService = Handler.MauiContext?.Services.GetService<MissionAlertService>();
        _connectionStatus = Handler.MauiContext?.Services.GetService<ConnectionStatusService>();
#if DEBUG
        MobileLogger.Log("AppHeader", $"OnHandlerChanged handler={Handler is not null}, alertService={_alertService is not null}");
#endif

        if (_connectionStatus is not null)
        {
            _connectionStatus.PropertyChanged += OnConnectionStatusChanged;
            ApplyConnectionStatus(_connectionStatus); // sync état courant
        }

        if (_alertService is null) return;

        _alertService.PropertyChanged += OnAlertServiceChanged;

        // Sync l'état courant (alerte déjà active quand la page devient visible)
        if (_alertService.HasAlert)
            ShowBannerSafe(_alertService.AlertTitle, _alertService.AlertAddress);
    }

    private void OnConnectionStatusChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var svc = _connectionStatus;
        if (svc is null) return;
        if (MainThread.IsMainThread)
            ApplyConnectionStatus(svc);
        else
            MainThread.BeginInvokeOnMainThread(() => ApplyConnectionStatus(svc));
    }

    private void ApplyConnectionStatus(ConnectionStatusService svc)
    {
        var color = Color.FromArgb(svc.StatusColor);
        StatusDot.Fill = color;
        StatusLabel.Text = svc.StatusText;
        StatusLabel.TextColor = color;
        DegradedBanner.IsVisible = svc.IsDegraded;
    }

    private void OnAlertServiceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MissionAlertService.HasAlert)) return;

        // Capture la ref locale pour éviter un NPE si _alertService change entre maintenant
        // et l'exécution du lambda (race condition Handler null / Show concurrent)
        var svc = _alertService;
        if (svc is null) return;

        if (MainThread.IsMainThread)
            ApplyAlertState(svc);
        else
            MainThread.BeginInvokeOnMainThread(() => ApplyAlertState(svc));
    }

    private void ApplyAlertState(MissionAlertService svc)
    {
        if (svc.HasAlert)
            ShowBannerSafe(svc.AlertTitle, svc.AlertAddress);
        else
            HideBanner();
    }

    private void ShowBannerSafe(string title, string address)
    {
        AlertTitleLabel.Text = title;
        AlertAddressLabel.Text = address;
        MissionBanner.IsVisible = true;
        // L'animation est cosmétique — on ne laisse pas une exception la propager
        try { StartPulse(); } catch { }
    }

    private void HideBanner()
    {
        try { StopPulse(); } catch { }
        MissionBanner.IsVisible = false;
    }

    private void StartPulse()
    {
        this.AbortAnimation("MissionPulse");
        var anim = new Animation(v => MissionBanner.Opacity = v, 1.0, 0.55);
        anim.Commit(this, "MissionPulse", length: 650, easing: Easing.SinInOut,
            repeat: () => _alertService?.HasAlert == true);
    }

    private void StopPulse()
    {
        this.AbortAnimation("MissionPulse");
        MissionBanner.Opacity = 1.0;
    }

    private void OnDismissAlertTapped(object? sender, TappedEventArgs e) =>
        _alertService?.Dismiss();

    private async void OnGoToMissionsClicked(object sender, EventArgs e)
    {
        _alertService?.Dismiss();
        try { await Shell.Current.GoToAsync("//main/missions"); } catch { }
    }
}
