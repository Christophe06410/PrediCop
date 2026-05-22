using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Tenants.Count == 0)
            _ = _vm.LoadTenantsAsync();
    }
}
