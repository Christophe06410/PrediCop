using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;
    private CancellationTokenSource? _spinCts;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LoginViewModel.IsLoadingTenants))
            {
                if (_vm.IsLoadingTenants)
                    StartSpin();
                else
                    StopSpin();
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Tenants.Count == 0)
            _ = _vm.LoadTenantsAsync();
    }

    private void StartSpin()
    {
        _spinCts?.Cancel();
        _spinCts = new CancellationTokenSource();
        var token = _spinCts.Token;
        RefreshIcon.Rotation = 0;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    RefreshIcon.RotateTo(RefreshIcon.Rotation + 360, 600, Easing.Linear));
                if (token.IsCancellationRequested) break;
            }
        }, token);
    }

    private void StopSpin()
    {
        _spinCts?.Cancel();
        _spinCts = null;
        RefreshIcon.Rotation = 0;
    }
}
