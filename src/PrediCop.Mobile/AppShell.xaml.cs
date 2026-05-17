using PrediCop.Mobile.Services;

namespace PrediCop.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var auth = Handler?.MauiContext?.Services.GetService<AuthService>();
        if (auth?.IsLoggedIn == true)
            await GoToAsync("//main/missions");
        else
            await GoToAsync("//login");
    }
}
